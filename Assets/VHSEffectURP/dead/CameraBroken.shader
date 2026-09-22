Shader "PrisaControlada/UI/CameraBroken"
{
    Properties
    {
        _MainTex ("Broken Texture (optional)", 2D) = "white" {}
        _UseTexture ("Use Texture", Float) = 0
        _Intensity ("Intensity", Range(0, 1)) = 1
        _Speed ("Speed", Range(0, 5)) = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _UseTexture;
            float _Intensity;
            float _Speed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y * _Speed;

                // darkening vignette
                float d = distance(uv, float2(0.5, 0.5));
                float vignette = smoothstep(0.1, 0.75, d);

                // horizontal tearing bars
                float bar = step(0.965, frac(uv.y * 36.0 + sin(uv.y * 9.0 + t) * 0.6));
                float glitch = bar * (0.5 + 0.5 * sin(uv.x * 5.0 + t * 9.0));

                // flickering cracks
                float crackA = step(0.9985, sin(uv.y * 4.2 + sin(uv.x * 80.0) * 5.0 + t * 2.0));
                float crackB = step(0.998, sin(uv.y * 6.4 + cos(uv.x * 60.0) * 7.0 + t * 1.4));
                float crack = crackA + crackB * 0.6;

                // global flicker
                float flicker = step(0.5, sin(t * 17.0) * 0.5 + 0.5) * 0.5 + 0.5;

                fixed4 tex = tex2D(_MainTex, uv);

                if (_UseTexture > 0.5)
                {
                    float a = tex.a * _Intensity * (0.25 + 0.75 * (1.0 - vignette)) * flicker;
                    return fixed4(tex.rgb, a);
                }

                float dark = _Intensity * 0.5 * (0.4 + 0.6 * vignette);
                float lines = _Intensity * (glitch * 0.9 + crack) * (1.0 - vignette * 0.5) * flicker;
                float a = saturate(dark + lines);
                return fixed4(1.0, 1.0, 1.0, a);
            }
            ENDCG
        }
    }
}