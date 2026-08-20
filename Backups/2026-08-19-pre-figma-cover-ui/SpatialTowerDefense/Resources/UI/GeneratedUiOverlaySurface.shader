Shader "SpatialTowerDefense/GeneratedUiOverlaySurface"
{
    Properties
    {
        _MainTex ("UI Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" }
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sampled = tex2D(_MainTex, input.uv) * _Color;
                fixed brightest = max(sampled.r, max(sampled.g, sampled.b));
                fixed darkest = min(sampled.r, min(sampled.g, sampled.b));
                fixed chroma = brightest - darkest;
                fixed luminance = dot(sampled.rgb, fixed3(0.299, 0.587, 0.114));
                fixed neutral = 1.0 - smoothstep(0.025, 0.10, chroma);
                fixed lightBackdrop = smoothstep(0.62, 0.82, luminance);
                sampled.a *= 1.0 - neutral * lightBackdrop;
                clip(sampled.a - 0.01);
                return sampled;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
