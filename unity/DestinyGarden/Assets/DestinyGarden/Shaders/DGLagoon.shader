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
                // A remote vertex may sit beyond the camera's far plane. Keep the
                // plane continuous to the horizon and leave near water at its true depth.
                #if UNITY_REVERSED_Z
                    output.positionCS.z = max(output.positionCS.z, 0.00001 * output.positionCS.w);
                #else
                    output.positionCS.z = min(output.positionCS.z, 0.99999 * output.positionCS.w);
                #endif
                output.positionWS = position.positionWS;
                return output;
            }

            float WaveSlope(float phase)
            {
                // Attenuate wavelengths that would be smaller than a pixel.
                return cos(phase) * (1.0 - smoothstep(0.85, 3.0, fwidth(phase)));
            }

            float LightTrail(float3 ray, float3 light, float width, float length)
            {
                float across = ray.x * light.z - ray.z * light.x;
                float above = ray.y - light.y;
                float front = smoothstep(0.0, 0.78, dot(ray, light));
                return front * exp2(-across * across * width - above * above * length);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz - _CoastParams.xy;
                float radius = length(p);
                float time = _DGSkyWeather.z;
                float range = distance(_WorldSpaceCameraPos, input.positionWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 horizon = GardenHorizon(-view);
                float mist = 1.0 - smoothstep(0.035, 0.19, view.y);
                mist = max(mist, smoothstep(158.0, 247.0, range));
                float detailFade = 1.0 - smoothstep(28.0, 76.0, range);
                float warp = sin(dot(p, float2(0.16, -0.23)) + time * 0.24);
                float swell = dot(p, float2(0.32, 1.16)) - time * 0.69 + warp * 1.2;
                float crossSwell = dot(p, float2(-0.77, 1.91)) + time * 0.91 + sin(swell * 0.68) * 0.89;
                float ripples = dot(p, float2(2.70, 4.32)) - time * 1.46 + sin(crossSwell * 0.77) * 1.22;
                float swellSlope = WaveSlope(swell);
                float crossSlope = WaveSlope(crossSwell);
                float rippleSlope = WaveSlope(ripples);
                float2 slope = float2(0.021, 0.074) * swellSlope
                    + float2(-0.027, 0.036) * crossSlope
                    + float2(0.010, 0.014) * rippleSlope * detailFade;
                slope *= lerp(0.33, 1.0, detailFade) * lerp(1.0, 1.24, _DGSkyWeather.w);
                float3 normal = normalize(float3(slope.x, 1.0, slope.y));

                float2 coastUV = p * _CoastParams.z + 0.5;
                float3 coast = SAMPLE_TEXTURE2D(_CoastMap, sampler_CoastMap, coastUV).rgb;
                float shore = (coast.r - 0.5) * _CoastParams.w;
                float exposed = coast.b;
                float depth = lerp(coast.g, 1.0, smoothstep(13.0, 16.0, radius));
                float shoal = (1.0 - smoothstep(0.0, 0.78, depth)) * lerp(0.92, 0.46, exposed);
                float seabed = (1.0 - smoothstep(0.0, 0.23, depth)) * (1.0 - exposed * 0.91);
                half3 tint = 0.53 + _WaterTint.rgb * 0.74;
                half3 deep = lerp(half3(0.027, 0.046, 0.104), half3(0.009, 0.017, 0.049), _DGSkyWeather.y);
                half3 shoalColor = half3(0.032, 0.19, 0.185);
                half3 sand = half3(0.21, 0.28, 0.205);
                half3 water = lerp(lerp(deep, shoalColor, shoal), sand, seabed * 0.78) * tint;
                // Crossing ripples focus light into short crests over the sandy shelf.
                float refracted = smoothstep(0.43, 0.92, sin(crossSwell * 1.8) * sin(ripples * 0.80));
                water *= 1.0 + (swellSlope * 0.060 + crossSlope * 0.047) * lerp(0.44, 1.0, detailFade)
                    + refracted * seabed * detailFade * 0.28;

                float fresnel = pow(1.0 - saturate(dot(normal, view)), 3.4);
                float3 reflected = reflect(-view, normal);
                float3 calm = float3(-view.x, view.y, -view.z);
                float3 cloudRay = normalize(lerp(calm, reflected, 0.30));
                half3 reflectedSky = lerp(GardenSkyGradient(cloudRay), GardenPaintedClouds(cloudRay),
                    _DGPaintedSkyWeight * lerp(0.93, 0.57, _DGSkyWeather.w));
                water = lerp(water, reflectedSky, (0.085 + fresnel * 0.46) * (1.0 - seabed * 0.42));

                // The light paths are elongated by the wave faces and broken up into short brush strokes.
                float crestLight = smoothstep(-0.19, 0.87, swellSlope * 0.65 + crossSlope * 0.35);
                float sunPath = LightTrail(reflected, _DGSkySun.xyz, 42.0, 8.0) * _DGSkySun.w;
                float moonPath = LightTrail(reflected, _DGSkyMoon.xyz, 96.0, 12.0) * _DGSkyMoon.w;
                water += (half3(0.35, 0.128, 0.057) * sunPath + half3(0.22, 0.22, 0.39) * moonPath)
                    * (0.065 + crestLight * 0.32) * (0.24 + fresnel * 0.76);

                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float highlight = saturate(dot(normal, SafeNormalize(sun.direction + view)));
                float gloss = pow(highlight, 64.0) * 0.035 + pow(highlight, 156.0) * 0.073;
                water *= lerp(0.65, 1.0, sun.shadowAttenuation);
                water += sun.color * sun.shadowAttenuation * detailFade * gloss;

                float runup = sin(swell * 1.16 + warp) * (0.065 + 0.035 * exposed);
                float wash = 1.0 - smoothstep(0.025, 0.108, abs(shore - 0.07 - runup));
                float crest = 1.0 - smoothstep(0.024, 0.096, abs(shore - lerp(0.57, 0.31, exposed) - runup * 1.6));
                float breaks = smoothstep(-0.56, 0.64, sin(crossSwell * 1.5) + sin(ripples) * 0.34);
                float lace = smoothstep(-0.69, 0.54, sin(ripples * 1.36 + swell) * 0.44 + sin(crossSwell));
                half foam = (wash * lerp(0.25, 0.47, exposed) * (0.42 + lace * 0.58) + crest * breaks * 0.24) * detailFade;
                half3 foamColor = lerp(half3(0.82, 0.82, 0.70), half3(0.29, 0.44, 0.65), _DGSkyWeather.y);
                water = lerp(water, foamColor * (0.51 + _WaterTint.rgb * 0.53), foam);

                // The submerged heel has a soft contact shadow and picks up light on nearby ripples.
                float foot = radius - _BottleWaterline.x;
                float contact = 1.0 - smoothstep(0.05, 0.46, abs(foot + 0.12));
                float wake = 1.0 - smoothstep(0.04, 0.56, abs(foot - 0.32 - slope.x * 2.7));
                water *= 1.0 - contact * _BottleWaterline.y * 0.27;
                water += (reflectedSky * 0.28 + half3(0.040, 0.064, 0.092))
                    * wake * (0.26 + crestLight * 0.68) * _BottleWaterline.y;
                return half4(lerp(water, horizon, mist), 1);
            }
            ENDHLSL
        }
    }
}
