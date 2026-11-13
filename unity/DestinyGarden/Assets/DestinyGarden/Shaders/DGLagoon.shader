Shader "DestinyGarden/Lagoon"
{
    Properties
    {
        [NoScaleOffset] _CoastMap ("Shore distance, depth, exposure", 2D) = "white" {}
        _CoastParams ("Island center, inverse map width, distance range", Vector) = (7.5, 7.5, 0.0322581, 5)
        _WaterTint ("Water tint", Vector) = (0.55, 0.75, 0.85, 0)
        _SkyTint ("Sky tint", Vector) = (0.30, 0.40, 0.52, 0)
        _WaterTime ("Water time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Lagoon"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CoastMap);
            SAMPLER(sampler_CoastMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _CoastParams;
                half4 _WaterTint;
                half4 _SkyTint;
                float _WaterTime;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                Varyings output;
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz - _CoastParams.xy;
                float time = _WaterTime;
                float warp = sin(dot(p, float2(0.40, -0.27)) + time * 0.31);
                float swell = dot(p, float2(1.53, 2.18)) - time * 0.93 + warp * 1.25;
                float crossSwell = dot(p, float2(-2.83, 1.43)) + time * 1.12 + sin(swell * 0.47) * 0.70;
                float ripples = dot(p, float2(6.44, -4.83)) - time * 1.75 + sin(crossSwell) * 1.35;
                float2 slope = float2(0.056, 0.079) * cos(swell)
                    + float2(-0.070, 0.045) * cos(crossSwell)
                    + float2(0.028, -0.023) * cos(ripples);
                float3 normal = normalize(float3(slope.x, 1.0, slope.y));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float2 coastUV = p * _CoastParams.z + 0.5;
                float3 coast = SAMPLE_TEXTURE2D(_CoastMap, sampler_CoastMap, coastUV).rgb;
                float shore = (coast.r - 0.5) * _CoastParams.w;
                float exposed = coast.b;
                float depth = lerp(coast.g, 1.0, smoothstep(13.0, 16.0, length(p)));
                float shoal = (1.0 - smoothstep(0.0, 0.78, depth)) * lerp(0.92, 0.46, exposed);
                float seabed = (1.0 - smoothstep(0.0, 0.23, depth)) * (1.0 - exposed * 0.91);
                half3 tint = 0.53 + _WaterTint.rgb * 0.74;
                half3 deep = half3(0.015, 0.055, 0.086);
                half3 shoalColor = half3(0.034, 0.16, 0.18);
                half3 sand = half3(0.17, 0.25, 0.20);
                float detailFade = 1.0 - smoothstep(24.0, 54.0, distance(_WorldSpaceCameraPos, input.positionWS));
                half3 water = lerp(lerp(deep, shoalColor, shoal), sand, seabed * 0.78) * tint;
                // Crossing ripples focus light into short crests over the sandy shelf.
                float refracted = smoothstep(0.43, 0.92, sin(crossSwell * 1.8) * sin(ripples * 0.80));
                water *= 1.0 + (sin(swell) * 0.042 + sin(crossSwell) * 0.037
                    + refracted * seabed * 0.34) * detailFade;

                float fresnel = pow(1.0 - saturate(dot(normal, view)), 3.0);
                water = lerp(water, _SkyTint.rgb * 0.43, 0.04 + fresnel * 0.65);
                float highlight = saturate(dot(normal, SafeNormalize(sun.direction + view)));
                float gloss = pow(highlight, 70.0) * 0.057 + pow(highlight, 172.0) * 0.17;
                water *= lerp(0.65, 1.0, sun.shadowAttenuation);
                water += sun.color * sun.shadowAttenuation * detailFade * gloss;

                float runup = sin(swell * 1.16 + warp) * (0.065 + 0.035 * exposed);
                float wash = 1.0 - smoothstep(0.025, 0.108, abs(shore - 0.07 - runup));
                float crest = 1.0 - smoothstep(0.024, 0.096, abs(shore - lerp(0.57, 0.31, exposed) - runup * 1.6));
                float breaks = smoothstep(-0.56, 0.64, sin(crossSwell * 1.5) + sin(ripples) * 0.34);
                float lace = smoothstep(-0.69, 0.54, sin(ripples * 1.36 + swell) * 0.44 + sin(crossSwell));
                half foam = (wash * lerp(0.26, 0.48, exposed) * (0.34 + lace * 0.66) + crest * breaks * 0.20) * detailFade;
                water = lerp(water, half3(0.63, 0.79, 0.75) * (0.36 + _WaterTint.rgb * 0.68), foam);
                // The sky and the sea share the scene fog as they approach the horizon.
                float viewDepth = -TransformWorldToView(input.positionWS).z;
                half fog = ComputeFogFactorZ0ToFar(max(viewDepth - _ProjectionParams.y, 0.0));
                return half4(MixFog(water, fog), 1);
            }
            ENDHLSL
        }
    }
}
