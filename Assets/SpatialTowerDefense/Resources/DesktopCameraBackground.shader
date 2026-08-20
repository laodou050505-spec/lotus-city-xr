Shader "Hidden/PicoTowerDefense/DesktopCameraBackground"
{
    Properties
    {
        _MainTex ("Camera", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float2 _CropScale;
            float _RotationSteps;
            float _MirrorX;
            float _MirrorY;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = (input.uv - 0.5) * _CropScale + 0.5;
                if (_MirrorX > 0.5)
                {
                    uv.x = 1.0 - uv.x;
                }
                if (_MirrorY > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                float2 sourceUv = uv;
                if (_RotationSteps > 0.5 && _RotationSteps < 1.5)
                {
                    sourceUv = float2(1.0 - uv.y, uv.x);
                }
                else if (_RotationSteps > 1.5 && _RotationSteps < 2.5)
                {
                    sourceUv = 1.0 - uv;
                }
                else if (_RotationSteps > 2.5)
                {
                    sourceUv = float2(uv.y, 1.0 - uv.x);
                }

                return fixed4(tex2D(_MainTex, sourceUv).rgb, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
