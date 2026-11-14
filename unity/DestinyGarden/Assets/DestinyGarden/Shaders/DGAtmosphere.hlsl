#ifndef DESTINY_GARDEN_ATMOSPHERE_INCLUDED
#define DESTINY_GARDEN_ATMOSPHERE_INCLUDED

float4 _DGSkyHorizon;
float4 _DGSkyZenith;
float4 _DGSkyWarmth;
float4 _DGSkySun;
float4 _DGSkyMoon;
float4 _DGSkyWeather; // cloud cover, darkness, drift time

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

#endif
