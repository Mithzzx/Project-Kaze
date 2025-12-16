Shader "Hidden/GrassOverlay"
{
    Properties
    {
        _MainTex ("Density Map", 2D) = "white" {}
        _HeightMap ("Height Map", 2D) = "black" {}
        _HeightScale ("Height Scale", Float) = 100
        _Color ("Color", Color) = (0, 1, 0, 0.5)
        _Opacity ("Opacity", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        // Standard blending
        Blend SrcAlpha OneMinusSrcAlpha
        
        // Depth settings to avoid z-fighting but still be occluded by other objects
        ZWrite Off
        ZTest LEqual
        Offset -1, -1 // Push the geometry towards the camera slightly to sit on top of terrain

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
            sampler2D _HeightMap;
            float _HeightScale;
            float4 _Color;
            float _Opacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.uv = v.uv;

                // Sample the heightmap in the vertex shader
                // We use tex2Dlod because vertex shaders don't have automatic mipmap level calculation
                float4 hSample = tex2Dlod(_HeightMap, float4(v.uv, 0, 0));
                
                // Unity Terrain Heightmaps are usually stored in the Red channel (or R+G for high precision)
                // For standard R16 or RFloat, .r is enough.
                float height = hSample.r;

                // Displace vertex Y
                // We assume the input mesh is a flat plane at Y=0
                float4 worldPos = v.vertex;
                worldPos.y += height * _HeightScale * 2.0; // Factor of 2 is often needed for Unity's heightmap encoding range

                o.vertex = UnityObjectToClipPos(worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the density map
                float density = tex2D(_MainTex, i.uv).r;
                
                // Don't draw where there is no grass (optional, or draw faint red)
                if (density <= 0.01) discard;

                // Color based on density
                float4 col = _Color;
                col.a *= density * _Opacity;
                
                return col;
            }
            ENDCG
        }
    }
}
