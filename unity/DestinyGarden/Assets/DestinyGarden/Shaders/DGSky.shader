Shader "DestinyGarden/Sky"
{
    Properties
    {
        [NoScaleOffset] _CloudNoise ("Cloud noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" "RenderPipeline"="UniversalPipeline" }
        Cull Front
        ZWrite Off
        Pass
        {
            Name "Sky"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DGAtmosphere.hlsl"

            TEXTURE2D(_CloudNoise);
            SAMPLER(sampler_CloudNoise);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ray : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 position = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(position);
                o.ray = position - _WorldSpaceCameraPos;
                return o;
            }

            half3 Clouds(float3 ray, half3 sky, out half veil)
            {
                // Project the upper hemisphere onto a distant cloud layer. Distance
                // compression keeps the banks thin and soft near the horizon.
                float altitude = max(ray.y + 0.20, 0.20);
                float2 plane = ray.xz / altitude;
                float2 drift = _DGSkyWeather.z * float2(0.0017, -0.0008);
                float2 broad = plane * float2(0.15, 0.31) + drift + float2(0.19, 0.31);
                float2 detail = plane * float2(0.34, 0.67) + drift * 1.6 + float2(0.61, 0.17);
                half billow = SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, broad).r;
                half wisps = SAMPLE_TEXTURE2D(_CloudNoise, sampler_CloudNoise, detail).r;
                half cover = _DGSkyWeather.x;
                half night = _DGSkyWeather.y;
                half density = billow * 0.73 + wisps * 0.27;
                half edge = lerp(0.53, 0.39, cover);
                half mass = smoothstep(edge, edge + 0.18, density);
                half higher = smoothstep(0.002, 0.073, ray.y);
                half streak = smoothstep(0.49, 0.76, wisps) * (1.0 - mass) * 0.19;
                half light = pow(saturate(dot(ray, _DGSkySun.xyz)), 6.0) * _DGSkySun.w;
                half silver = pow(saturate(dot(ray, _DGSkyMoon.xyz)), 7.0) * _DGSkyMoon.w;
                half3 cloud = lerp(half3(0.22, 0.19, 0.32), half3(0.022, 0.028, 0.069), night);
                half edges = smoothstep(edge + 0.08, edge + 0.28, density);
                cloud = lerp(cloud, half3(0.70, 0.73, 0.77), edges * (0.66 - night * 0.64));
                cloud = lerp(cloud, half3(0.88, 0.43, 0.24), (0.32 + 0.48 * light) * _DGSkyWarmth.w * (0.44 + edges * 0.56));
                cloud += half3(0.050, 0.075, 0.14) * silver;
                veil = higher * saturate(mass * (0.19 + cover * 0.61) + streak);
                return lerp(sky, cloud, veil);
            }

            half3 Sun(float3 ray)
            {
                float distance = length(cross(ray, _DGSkySun.xyz));
                float visibility = step(0.0, dot(ray, _DGSkySun.xyz)) * _DGSkySun.w;
                float core = 1.0 - smoothstep(0.011, 0.019, distance);
                float halo = exp2(-24.0 * distance) * 0.25 + exp2(-7.2 * distance) * 0.071;
                return visibility * (half3(1.39, 0.94, 0.55) * core + half3(0.61, 0.26, 0.13) * halo);
            }

            half3 Moon(float3 ray)
            {
                float3 right = normalize(cross(float3(0, 1, 0), _DGSkyMoon.xyz));
                float3 up = cross(_DGSkyMoon.xyz, right);
                float2 disc = float2(dot(ray, right), dot(ray, up)) / 0.036;
                float r = length(disc);
                float front = step(0.0, dot(ray, _DGSkyMoon.xyz)) * _DGSkyMoon.w;
                float darkEdge = length(disc - float2(0.40, 0.26));
                float feather = max(0.014, 0.6 * fwidth(r));
                half body = 1.0 - smoothstep(1.0 - feather, 1.0 + feather, r);
                half crescent = body * smoothstep(0.86 - feather, 0.86 + feather, darkEdge);
                half shade = lerp(0.68, 1.05, saturate(disc.x * -0.48 + disc.y * 0.40 + 0.54));
                float2 mare = disc + float2(0.42, 0.22);
                shade -= 0.075 * exp2(-9.0 * dot(mare, mare));
                half halo = exp2(-1.65 * r) * 0.16 + exp2(-0.35 * r) * 0.038;
                half3 gold = lerp(half3(1.12, 0.85, 0.56), half3(1.21, 1.07, 0.83), _DGSkyWeather.y);
                return front * (gold * crescent * shade + half3(0.42, 0.37, 0.64) * halo);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.ray);
                half3 sky = GardenSkyGradient(ray);
                half paint = _DGPaintedSkyWeight * (1.0 - smoothstep(0.38, 0.74, ray.y));
                sky = lerp(sky, GardenPaintedClouds(ray), paint * lerp(0.97, 0.80, _DGSkyWeather.w));
                half veil;
                sky = Clouds(ray, sky, veil);
                sky += (Sun(ray) + Moon(ray)) * (1.0 - veil * 0.78);
                // Far water uses the same color all the way down to the painted horizon.
                return half4(lerp(GardenHorizon(ray), sky, smoothstep(-0.005, 0.066, ray.y)), 1.0);
            }
            ENDHLSL
        }
    }
}
