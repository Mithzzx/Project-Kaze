Shader "Custom/GrassShader"
{
    Properties
    {
        [Header(Color)]
        _RootColor ("Root Color", Color) = (0.04, 0.08, 0.02, 1)
        _MidColor ("Mid Color", Color) = (0.15, 0.35, 0.08, 1)
        _TipColor ("Tip Color", Color) = (0.45, 0.55, 0.15, 1)
        _DryColor ("Dry/Dead Color", Color) = (0.4, 0.35, 0.15, 1)
        _DryAmount ("Dryness Amount", Range(0, 1)) = 0.15
        _ColorNoiseScale ("Color Noise Scale", Float) = 0.3
        _ColorNoiseStrength ("Color Noise Strength", Range(0, 1)) = 0.2
        
        [Header(Blade Shape)]
        _BladeWidth ("Blade Width", Float) = 0.05
        _BladeHeight ("Blade Height Multiplier", Float) = 1.0
        _BladeBend ("Blade Bend (Natural Curve)", Range(0, 1)) = 0.3
        
        [Header(Wind)]
        _WindMap ("Wind Noise Texture", 2D) = "white" {}
        _WindVelocity ("Wind Velocity (XY direction)", Vector) = (1, 0, 0, 0)
        _WindFrequency ("Wind Frequency (Tile Size)", Float) = 0.05
        _WindGustStrength ("Gust Strength", Float) = 1.0
        _WindFlutterStrength ("Flutter Strength", Float) = 0.1
        _WindDisableDistance ("Wind Disable Distance", Float) = 50.0
        
        [Header(Normal Rounding)]
        _NormalRotationAngle ("Normal Rotation Angle", Range(0, 0.5)) = 0.3
        
        [Header(Lighting)]
        _Translucency ("Translucency (SSS)", Range(0, 1)) = 0.6
        _TranslucencyPower ("Translucency Power", Range(1, 8)) = 3.0
        _AmbientOcclusionStrength ("AO Strength", Range(0, 1)) = 0.7
        _ShadowSoftness ("Shadow Softness", Range(0, 1)) = 0.3
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.15
        _SpecularPower ("Specular Power", Range(8, 128)) = 32
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
            float stiffness;
            float widthScale;
        };
        
        // Variables
        TEXTURE2D(_WindMap);
        SAMPLER(sampler_WindMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _RootColor;
            float4 _MidColor;
            float4 _TipColor;
            float4 _DryColor;
            float4 _WindMap_ST;
            float4 _WindVelocity;
            float _DryAmount;
            float _ColorNoiseScale;
            float _ColorNoiseStrength;
            float _BladeWidth;
            float _BladeHeight;
            float _BladeBend;
            float _WindFrequency;
            float _WindGustStrength;
            float _WindFlutterStrength;
            float _WindDisableDistance;
            float _NormalRotationAngle;
            float _Translucency;
            float _TranslucencyPower;
            float _AmbientOcclusionStrength;
            float _ShadowSoftness;
            float _SpecularStrength;
            float _SpecularPower;
        CBUFFER_END
        
        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<GrassData> _GrassDataBuffer;
        #endif

        // Helper Functions
        float easeOut(float t, float power)
        {
            return 1.0 - pow(abs(1.0 - t), power);
        }
        
        // Rotate vector around Y axis
        float3 RotateAroundY(float3 v, float angle)
        {
            float s = sin(angle);
            float c = cos(angle);
            return float3(
                v.x * c - v.z * s,
                v.y,
                v.x * s + v.z * c
            );
        }
        
        // Simple hash for color variation
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

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
            
            // Calculate distance fade for wind
            float dist = distance(worldPos, _WorldSpaceCameraPos);
            // Fade out wind over 10 meters before the disable distance
            float windFade = 1.0 - saturate((dist - (_WindDisableDistance - 10.0)) / 10.0);
            
            // Macro Wind
            float2 windUV = worldPos.xz * _WindFrequency + _Time.y * _WindVelocity.xy;
            float windSample = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
            
            // Micro Wind (Flutter)
            float flutter = sin(_Time.y * 15.0 + grass.windPhase * 10.0) * _WindFlutterStrength;
            
            // Combine and apply stiffness (lower stiffness = more wind effect)
            // Apply distance fade
            float currentWindImpact = ((windSample * _WindGustStrength) + flutter) * (2.0 - grass.stiffness) * windFade;
            
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
            
            // Tip Flapping:
            // 1. Tip bends more aggressively with strong wind (quadratic response)
            // 2. Extra high-frequency flutter at the tip when wind is strong
            float tipBendFactor = 2.0 + (windSample * 1.0); 
            float tipFlutter = sin(_Time.y * 25.0 + grass.windPhase * 20.0) * windSample * _WindFlutterStrength;
            float3 tipFlutterVec = windDirOS * tipFlutter;

            // Natural bend - grass bends in its facing direction for organic look
            // The bend increases along the height of the blade (quadratic curve)
            float3 naturalBendDir = float3(0, 0, 1); // Bend forward in object space (along facing)
            float bendAmount = _BladeBend * height;
            
            float3 p0 = float3(0, 0, 0);
            float3 p1 = float3(0, height * 0.33, 0) + naturalBendDir * bendAmount * 0.1;
            
            // Calculate target positions for p2 and p3 with wind applied
            float3 p2_target = float3(0, height * 0.66, 0) + leanVector + naturalBendDir * bendAmount * 0.4;
            float3 p3_target = float3(0, height, 0) + (leanVector * tipBendFactor) + tipFlutterVec + naturalBendDir * bendAmount;

            // Length preservation: Constrain control points to their respective heights
            // This ensures the blade bends (rotates) rather than shearing (stretching)
            float3 p2 = normalize(p2_target) * (height * 0.66);
            float3 p3 = normalize(p3_target) * height;
            
            // 4. Calculate Position
            float3 spinePos = Bezier(p0, p1, p2, p3, t);
            
            // 5. Apply Width
            // Assuming input mesh is a quad with x centered at 0
            // We can use the input positionOS.x to determine width offset
            // Or use UV.x if we want to be procedural about width too
            float width = _BladeWidth * grass.widthScale;
            float3 widthOffset = float3(positionOS.x * (width / 0.05), 0, 0); // Scale based on default width 0.05
            
            positionOS = spinePos + widthOffset;
            
            // 6. Recalculate Normal
            float3 tangent = normalize(BezierTangent(p0, p1, p2, p3, t));
            normalOS = normalize(cross(float3(1, 0, 0), tangent));
        }
        
        // Shared setup function for procedural instancing (used by all passes)
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
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
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
                float3 rotatedNormal1 : TEXCOORD3;
                float3 rotatedNormal2 : TEXCOORD4;
                float2 widthAndAO : TEXCOORD5;  // x = widthPercent, y = ao
                float3 grassRootWS : TEXCOORD6;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD7;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
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
                
                // View Space Adjustment (Thicken grass when viewed from side)
                float3 positionVS = TransformWorldToView(output.positionWS);
                
                // Calculate face normal in World Space (blade faces Z in object space)
                float3 faceNormalWS = TransformObjectToWorldNormal(float3(0,0,1));
                float3 viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                
                // Calculate how much we are looking at the edge
                float viewDotNormal = saturate(dot(normalize(faceNormalWS.xz), normalize(viewDirWS.xz)));
                
                // Calculate thicken factor
                float viewSpaceThickenFactor = easeOut(1.0 - viewDotNormal, 4.0);
                viewSpaceThickenFactor *= smoothstep(0.0, 0.2, viewDotNormal);
                
                // Apply thickening in View Space X direction
                float xDirection = (input.uv.x - 0.5) * 2.0;
                positionVS.x += viewSpaceThickenFactor * xDirection * _BladeWidth * 0.5;
                
                output.positionCS = TransformWViewToHClip(positionVS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.heightGradient = saturate(input.uv.y);
                
                // Fake normal rounding - rotate normal around Y axis to simulate curved blade
                float rotAngle = PI * _NormalRotationAngle;
                float3 rotatedNormal1OS = RotateAroundY(normalOS, rotAngle);   // Right edge normal
                float3 rotatedNormal2OS = RotateAroundY(normalOS, -rotAngle);  // Left edge normal
                
                output.rotatedNormal1 = TransformObjectToWorldNormal(rotatedNormal1OS);
                output.rotatedNormal2 = TransformObjectToWorldNormal(rotatedNormal2OS);
                output.widthAndAO.x = saturate(input.uv.x);
                
                // Pre-calculate AO in vertex shader (height-based)
                float heightT = saturate(input.uv.y);
                output.widthAndAO.y = saturate(heightT * 2.0 + 0.3);
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    output.grassRootWS = grass.position;
                #else
                    output.grassRootWS = float3(0, 0, 0);
                #endif
                
                // Calculate shadow coordinates
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                #endif
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                float t = input.heightGradient;
                float widthPercent = input.widthAndAO.x;
                float ao = lerp(1.0, input.widthAndAO.y, _AmbientOcclusionStrength);
                
                // === ADVANCED COLOR CALCULATION ===
                // 1. Three-point gradient (root -> mid -> tip)
                half3 healthyColor;
                if (t < 0.5)
                {
                    healthyColor = lerp(_RootColor.rgb, _MidColor.rgb, t * 2.0);
                }
                else
                {
                    healthyColor = lerp(_MidColor.rgb, _TipColor.rgb, (t - 0.5) * 2.0);
                }
                
                // 2. Per-blade color noise (using grass root position)
                float2 noiseUV = input.grassRootWS.xz * _ColorNoiseScale;
                float colorNoise = Hash21(noiseUV);
                
                // 3. Mix in dry/dead color based on noise and dryness amount
                float dryMask = saturate(colorNoise * 2.0 - 1.0 + _DryAmount);
                half3 baseColor = lerp(healthyColor, _DryColor.rgb, dryMask * _DryAmount);
                
                // 4. Add subtle hue variation
                float hueShift = (colorNoise - 0.5) * _ColorNoiseStrength;
                baseColor = lerp(baseColor, baseColor * float3(1.0 + hueShift, 1.0, 1.0 - hueShift * 0.5), _ColorNoiseStrength);
                
                // === NORMAL CALCULATION ===
                float3 blendedNormal = lerp(
                    input.rotatedNormal1,
                    input.rotatedNormal2,
                    widthPercent
                );
                float3 normalWS = normalize(blendedNormal);
                
                // === SHADOW COORD ===
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                // === LIGHTING ===
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = mainLight.direction;
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                
                // Shadow attenuation
                float shadow = mainLight.shadowAttenuation;
                shadow = lerp(shadow, 1.0, _ShadowSoftness * (1.0 - t)); // Softer shadows at base
                
                // Diffuse (wrapped for softer look)
                float NdotL = dot(normalWS, lightDir);
                float wrappedDiffuse = saturate(NdotL * 0.5 + 0.5); // Half-Lambert
                float3 diffuse = baseColor * mainLight.color * wrappedDiffuse * shadow;
                
                // === SUBSURFACE SCATTERING / TRANSLUCENCY ===
                float3 backLightDir = -lightDir + normalWS * 0.3;
                float VdotBackLight = saturate(dot(viewDir, backLightDir));
                float sss = pow(VdotBackLight, _TranslucencyPower) * _Translucency;
                sss *= t;
                float3 subsurface = sss * mainLight.color * baseColor * shadow;
                
                // === SPECULAR ===
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, _SpecularPower) * _SpecularStrength;
                float3 specular = spec * mainLight.color * shadow * t;
                
                // === AMBIENT ===
                float3 ambient = SampleSH(normalWS) * baseColor;
                
                // === FINAL COMBINE ===
                float3 finalColor = (diffuse + subsurface + specular + ambient) * ao;
                
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
            
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            float3 _LightDirection;
            float3 _LightPosition;
            
            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
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
                
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // Fix for missing shadows: Ensure light direction is valid
                if (dot(lightDirectionWS, lightDirectionWS) < 0.0001)
                {
                    lightDirectionWS = GetMainLight().direction;
                }
                
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return output;
            }
            
            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // ------------------------------------------------------------------
        // Depth Only Pass (required for proper depth buffer)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5
            
            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float3 posOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    GrassData grass = _GrassDataBuffer[unity_InstanceID];
                    CalculateGrassVertex(posOS, normalOS, input.uv, grass);
                #endif
                
                float3 positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                
                return output;
            }
            
            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
