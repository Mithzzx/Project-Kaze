Shader "Hidden/GrassDensityOverlay"
{
    Properties
    {
        _MainTex ("Density Map", 2D) = "white" {}
        _Color ("Color", Color) = (0, 1, 0, 0.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        // Standard blending
        Blend SrcAlpha OneMinusSrcAlpha
        
        // Depth settings
        ZWrite Off
        ZTest LEqual
        Offset -1, -1 // Bias towards camera to sit on top of terrain

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
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the density map (Red channel)
                float density = tex2D(_MainTex, i.uv).r;
                
                // Discard empty areas to save fill rate and keep view clean
                if (density <= 0.01) discard;

                // Color based on density
                float4 col = _Color;
                // Scale alpha by density so denser areas are more opaque
                col.a *= density;
                
                return col;
            }
            ENDCG
        }
    }
}
