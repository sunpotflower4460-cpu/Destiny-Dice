#ifndef DESTINY_GARDEN_ATMOSPHERE_INCLUDED
#define DESTINY_GARDEN_ATMOSPHERE_INCLUDED

float4 _DGSkyHorizon;
float4 _DGSkyZenith;
float4 _DGSkyWarmth;
float4 _DGSkySun;
float4 _DGSkyMoon;
float4 _DGSkyWeather; // cloud cover, darkness, drift time, moisture

TEXTURE2D(_DGPaintedSky);
SAMPLER(sampler_DGPaintedSky);
half _DGPaintedSkyWeight;

// The dome and the lagoon evaluate the same light along different world-space rays.
half3 GardenSkyGradient(float3 ray)
{
    float height = smoothstep(-0.025, 0.33, ray.y);
    float dusk = _DGSkyWarmth.w * pow(saturate(dot(ray, _DGSkySun.xyz)), 4.0);
    float moonlight = _DGSkyMoon.w * pow(saturate(dot(ray, _DGSkyMoon.xyz)), 12.0);
    return lerp(_DGSkyHorizon.rgb, _DGSkyZenith.rgb, height)
        + _DGSkyWarmth.rgb * dusk * (1.0 - height * 0.89)
        + half3(0.011, 0.021, 0.045) * moonlight;
}

half3 GardenPaintedClouds(float3 ray)
{
    // The panorama reverses smoothly at the sides. More of the painting is
    // visible at low elevations, where the sky meets the sea.
    float u = 0.5 + 0.47 * sin(1.47 * ray.x);
    float v = 0.052 + 0.89 * (1.0 - exp2(-12.0 * max(0.0, ray.y)));
    half3 paint = SAMPLE_TEXTURE2D(_DGPaintedSky, sampler_DGPaintedSky, float2(u, v)).rgb;
    half luma = dot(paint, half3(0.2126, 0.7152, 0.0722));
    half3 daylight = paint * 1.12 + _DGSkyHorizon.rgb * 0.13;
    daylight = lerp(daylight, luma * half3(0.71, 0.83, 1.03), _DGSkyWeather.w * 0.73);
    half3 moonlit = paint * half3(0.12, 0.16, 0.30) + half3(0.009, 0.011, 0.025);
    return lerp(daylight, moonlit, _DGSkyWeather.y);
}

#endif
