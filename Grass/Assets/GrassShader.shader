Shader "Custom/GrassShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma target 4.5

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input
        {
            float2 uv_MainTex;
        };

        // --- THE MAGIC PART ---
        // This is where the GPU reads the memory we send from C#
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<float4x4> _PositionBuffer;
        #endif

        // This function runs for every vertex to position it correctly
        void setup()
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                // Get the matrix for this specific blade ID
                float4x4 data = _PositionBuffer[unity_InstanceID];

                // Apply that matrix to the vertex position
                unity_ObjectToWorld = data;
                unity_WorldToObject = unity_ObjectToWorld;
                unity_WorldToObject._14_24_34 *= -1;
                unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
            #endif
        }
        // ----------------------

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}