Shader "Custom/GrassShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.5, 0.1, 1)
        _TipColor ("Tip Color", Color) = (0.4, 0.8, 0.2, 1)
        
        [Header(Blade Shape)]
        _BladeWidth ("Blade Width", Float) = 0.05
        _BladeHeight ("Blade Height Multiplier", Float) = 1.0
        
        [Header(Wind)]
        _WindMap ("Wind Noise Texture", 2D) = "white" {}
        _WindVelocity ("Wind Velocity (XY direction)", Vector) = (1, 0, 0, 0)
        _WindFrequency ("Wind Frequency (Tile Size)", Float) = 0.05
        _WindGustStrength ("Gust Strength", Float) = 1.0
        _WindFlutterStrength ("Flutter Strength", Float) = 0.1
        
        [Header(Lighting)]
        _Translucency ("Translucency Strength", Range(0,1)) = 0.5
        _TranslucencyDistortion ("Translucency Distortion", Range(0,1)) = 0.1
        _TranslucencyPower ("Translucency Power", Range(1,10)) = 4.0
        _TranslucencyScale ("Translucency Scale", Range(0,5)) = 1.0
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // --- SHARED CODE START ---
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        // Data Structures
        struct GrassData
        {
            float3 position;
            float height;
            float2 facing;
            float windPhase;
        };
        
        // Variables
        TEXTURE2D(_WindMap);
        SAMPLER(sampler_WindMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _TipColor;
            float4 _WindMap_ST;
            float4 _WindVelocity;
            float _BladeWidth;
            float _BladeHeight;
            float _WindFrequency;
            float _WindGustStrength;
            float _WindFlutterStrength;
            float _Translucency;
            float _TranslucencyDistortion;
            float _TranslucencyPower;
            float _TranslucencyScale;
            float _ShadowStrength;
        CBUFFER_END
        
        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<GrassData> _GrassDataBuffer;
        #endif

        // Helper Functions
        float3 Bezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float u = 1.0 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        float3 BezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float u = 1.0 - t;
            float uu = u * u;
            float tt = t * t;
            return 3 * uu * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * tt * (p3 - p2);
        }

        // Shared Vertex Calculation
        void CalculateGrassVertex(inout float3 positionOS, inout float3 normalOS, float2 uv, GrassData grass)
        {
            // 1. Setup
            float t = saturate(uv.y);
            float height = grass.height * _BladeHeight;
            
            // 2. Wind Calculation
            float3 worldPos = grass.position;
            
            // Macro Wind
            float2 windUV = worldPos.xz * _WindFrequency + _Time.y * _WindVelocity.xy;
            float windSample = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
            
            // Micro Wind (Flutter)
            float flutter = sin(_Time.y * 15.0 + grass.windPhase * 10.0) * _WindFlutterStrength;
            
            // Combine
            float currentWindImpact = (windSample * _WindGustStrength) + flutter;
            
            // Wind Direction (Object Space)
            float3 windDirWS = normalize(float3(_WindVelocity.x, 0, _WindVelocity.y));
            
            // Rotate wind to Object Space
            float2 facing = grass.facing;
            float3 right = float3(facing.y, 0, -facing.x);
            float3 forward = float3(facing.x, 0, facing.y);
            
            float3 windDirOS;
            windDirOS.x = dot(windDirWS, right);
            windDirOS.y = 0;
            windDirOS.z = dot(windDirWS, forward);
            
            // 3. Bezier Control Points
            float3 leanVector = windDirOS * currentWindImpact;
            
            float3 p0 = float3(0, 0, 0);
            float3 p1 = float3(0, height * 0.33, 0);
            float3 p2 = float3(0, height * 0.66, 0) + leanVector;
            float3 p3 = float3(0, height, 0) + leanVector * 1.5;
            
            // 4. Calculate Position
            float3 spinePos = Bezier(p0, p1, p2, p3, t);
            
            // 5. Apply Width
            // Assuming input mesh is a quad with x centered at 0
            // We can use the input positionOS.x to determine width offset
            // Or use UV.x if we want to be procedural about width too
            float width = _BladeWidth;
            float3 widthOffset = float3(positionOS.x * (width / 0.05), 0, 0); // Scale based on default width 0.05
            
            positionOS = spinePos + widthOffset;
            
            // 6. Recalculate Normal
            float3 tangent = normalize(BezierTangent(p0, p1, p2, p3, t));
            normalOS = normalize(cross(float3(1, 0, 0), tangent));
        }
        ENDHLSL
        // --- SHARED CODE END ---
        
        // ------------------------------------------------------------------
        // Forward Lit Pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float heightGradient : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            void setup()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    GrassData grass = _GrassDataBuffer[unity_InstanceID];
                    
                    float2 facing = grass.facing;
                    float3 right = float3(facing.y, 0, -facing.x);
                    float3 up = float3(0, 1, 0);
                    float3 forward = float3(facing.x, 0, facing.y);
                    
                    unity_ObjectToWorld = float4x4(
                        right.x, up.x, forward.x, grass.position.x,
                        right.y, up.y, forward.y, grass.position.y,
                        right.z, up.z, forward.z, grass.position.z,
                        0, 0, 0, 1
                    );
                    
                    unity_WorldToObject = float4x4(
                        right.x, right.y, right.z, -dot(right, grass.position),
                        up.x, up.y, up.z, -dot(up, grass.position),
                        forward.x, forward.y, forward.z, -dot(forward, grass.position),
                        0, 0, 0, 1
                    );
                #endif
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float3 posOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    GrassData grass = _GrassDataBuffer[unity_InstanceID];
                    CalculateGrassVertex(posOS, normalOS, input.uv, grass);
                #endif
                
                output.positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.heightGradient = saturate(input.uv.y);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // 1. Base Color & Gradient
                half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, input.heightGradient);
                
                // 2. Fake AO (Darken roots)
                float heightAO = clamp(input.heightGradient + 0.2, 0, 1);
                color *= heightAO;
                
                // 3. Lighting Data
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = mainLight.direction;
                float3 viewDir = GetWorldSpaceViewDir(input.positionWS);
                
                // Shadow attenuation with strength control
                float shadowAtten = mainLight.shadowAttenuation;
                shadowAtten = lerp(1.0, shadowAtten, _ShadowStrength);

                // 4. Diffuse Lighting
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 diffuse = color * mainLight.color * (NdotL * shadowAtten);
                
                // 5. Translucency (Backlight)
                // Calculate translucency based on light direction relative to view and normal
                // This allows light to pass through the blade when the sun is behind it
                float3 transLightDir = lightDir + normalWS * _TranslucencyDistortion;
                float transDot = pow(saturate(dot(viewDir, -transLightDir)), _TranslucencyPower);
                float3 transLight = transDot * _TranslucencyScale * _Translucency * color * mainLight.color * shadowAtten;
                
                // 6. Ambient
                float3 ambient = SampleSH(normalWS) * color;
                
                // Combine
                float3 finalColor = diffuse + transLight + ambient;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // ------------------------------------------------------------------
        // Shadow Caster Pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            float3 _LightDirection;
            
            void setup()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    GrassData grass = _GrassDataBuffer[unity_InstanceID];
                    
                    float2 facing = grass.facing;
                    float3 right = float3(facing.y, 0, -facing.x);
                    float3 up = float3(0, 1, 0);
                    float3 forward = float3(facing.x, 0, facing.y);
                    
                    unity_ObjectToWorld = float4x4(
                        right.x, up.x, forward.x, grass.position.x,
                        right.y, up.y, forward.y, grass.position.y,
                        right.z, up.z, forward.z, grass.position.z,
                        0, 0, 0, 1
                    );
                    
                    unity_WorldToObject = float4x4(
                        right.x, right.y, right.z, -dot(right, grass.position),
                        up.x, up.y, up.z, -dot(up, grass.position),
                        forward.x, forward.y, forward.z, -dot(forward, grass.position),
                        0, 0, 0, 1
                    );
                #endif
            }
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float3 posOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    GrassData grass = _GrassDataBuffer[unity_InstanceID];
                    CalculateGrassVertex(posOS, normalOS, input.uv, grass);
                #endif
                
                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                return output;
            }
            
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
