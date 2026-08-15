Shader "PrisaControlada/UI/ChromaKey"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _KeyColor ("Key Color", Color) = (0, 1, 0, 1)
        _Threshold ("Threshold", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 0.5)) = 0.08
        _Spill ("Spill Suppression", Range(0, 1)) = 0.25
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Mode ("Mode (0 = distance, 1 = dominant channel)", Int) = 0
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
            float4 _KeyColor;
            float _Threshold;
            float _Smoothness;
            float _Spill;
            float _Opacity;
            int _Mode;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float keyed;
                if (_Mode > 0.5)
                {
                    // Dominant-channel keying, robust to color-space shifts (e.g.
                    // the video decoder shifting the blue screen). Keys pixels where
                    // the key color's strongest channel clearly beats the other two.
                    float metric;
                    if (_KeyColor.b >= _KeyColor.r && _KeyColor.b >= _KeyColor.g)
                        metric = col.b - max(col.r, col.g);
                    else if (_KeyColor.g >= _KeyColor.r && _KeyColor.g >= _KeyColor.b)
                        metric = col.g - max(col.r, col.b);
                    else
                        metric = col.r - max(col.g, col.b);

                    keyed = smoothstep(_Threshold, _Threshold + _Smoothness, metric);
                }
                else
                {
                    float dist = distance(col.rgb, _KeyColor.rgb);
                    keyed = 1.0 - smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, dist);
                }

                // remove the tint left on the keyed edges
                col.rgb = lerp(col.rgb, col.rgb * 0.5, keyed * _Spill);
                col.a *= (1.0 - keyed) * _Opacity;

                return col;
            }
            ENDCG
        }
    }
}