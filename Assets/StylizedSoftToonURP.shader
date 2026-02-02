Shader "Custom/StylizedSoftToonURP"
{
    Properties
    {
        _BaseColor      ("Base Color", Color) = (1,1,1,1)
        _ShadowTint     ("Shadow Tint", Color) = (0.55,0.55,0.60,1)

        _RampThreshold  ("Ramp Threshold", Range(0,1)) = 0.55
        _RampSmoothness ("Ramp Smoothness", Range(0.001,0.5)) = 0.10

        _AmbientColor   ("Ambient Color", Color) = (0.18,0.18,0.18,1)

        _SpecStrength   ("Spec Strength", Range(0,1)) = 0.05
        _SpecPower      ("Spec Power", Range(1,128)) = 32

        _RimColor       ("Rim Color", Color) = (1,1,1,1)
        _RimStrength    ("Rim Strength", Range(0,1)) = 0.10
        _RimPower       ("Rim Power", Range(0.5,8)) = 3.0

        [Toggle(_ADDITIONAL_LIGHTS)] _UseAdditionalLights ("Use Additional Lights", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _RampThreshold;
                float  _RampSmoothness;
                float4 _AmbientColor;
                float  _SpecStrength;
                float  _SpecPower;
                float4 _RimColor;
                float  _RimStrength;
                float  _RimPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
                float4 shadowCoord  : TEXCOORD3;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nor = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = normalize(nor.normalWS);
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                OUT.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);
                return OUT;
            }

            inline float SoftRamp(float ndotl, float threshold, float smoothness)
            {
                float e1 = threshold - smoothness;
                float e2 = threshold + smoothness;
                return smoothstep(e1, e2, ndotl);
            }

            inline float StylizedSpec(float3 N, float3 L, float3 V, float power)
            {
                float3 H = normalize(L + V);
                float ndoth = saturate(dot(N, H));
                return pow(ndoth, power);
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                float3 col = _BaseColor.rgb * _AmbientColor.rgb;

                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 Lm = normalize(mainLight.direction);

                float ndotl_m = saturate(dot(N, Lm));
                float ramp_m  = SoftRamp(ndotl_m, _RampThreshold, _RampSmoothness);

                float3 base_m = lerp(_ShadowTint.rgb, _BaseColor.rgb, ramp_m);
                float atten_m = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                col += base_m * mainLight.color.rgb * atten_m;

                if (_SpecStrength > 0.0001)
                {
                    float spec_m = StylizedSpec(N, Lm, V, _SpecPower);
                    col += spec_m * _SpecStrength * mainLight.color.rgb * atten_m;
                }

                if (_RimStrength > 0.0001)
                {
                    float rim = 1.0 - saturate(dot(N, V));
                    rim = pow(rim, _RimPower);
                    col += rim * _RimStrength * _RimColor.rgb;
                }

                #if defined(_ADDITIONAL_LIGHTS)
                {
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint li = 0u; li < lightCount; li++)
                    {
                        Light light = GetAdditionalLight(li, IN.positionWS);

                        float3 L = normalize(light.direction);
                        float ndotl = saturate(dot(N, L));
                        float ramp  = SoftRamp(ndotl, _RampThreshold, _RampSmoothness);

                        float3 base = lerp(_ShadowTint.rgb, _BaseColor.rgb, ramp);
                        float atten = light.distanceAttenuation * light.shadowAttenuation;

                        col += base * light.color.rgb * atten;

                        if (_SpecStrength > 0.0001)
                        {
                            float spec = StylizedSpec(N, L, V, _SpecPower);
                            col += spec * _SpecStrength * light.color.rgb * atten;
                        }
                    }
                }
                #endif

                return float4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
