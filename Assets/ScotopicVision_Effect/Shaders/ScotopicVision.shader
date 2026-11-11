Shader "Hidden/PCHZ/ScotopicVision"
{
    // Propiedades declaradas para que Material.SetFloat/SetColor (usados por
    // ScotopicVisionController) puedan enviar los uniforms a la GPU. Sin este
    // bloque Unity ignora los Set* y el efecto no se aplica.
    Properties
    {
        _DarknessAmount ("Darkness Amount", Float) = 0
        _EigengrauAmount ("Eigengrau Amount", Float) = 1
        _EigengrauColor ("Eigengrau Color", Color) = (0.030, 0.030, 0.038, 1)
        _ScotopicGain ("Scotopic Gain", Float) = 3
        _MonoAmount ("Mono Amount", Float) = 0.95
        _NoiseIntensity ("Noise Intensity", Float) = 0.035
        _Noise1Scale ("Noise 1 Scale", Float) = 900
        _Noise2Scale ("Noise 2 Scale", Float) = 240
        _NoiseMode ("Noise Mode", Float) = 1
        _NoiseFlickerRate ("Noise Flicker Rate", Float) = 12
        _PeripheralPower ("Peripheral Power", Float) = 0.85
        _CenterWeight ("Center Weight", Float) = 0
        _BlurRadius ("Blur Radius", Float) = 1.5
        _LocalLitSensitivity ("Local Lit Sensitivity", Float) = 6
        _FilterStrength ("Filter Strength", Range(0,1)) = 1
        _FilterGamma ("Filter Gamma", Float) = 1
        _MinDarkness ("Min Darkness Floor", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "ScotopicVision"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl trae el vertice fullscreen (Vert), Varyings, _BlitTexture
            // y el sampler. Es lo que usa Blitter.BlitCameraTexture por debajo.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment frag

            // ------------------------------------------------------ uniforms
            // El controlador (ScotopicVisionController) los setea en el material.
            float  _DarknessAmount;     // 0 = plena luz, 1 = oscuridad adaptada
            float  _EigengrauAmount;    // altura del piso de Eigengrau (1 = gris intrínseco completo)
            half3  _EigengrauColor;     // el Eigengrau (gris oscuro de la retina)
            float  _ScotopicGain;       // ganancia retiniana: amplifica las formas tenues al adaptar
            float  _MonoAmount;         // cuánto desaturar en la oscuridad (bastones)
            float  _NoiseIntensity;     // amplitud del ruido neural
            float  _Noise1Scale;        // escala de la capa 1 de ruido
            float  _Noise2Scale;        // escala de la capa 2 de ruido
            float  _NoiseMode;          // 0=scroll direccional, 1=estatica, 2=particulas
            float  _NoiseFlickerRate;   // cuantas veces por segundo se re-randomiza
            float  _PeripheralPower;    // qué tan rápido crece el efecto hacia los bordes
            float  _CenterWeight;       // peso del efecto en el centro (0 = cono/nítido)
            float  _BlurRadius;         // radio de blur escotópico en pixeles
            float  _LocalLitSensitivity;// qué tan pronto un pixel iluminado queda "nítido"

            // --- Controles maestros (ajustables en vivo desde el inspector/código) ---
            float  _FilterStrength;     // mezcla global del efecto (0 = escena cruda, filtro off)
            float  _FilterGamma;        // curva tonal del look escotópico (1 = sin cambio)
            float  _MinDarkness;        // piso mínimo de oscuridad: "nunca negro puro"

            // Piso mínimo de oscuridad: garantiza "nunca negro puro" incluso
            // cuando el ojo esta adaptado a la luz (linterna encendida, la
            // adaptacion baja y el Eigengrau desapareceria). Los bastones
            // perifericos siguen activos: la zona oscura nunca queda negro
            // muerto. Se anula en el cono iluminado por (1.0 - localLit).
            // El valor lo controla _MinDarkness (antes era una constante).

            // --------------------------------------------------------- ruido
            // Hash y ruido de valor 2D. Dos capas a distinta escala y desfasadas
            // en el tiempo evitan el patrón fijo y dan el "hirviente" del ruido
            // neural de los bastones (igual que el subgrafo del video).
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash22(i + float2(0.0, 0.0)).x;
                float b = hash22(i + float2(1.0, 0.0)).x;
                float c = hash22(i + float2(0.0, 1.0)).x;
                float d = hash22(i + float2(1.0, 1.0)).x;

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float NeuralNoise(float2 uv, float time)
            {
                // --- Modo 0: deriva direccional (el original) ---------------
                // El tiempo se suma a las UV, asi que TODO el campo se desliza
                // en diagonal junto (como scrolling). Sensacion de "viento".
                if (_NoiseMode < 0.5)
                {
                    float n1 = valueNoise(uv * _Noise1Scale + time * 19.0);
                    float n2 = valueNoise(uv * _Noise2Scale - time * 11.0);
                    return n1 * 0.5 + n2 * 0.5;
                }

                // Paso de tiempo discreto: en cada salto el hash re-randomiza.
                // Aqui el tiempo deja de ser un desplazamiento espacial y pasa a
                // ser entrada del hash -> ya no hay direccion, hay parpadeo.
                float step_t = floor(time * max(1.0, _NoiseFlickerRate));

                // --- Modo 1: estatica de TV / grano de pelicula -------------
                // Cada celda fina salta a un valor aleatorio nuevo en cada paso.
                // Cero direccion, puro parpadeo por fotoreceptor (ruido neural
                // real de la retina).
                if (_NoiseMode < 1.5)
                {
                    float2 cell1 = floor(uv * _Noise1Scale);
                    float2 cell2 = floor(uv * _Noise2Scale);
                    float n1 = hash22(cell1 + step_t * float2(1.618, 2.732)).x;
                    float n2 = hash22(cell2 + step_t * float2(3.117, 1.414)).y;
                    return n1 * 0.5 + n2 * 0.5;
                }

                // --- Modo 2: particulas discretas ---------------------------
                // Celdas mas gruesas que parpadean cada una en su propio
                // instante y ciclo (fase random): puntos que aparecen y
                // desaparecen sin direccion, como particulas sueltas.
                float2 cell = floor(uv * _Noise2Scale);
                float2 rnd = hash22(cell);
                float rate = max(1.0, _NoiseFlickerRate) * (0.4 + rnd.x);
                float t = frac(time * 0.08 * rate + rnd.y);
                // Destello breve: solo una fraccion del ciclo esta "encendida",
                // asi la mayoria de celdas esta en reposo (sparse = particulas).
                float pulse = smoothstep(0.72, 0.97, t);
                return 0.5 + (rnd.y - 0.5) * pulse;
            }

            // --------------------------------------------------- disco blur
            // Blur tipo disco con 12 taps. El radio crece con la oscuridad y la
            // distancia periferica: simula la baja agudeza espacial por
            // convergencia de bastones (15-30 bastones -> 1 celula bipolar).
            static const float2 kDisk[12] =
            {
                float2( 0.000,  0.000), float2( 0.527,  0.000),
                float2( 0.264,  0.458), float2(-0.264,  0.458),
                float2(-0.527,  0.000), float2(-0.264, -0.458),
                float2( 0.264, -0.458), float2( 0.809,  0.588),
                float2( 0.000,  0.951), float2(-0.809,  0.588),
                float2(-0.809, -0.588), float2( 0.000, -0.951)
            };

            float3 BlurDisc(float2 uv, float radiusUV)
            {
                float3 acc = 0;
                [unroll]
                for (int k = 0; k < 12; k++)
                {
                    float2 off = kDisk[k] * radiusUV;
                    acc += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + off).rgb;
                }
                return acc / 12.0;
            }

            // -------------------------------------------------------- main
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Escena original.
                float3 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // Luminancia percibida (0.21 R + 0.71 G + 0.07 B). Los bastones
                // no distinguen color, por eso la vision nocturna es monocroma.
                float lum = dot(scene, float3(0.21, 0.71, 0.07));

                // Mascara periférica radial: 0 en el centro (fovea = conos),
                // 1 en los bordes (bastones). Corrige el aspecto de pantalla.
                float2 c = uv - 0.5;
                c.x *= _ScreenParams.x / _ScreenParams.y;
                float dist = saturate(length(c) * 1.55);
                float periph = pow(dist, max(0.05, _PeripheralPower) * 3.0);

                // --- Rango de adaptation (la rampa de oscuridad) -----------
                // _DarknessAmount = 0 (ojo cegado por la luz / recien apagada la
                // linterna) => la zona oscura se ve NEGRO absoluto. Sube a 1 (ojo
                // adaptado) => emerge el look escotopico completo (Eigengrau +
                // ganancia + ruido). Esto da la sensacion de "empezar a ciegas y
                // ir aclarando".
                float adapt = saturate(_DarknessAmount);

                // Oscuridad local: donde una luz fuerte (linterna) alumbra, el
                // pixel queda nitido. 0 = cono iluminado, 1 = zona oscura.
                float localLit = saturate(lum * _LocalLitSensitivity);
                float darkZone  = 1.0 - localLit;

                // Efecto "bastones" (ruido, blur, monocromo): escala con la
                // adaptacion (emerge al aclarar) y pesa hacia la periferia (los
                // bastones estan en los bordes, no en la fovea).
                float rodEffect = saturate(adapt * darkZone * lerp(_CenterWeight, 1.0, periph));

                // --- 1) Blur de bastones: desenfoca la ESCENA cruda ---------
                // La baja agudeza espacial (15-30 bastones -> 1 celula bipolar)
                // desenfoca la imagen que llega a la retina. Se aplica sobre la
                // escena cruda y ANTES del piso de Eigengrau.
                float3 sceneIn = scene;
                if (rodEffect > 0.02 && _BlurRadius > 0.0)
                {
                    float radiusUV = _BlurRadius * rodEffect / _ScreenParams.y;
                    sceneIn = BlurDisc(uv, radiusUV);
                }

                // --- 2) Base escotopica: ganancia + piso de Eigengrau -------
                // La ganancia amplifica la escena oscura para que emerjan las
                // formas. Se multiplica por adapt: con el ojo cegado (adapt=0)
                // todo colapsa a negro; a adapt=1 la ganancia es plena.
                float gain = 1.0 + darkZone * _ScotopicGain;
                float3 amplified = sceneIn * gain * adapt;

                // El Eigengrau (gris intrinseco de la retina) TAMBIEN emerge con
                // la adaptacion: a adapt=0 no hay gris (negro absoluto), a
                // adapt=1 el gris esta completo. _MinDarkness da un piso de gris
                // al final para no quedar en negro puro ya adaptado.
                float eigAmt = max(_EigengrauAmount, _MinDarkness);
                float3 eigengrauFloor = _EigengrauColor * (eigAmt * adapt * darkZone);

                float3 scotopic = max(eigengrauFloor, amplified);

                // --- 3) Gama del filtro (curva tonal de la respuesta oscura) ---
                // Ajusta el contraste/brillo del look escotopico SIN tocar el
                // cono iluminado. >1 = mas oscuro/contraste; <1 = mas claro.
                scotopic = pow(max(scotopic, 0.0), _FilterGamma);

                // Zona iluminada (cono de linterna) -> escena cruda y nitida;
                // zona oscura -> respuesta escotopica (negro al inicio, Eigengrau
                // + formas al adaptarse).
                float3 result = lerp(scene, scotopic, darkZone);

                // --- 4) Monocromatismo (los bastones no ven color) ----------
                float outLum = dot(result, float3(0.21, 0.71, 0.07));
                result = lerp(result, float3(outLum, outLum, outLum), saturate(rodEffect * _MonoAmount));

                // --- 5) Ruido neural animado (el "estatico" de la retina) ----
                float n = NeuralNoise(uv, _Time.y);
                result += (n - 0.5) * _NoiseIntensity * rodEffect;

                // --- 6) Fuerza maestra del filtro ----------------------------
                // Mezcla entre la escena cruda (sin ningún efecto) y el look
                // escotópico completo. _FilterStrength = 1 (filtro encendido),
                // 0 = escena cruda (filtro apagado). Permite fundirlo a gusto.
                result = lerp(scene, result, saturate(_FilterStrength));

                return half4(max(result, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
