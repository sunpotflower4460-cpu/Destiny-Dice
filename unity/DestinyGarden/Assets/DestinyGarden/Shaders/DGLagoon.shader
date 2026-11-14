Shader "DestinyGarden/Lagoon"
{
    Properties
    {
        [NoScaleOffset] _CoastMap ("Shore distance, depth, exposure", 2D) = "white" {}
        _CoastParams ("Island center, inverse map width, distance range", Vector) = (7.5, 7.5, 0.0322581, 5)
        _WaterTint ("Water tint", Vector) = (0.55, 0.75, 0.85, 0)
        _BottleWaterline ("Bottle radius and visibility", Vector) = (14.28, 0, 0, 0)
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
            #include "DGAtmosphere.hlsl"

            TEXTURE2D(_CoastMap);
            SAMPLER(sampler_CoastMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _CoastParams;
                half4 _WaterTint;
                float4 _BottleWaterline;
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
                float radius = length(p);
                float time = _DGSkyWeather.z;
                float range = distance(_WorldSpaceCameraPos, input.positionWS);
                float detailFade = 1.0 - smoothstep(22.0, 52.0, range);
                float warp = sin(dot(p, float2(0.40, -0.27)) + time * 0.31);
                float swell = dot(p, float2(1.14, 1.77)) - time * 0.86 + warp * 1.83;
                float crossSwell = dot(p, float2(-2.19, 1.72)) + time * 1.04 + sin(swell * 0.51) * 0.95;
                float ripples = dot(p, float2(5.36, -3.89)) - time * 1.52 + sin(crossSwell) * 1.40;
                float2 slope = float2(0.042, 0.066) * cos(swell)
                    + float2(-0.051, 0.036) * cos(crossSwell)
                    + float2(0.019, -0.015) * cos(ripples);
                slope *= lerp(0.14, 1.0, detailFade);
                float3 normal = normalize(float3(slope.x, 1.0, slope.y));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float2 coastUV = p * _CoastParams.z + 0.5;
                float3 coast = SAMPLE_TEXTURE2D(_CoastMap, sampler_CoastMap, coastUV).rgb;
                float shore = (coast.r - 0.5) * _CoastParams.w;
                float exposed = coast.b;
                float depth = lerp(coast.g, 1.0, smoothstep(13.0, 16.0, radius));
                float shoal = (1.0 - smoothstep(0.0, 0.78, depth)) * lerp(0.92, 0.46, exposed);
                float seabed = (1.0 - smoothstep(0.0, 0.23, depth)) * (1.0 - exposed * 0.91);
                half3 tint = 0.53 + _WaterTint.rgb * 0.74;
                half3 deep = half3(0.030, 0.037, 0.081);
                half3 shoalColor = half3(0.041, 0.16, 0.17);
                half3 sand = half3(0.22, 0.24, 0.18);
                half3 water = lerp(lerp(deep, shoalColor, shoal), sand, seabed * 0.78) * tint;
                // Crossing ripples focus light into short crests over the sandy shelf.
                float refracted = smoothstep(0.43, 0.92, sin(crossSwell * 1.8) * sin(ripples * 0.80));
                water *= 1.0 + (sin(swell) * 0.042 + sin(crossSwell) * 0.037
                    + refracted * seabed * 0.34) * detailFade;

                float fresnel = pow(1.0 - saturate(dot(normal, view)), 4.2);
                float3 reflected = reflect(-view, normal);
                half3 reflectedSky = GardenSkyGradient(reflected);
                // The reflected cloud banks are softer where the surface is seen from above.
                float3 cloudRay = reflect(-view, normalize(float3(slope.x * 0.32, 1.0, slope.y * 0.32)));
                cloudRay.y = lerp(0.018, 0.30, saturate(cloudRay.y));
                half paint = _DGPaintedSkyWeight * lerp(0.65, 0.35, _DGSkyWeather.w);
                reflectedSky = lerp(reflectedSky, GardenPaintedClouds(cloudRay), paint);
                water = lerp(water * lerp(1.0, 0.67, _DGSkyWeather.y), reflectedSky, 0.15 + fresnel * 0.39);
                float broken = smoothstep(-0.52, 0.68, sin(swell) * 0.65 + sin(ripples) * 0.35);
                float solar = pow(saturate(dot(reflected, _DGSkySun.xyz)), 25.0) * _DGSkyWarmth.w;
                float lunar = pow(saturate(dot(reflected, _DGSkyMoon.xyz)), 42.0) * _DGSkyMoon.w;
                water += (half3(0.31, 0.135, 0.073) * solar + half3(0.22, 0.16, 0.21) * lunar)
                    * (0.34 + broken * 0.66) * lerp(0.52, 1.0, fresnel);
                float highlight = saturate(dot(normal, SafeNormalize(sun.direction + view)));
                float gloss = pow(highlight, 64.0) * 0.035 + pow(highlight, 156.0) * 0.073;
                water *= lerp(0.65, 1.0, sun.shadowAttenuation);
                water += sun.color * sun.shadowAttenuation * detailFade * gloss;
                water = lerp(water, reflectedSky * 0.45, smoothstep(36.0, 68.0, range) * 0.28);

                float runup = sin(swell * 1.16 + warp) * (0.065 + 0.035 * exposed);
                float wash = 1.0 - smoothstep(0.025, 0.108, abs(shore - 0.07 - runup));
                float crest = 1.0 - smoothstep(0.024, 0.096, abs(shore - lerp(0.57, 0.31, exposed) - runup * 1.6));
                float breaks = smoothstep(-0.56, 0.64, sin(crossSwell * 1.5) + sin(ripples) * 0.34);
                float lace = smoothstep(-0.69, 0.54, sin(ripples * 1.36 + swell) * 0.44 + sin(crossSwell));
                half foam = (wash * lerp(0.26, 0.48, exposed) * (0.34 + lace * 0.66) + crest * breaks * 0.20) * detailFade;
                water = lerp(water, half3(0.77, 0.78, 0.72) * (0.36 + _WaterTint.rgb * 0.68), foam);

                // The submerged heel has a soft contact shadow and picks up light on nearby ripples.
                float foot = radius - _BottleWaterline.x;
                float contact = 1.0 - smoothstep(0.02, 0.37, abs(foot + 0.08));
                float glint = 1.0 - smoothstep(0.04, 0.56, abs(foot - 0.26 - slope.x * 2.3));
                water *= 1.0 - contact * _BottleWaterline.y * 0.19;
                water += (reflectedSky * 0.13 + half3(0.031, 0.027, 0.049))
                    * glint * (0.28 + broken * 0.72) * _BottleWaterline.y;
                // The outer sea fades to the same horizontal sky seen through the dome.
                float viewDepth = -TransformWorldToView(input.positionWS).z;
                half fog = ComputeFogFactorZ0ToFar(max(viewDepth - _ProjectionParams.y, 0.0));
                float3 toward = -view;
                toward.y = 0.0;
                half3 horizon = GardenSkyGradient(SafeNormalize(toward));
                half3 fogged = MixFogColor(water, horizon, fog);
                return half4(lerp(fogged, horizon, smoothstep(41.0, 76.0, radius)), 1);
            }
            ENDHLSL
        }
    }
}
