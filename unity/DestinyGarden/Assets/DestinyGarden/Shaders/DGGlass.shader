Shader "DestinyGarden/Glass"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
        _Visibility ("Visibility", Range(0, 1)) = 0
        _NearWall ("Near wall", Float) = 0
        _Lip ("Rolled lip", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DGAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Cull;
                float _Visibility;
                float _NearWall;
                float _Lip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float height : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                Varyings output;
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float grazing = 1.0 - saturate(abs(dot(normal, view)));
                float rim = pow(grazing, 4.0);
                float edge = pow(grazing, 12.0);
                float3 reflected = reflect(-view, normal);

                // Soft sky reflections bend with the glass and travel across it as the camera turns.
                float3 brightSky = float3(-0.59, 0.57, -0.57);
                float skyPatch = pow(saturate(dot(reflected, brightSky)), 12.0);
                float horizon = 1.0 - smoothstep(0.0, 0.11, abs(reflected.y - 0.045));
                float sun = pow(saturate(dot(reflected, _DGSkySun.xyz)), 46.0) * _DGSkySun.w;
                float moon = pow(saturate(dot(reflected, _DGSkyMoon.xyz)), 52.0) * _DGSkyMoon.w;
                float direct = sun + moon;
                float night = _DGSkyWeather.y;
                float softLight = skyPatch * lerp(1.0, 0.56, _DGSkyWeather.x);
                half3 sky = GardenSkyGradient(reflected);
                half3 glassTint = lerp(half3(0.52, 0.51, 0.69), half3(0.18, 0.23, 0.43), night);
                half3 pale = lerp(half3(0.96, 0.86, 0.92), half3(0.56, 0.64, 0.94), night);
                half3 color = lerp(sky + glassTint * 0.22, glassTint, rim * 0.78);
                // A tapered stroke follows the shoulder of the bottle like a brush highlight.
                float shoulder = smoothstep(5.7, 10.6, input.height) * (1.0 - smoothstep(13.0, 15.1, input.height));
                float strokeAngle = dot(reflected, float3(-0.67, 0.23, -0.705));
                float brush = smoothstep(0.86, 0.982, strokeAngle) * shoulder * _NearWall;
                half3 pearl = lerp(half3(1.0, 0.79, 0.64), half3(0.75, 0.72, 0.98), night);
                float lipTop = smoothstep(16.62, 17.08, input.height);
                color = lerp(color, pale, saturate(0.47 * edge + 0.55 * softLight + 0.17 * horizon + _Lip * lipTop * 0.38));
                color = lerp(color, pearl, saturate(edge * 0.35 + brush * 0.84));
                color += half3(0.71, 0.38, 0.23) * sun + half3(0.28, 0.38, 0.64) * moon;
                color += pale * (softLight * (rim * 0.15 + _Lip * lipTop * 0.24));

                // The thicker glass at the foot catches a narrow band of sea light.
                float heel = 1.0 - smoothstep(0.05, 0.43, abs(input.height - 0.22));
                color = lerp(color, glassTint * 0.72 + pale * 0.30, heel * (1.0 - edge) * 0.66);
                float wallAlpha = rim * lerp(0.25, 0.16, _NearWall) + 0.079 * edge + 0.19 * brush
                    + lerp(0.012, 0.0, _NearWall) + softLight * lerp(0.065, 0.048, _NearWall)
                    + horizon * rim * 0.058 + direct * 0.17 + heel * lerp(0.021, 0.18, _NearWall);
                float lipAlpha = (0.16 + rim * 0.36 + softLight * 0.27 + horizon * 0.11 + direct * 0.21) * lipTop;
                float nearFade = smoothstep(0.7, 3.4, distance(_WorldSpaceCameraPos, input.positionWS));
                float waterlineFade = smoothstep(-0.02, 0.06, input.height);
                return half4(color, saturate(lerp(wallAlpha, lipAlpha, _Lip) * nearFade * waterlineFade * _Visibility));
            }
            ENDHLSL
        }
    }
}
