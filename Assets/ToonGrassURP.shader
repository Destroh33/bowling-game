Shader "Custom/ToonGrassURP"
{
    Properties
    {
        // Base
        _BaseColor      ("Base Color", Color) = (0.25, 0.75, 0.35, 1)
        _ShadowTint     ("Shadow Tint", Color) = (0.12, 0.35, 0.18, 1)
        _MainTex        ("Base Texture (optional)", 2D) = "white" {}
        _UseMainTex     ("Use Base Texture", Range(0,1)) = 0

        // Toon lighting
        _RampThreshold  ("Ramp Threshold", Range(0,1)) = 0.58
        _RampSmoothness ("Ramp Smoothness", Range(0.001,0.5)) = 0.10
        _AmbientColor   ("Ambient Color", Color) = (0.22,0.22,0.22,1)

        // Spec / Rim (keep subtle for grass)
        _SpecStrength   ("Spec Strength", Range(0,1)) = 0.02
        _SpecPower      ("Spec Power", Range(1,128)) = 32
        _RimColor       ("Rim Color", Color) = (1,1,1,1)
        _RimStrength    ("Rim Strength", Range(0,1)) = 0.06
        _RimPower       ("Rim Power", Range(0.5,8)) = 2.5

        // Mapping
        [Toggle] _WorldXZ ("World-space XZ UV (recommended)", Float) = 1
        _UVScale         ("UV Scale", Range(0.1, 20)) = 4

        // Scribble layer (squiggles)
        _ScribbleColor    ("Scribble Color", Color) = (0.10, 0.35, 0.15, 1)
        _ScribbleStrength ("Scribble Strength", Range(0,1)) = 0.35
        _ScribbleScale    ("Scribble Scale", Range(0.5, 30)) = 10
        _ScribbleThickness("Scribble Thickness", Range(0.001, 0.2)) = 0.04
        _ScribbleWarp     ("Scribble Warp", Range(0, 1)) = 0.35

        // Blade flecks
        _FleckStrength ("Fleck Strength", Range(0,1)) = 0.25
        _FleckScale    ("Fleck Scale", Range(1, 60)) = 22
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowTint;
                float  _UseMainTex;

                float  _RampThreshold;
                float  _RampSmoothness;
                float4 _AmbientColor;

                float  _SpecStrength;
                float  _SpecPower;

                float4 _RimColor;
                float  _RimStrength;
                float  _RimPower;

                float  _WorldXZ;
                float  _UVScale;

                float4 _ScribbleColor;
                float  _ScribbleStrength;
                float  _ScribbleScale;
                float  _ScribbleThickness;
                float  _ScribbleWarp;

                float  _FleckStrength;
                float  _FleckScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float2 uv          : TEXCOORD4;
            };

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

            // Cheap hash/noise for warping patterns (stable, fast)
            inline float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            inline float2 WarpUV(float2 uv, float amount)
            {
                float n1 = Hash21(uv * 3.1);
                float n2 = Hash21(uv * 3.1 + 17.7);
                float2 w = (float2(n1, n2) - 0.5) * 2.0;
                return uv + w * amount;
            }

            // Procedural scribble: curvy stripes + warp
            inline float ScribbleMask(float2 uv, float scale, float thickness, float warp)
            {
                uv *= scale;
                uv = WarpUV(uv, warp);

                // Curvy line field
                float f = sin(uv.x + sin(uv.y * 1.3)) + sin(uv.y * 1.1);
                // Turn field into thin lines around 0-crossings
                float a = abs(f);
                float lines = smoothstep(thickness, 0.0, a); // thinner as thickness decreases
                return lines;
            }

            // Flecks: little vertical-ish blades / dots
            inline float FleckMask(float2 uv, float scale)
            {
                uv *= scale;
                float2 cell = floor(uv);
                float2 local = frac(uv);

                // Random per cell
                float r = Hash21(cell);
                // “Blade” position
                float2 center = frac(float2(r, Hash21(cell + 9.7)));

                // Thin vertical-ish mark
                float dx = abs(local.x - center.x);
                float dy = abs(local.y - center.y);

                // Tall-ish fleck: narrow in x, longer in y
                float blade = smoothstep(0.18, 0.0, dx) * smoothstep(0.35, 0.0, dy);
                return blade;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nor = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = normalize(nor.normalWS);

                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                OUT.shadowCoord = TransformWorldToShadowCoord(pos.positionWS);

                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Choose UVs (world XZ is great for grass/terrain)
                float2 uv = (_WorldXZ > 0.5)
                    ? (IN.positionWS.xz * _UVScale)
                    : (IN.uv * _UVScale);

                // Base albedo
                float3 baseAlbedo = _BaseColor.rgb;
                if (_UseMainTex > 0.5)
                {
                    float3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                    baseAlbedo *= tex;
                }

                // Add grass detail layers (albedo modulation)
                float scrib = ScribbleMask(uv, _ScribbleScale, _ScribbleThickness, _ScribbleWarp);
                float fleck = FleckMask(uv, _FleckScale);

                // Dark scribbles, subtle flecks
                float3 detailAlbedo = baseAlbedo;
                detailAlbedo = lerp(detailAlbedo, detailAlbedo * _ScribbleColor.rgb, scrib * _ScribbleStrength);
                detailAlbedo = lerp(detailAlbedo, detailAlbedo * 0.8, fleck * _FleckStrength); // tiny dark flecks

                // Ambient base
                float3 col = detailAlbedo * _AmbientColor.rgb;

                // Main light
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 Lm = normalize(mainLight.direction);

                float ndotl_m = saturate(dot(N, Lm));
                float ramp_m = SoftRamp(ndotl_m, _RampThreshold, _RampSmoothness);

                // Use toon ramp between shadow tint and the detailed albedo
                float3 shadedBase_m = lerp(_ShadowTint.rgb, detailAlbedo, ramp_m);

                float atten_m = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                col += shadedBase_m * mainLight.color.rgb * atten_m;

                // Gentle spec (tiny)
                if (_SpecStrength > 0.0001)
                {
                    float spec_m = StylizedSpec(N, Lm, V, _SpecPower);
                    col += spec_m * _SpecStrength * mainLight.color.rgb * atten_m;
                }

                // Rim (subtle)
                if (_RimStrength > 0.0001)
                {
                    float rim = 1.0 - saturate(dot(N, V));
                    rim = pow(rim, _RimPower);
                    col += rim * _RimStrength * _RimColor.rgb;
                }

                // Additional lights (optional)
                #if defined(_ADDITIONAL_LIGHTS)
                {
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint li = 0u; li < lightCount; li++)
                    {
                        Light light = GetAdditionalLight(li, IN.positionWS);
                        float3 L = normalize(light.direction);

                        float ndotl = saturate(dot(N, L));
                        float ramp = SoftRamp(ndotl, _RampThreshold, _RampSmoothness);

                        float3 shadedBase = lerp(_ShadowTint.rgb, detailAlbedo, ramp);
                        float atten = light.distanceAttenuation * light.shadowAttenuation;

                        col += shadedBase * light.color.rgb * atten;

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
    }
}
