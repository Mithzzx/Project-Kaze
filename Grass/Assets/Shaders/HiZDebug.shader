Shader "Hidden/HiZDebug"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MipLevel ("Mip Level", Float) = 0
        _DepthScale ("Depth Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _MipLevel;
            float _DepthScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Sample the texture at specified mip level
                float depth = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, i.uv, _MipLevel).r;
                
                // Unity Reverse-Z: 1 = near, 0 = far
                // Invert for visualization (white = near, black = far)
                float visualDepth = depth * _DepthScale;
                
                return half4(visualDepth, visualDepth, visualDepth, 1);
            }
            ENDHLSL
        }
    }
}
