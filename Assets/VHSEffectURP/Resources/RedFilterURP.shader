Shader "Hidden/RedFilterURP"
{
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        ENDHLSL

        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "RedFilter"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            float _Intensity;
            float _RedSensitivity;
            float _RedStrength;
            float _Gamma;
            float _BlurAmount;
            float _BlurRadius;

            float3 ApplyEffects(float3 col)
            {
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float3 gray = lum.xxx;

                float redAmount = saturate((col.r - max(col.g, col.b)) * _RedSensitivity);

                float3 result = lerp(col, gray, _Intensity);
                result = lerp(result, col, redAmount * _RedStrength * _Intensity);

                return pow(saturate(result), _Gamma);
            }

            float4 Frag (Varyings input) : SV_Target
            {
                float3 c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;

                float3 sharp = ApplyEffects(c);

                if (_BlurAmount > 0.001)
                {
                    float2 texel = _BlitTexture_TexelSize.xy * _BlurRadius;

                    float3 sum = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb * 4.0;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, 0)).rgb * 2.0;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(texel.x, 0)).rgb * 2.0;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0, -texel.y)).rgb * 2.0;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(0, texel.y)).rgb * 2.0;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, -texel.y)).rgb;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(texel.x, -texel.y)).rgb;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(-texel.x, texel.y)).rgb;
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + float2(texel.x, texel.y)).rgb;

                    float3 blurred = ApplyEffects(sum / 16.0);
                    sharp = lerp(sharp, blurred, _BlurAmount);
                }

                return float4(sharp, 1);
            }

            ENDHLSL
        }
    }
}