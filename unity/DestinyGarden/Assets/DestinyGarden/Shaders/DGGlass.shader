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
                float rim = pow(grazing, 3.9);
                float edge = pow(grazing, 14.0);
                float3 reflected = reflect(-view, normal);

                float horizon = 1.0 - smoothstep(0.015, 0.13, abs(reflected.y - 0.028));
                float sun = pow(saturate(dot(reflected, _DGSkySun.xyz)), 46.0) * _DGSkySun.w;
                float moon = pow(saturate(dot(reflected, _DGSkyMoon.xyz)), 52.0) * _DGSkyMoon.w;
                float night = _DGSkyWeather.y;
                half3 sky = lerp(GardenSkyGradient(reflected), GardenPaintedClouds(reflected),
                    _DGPaintedSkyWeight * smoothstep(-0.035, 0.21, reflected.y) * 0.83);
                half3 pearl = lerp(half3(1.08, 0.87, 0.72), half3(0.68, 0.76, 1.13), night);
                half3 inner = lerp(half3(0.22, 0.29, 0.45), half3(0.079, 0.13, 0.29), night);

                // Two unequal reflections taper over the blown shoulder and follow the orbit.
                float shoulder = smoothstep(5.2, 9.7, input.height) * (1.0 - smoothstep(13.6, 15.7, input.height));
                float stroke = smoothstep(0.80, 0.985, dot(reflected, float3(-0.75, 0.62, -0.23)));
                float sideLight = smoothstep(0.83, 0.988, dot(reflected, float3(0.74, 0.63, -0.24)));
                float brush = (stroke + sideLight * 0.34) * shoulder * _NearWall
                    * lerp(1.0, 0.64, _DGSkyWeather.w);
                float lowerLip = smoothstep(16.62, 16.87, input.height);
                float lipTop = smoothstep(16.89, 17.17, input.height);
                float innerRoll = (lowerLip - lipTop) * _Lip;
                float heel = 1.0 - smoothstep(0.09, 0.48, abs(input.height - 0.30));
                float heelEdge = 1.0 - smoothstep(0.035, 0.21, abs(input.height - 0.70));
                half3 color = lerp(inner, sky * 0.86 + pearl * 0.19, saturate(rim * 0.55 + brush + horizon * rim * 0.40));
                color = lerp(color, pearl, saturate(edge * 0.55 + brush * 0.72 + heelEdge * 0.54 + _Lip * lipTop * 0.79));
                color = lerp(color, inner, innerRoll * 0.62 + heel * 0.24);
                color += half3(0.62, 0.34, 0.21) * sun + half3(0.25, 0.37, 0.68) * moon;
                float wallAlpha = rim * lerp(0.19, 0.14, _NearWall) + 0.16 * edge + 0.30 * brush
                    + horizon * rim * 0.10 + (sun + moon) * 0.14
                    + (heel * 0.12 + heelEdge * 0.26) * lerp(0.30, 1.0, _NearWall);
                float lipAlpha = 0.26 * lowerLip + 0.23 * lipTop + 0.28 * rim + 0.18 * (sun + moon);
                float nearFade = smoothstep(0.7, 3.4, distance(_WorldSpaceCameraPos, input.positionWS));
                float waterlineFade = smoothstep(-0.02, 0.06, input.height);
                return half4(color, saturate(lerp(wallAlpha, lipAlpha, _Lip) * nearFade * waterlineFade * _Visibility));
            }
            ENDHLSL
        }
    }
}
