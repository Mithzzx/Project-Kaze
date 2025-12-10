Shader "Custom/GrassShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.5, 0.1, 1)
        _TipColor ("Tip Color", Color) = (0.4, 0.8, 0.2, 1)
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.5
        _WindSpeed ("Wind Speed", Range(0, 5)) = 2.0
        _WindFrequency ("Wind Frequency", Range(0, 5)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // Grass data structure - must match compute shader
            struct GrassData
            {
                float3 position;
                float height;
                float2 facing;
                float windPhase;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _WindStrength;
                float _WindSpeed;
                float _WindFrequency;
            CBUFFER_END
            
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<GrassData> _GrassDataBuffer;
            #endif
            
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
            
            // Instance data stored per-instance
            float _GrassHeight;
            float _GrassWindPhase;
            
            void setup()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    GrassData grass = _GrassDataBuffer[unity_InstanceID];
                    
                    // Build rotation matrix from facing direction
                    float2 facing = grass.facing;
                    float3 right = float3(facing.y, 0, -facing.x);
                    float3 up = float3(0, 1, 0);
                    float3 forward = float3(facing.x, 0, facing.y);
                    
                    // Create transformation matrix
                    unity_ObjectToWorld = float4x4(
                        right.x, up.x, forward.x, grass.position.x,
                        right.y, up.y, forward.y, grass.position.y,
                        right.z, up.z, forward.z, grass.position.z,
                        0, 0, 0, 1
                    );
                    
                    // Inverse transpose for normals (simplified for uniform scale)
                    unity_WorldToObject = float4x4(
                        right.x, right.y, right.z, -dot(right, grass.position),
                        up.x, up.y, up.z, -dot(up, grass.position),
                        forward.x, forward.y, forward.z, -dot(forward, grass.position),
                        0, 0, 0, 1
                    );
                    
                    _GrassHeight = grass.height;
                    _GrassWindPhase = grass.windPhase;
                #endif
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float4 posOS = input.positionOS;
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    // Scale height based on grass data
                    posOS.y *= _GrassHeight;
                    
                    // Store normalized height for color gradient
                    output.heightGradient = saturate(input.positionOS.y);
                    
                    // Wind animation
                    float windTime = _Time.y * _WindSpeed + _GrassWindPhase;
                    float windWave = sin(windTime + posOS.x * _WindFrequency) * 
                                     cos(windTime * 0.7 + posOS.z * _WindFrequency * 0.8);
                    
                    // Apply wind - more effect at the top of the blade
                    float windInfluence = output.heightGradient * output.heightGradient;
                    posOS.x += windWave * _WindStrength * windInfluence;
                    posOS.z += windWave * _WindStrength * 0.5 * windInfluence;
                #else
                    output.heightGradient = saturate(input.positionOS.y);
                #endif
                
                // Transform to world and clip space
                output.positionWS = TransformObjectToWorld(posOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // Gradient from base to tip
                half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, input.heightGradient);
                
                // Simple lighting
                float3 normalWS = normalize(input.normalWS);
                
                // Get main light
                Light mainLight = GetMainLight();
                
                // Lambertian diffuse with wrap lighting for grass
                float NdotL = dot(normalWS, mainLight.direction);
                float wrap = 0.5;
                float diffuse = saturate((NdotL + wrap) / (1.0 + wrap));
                
                // Add some ambient
                half3 ambient = SampleSH(normalWS) * 0.5;
                
                // Final color
                half3 finalColor = color * (mainLight.color * diffuse + ambient);
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
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
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct GrassData
            {
                float3 position;
                float height;
                float2 facing;
                float windPhase;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _WindStrength;
                float _WindSpeed;
                float _WindFrequency;
            CBUFFER_END
            
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<GrassData> _GrassDataBuffer;
            #endif
            
            float _GrassHeight;
            float _GrassWindPhase;
            
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
                    
                    _GrassHeight = grass.height;
                    _GrassWindPhase = grass.windPhase;
                #endif
            }
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            float3 _LightDirection;
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float4 posOS = input.positionOS;
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    posOS.y *= _GrassHeight;
                    
                    float heightGradient = saturate(input.positionOS.y);
                    float windTime = _Time.y * _WindSpeed + _GrassWindPhase;
                    float windWave = sin(windTime + posOS.x * _WindFrequency) * 
                                     cos(windTime * 0.7 + posOS.z * _WindFrequency * 0.8);
                    
                    float windInfluence = heightGradient * heightGradient;
                    posOS.x += windWave * _WindStrength * windInfluence;
                    posOS.z += windWave * _WindStrength * 0.5 * windInfluence;
                #endif
                
                float3 positionWS = TransformObjectToWorld(posOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
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
