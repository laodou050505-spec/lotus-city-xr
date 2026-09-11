Shader "YiNianLotusCity/TitleEdgeFeather"
{
    Properties
    {
        _MainTex ("Cover", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _FeatherWidth ("Edge Feather Width", Range(0.001, 0.15)) = 0.045
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _FeatherWidth;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 colour = tex2D(_MainTex, input.uv) * _Color;

                // Measure both axes against the source image's short side so
                // the 16:9 cover receives an even physical feather on all
                // four edges. Multiplication lets the two fades blend softly
                // together at each corner instead of leaving a hard corner.
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float horizontalEdge = min(input.uv.x, 1.0 - input.uv.x) * aspect;
                float verticalEdge = min(input.uv.y, 1.0 - input.uv.y);
                float horizontalFeather = smoothstep(0.0, _FeatherWidth, horizontalEdge);
                float verticalFeather = smoothstep(0.0, _FeatherWidth, verticalEdge);
                colour.a *= horizontalFeather * verticalFeather;
                return colour;
            }
            ENDCG
        }
    }

    Fallback Off
}
