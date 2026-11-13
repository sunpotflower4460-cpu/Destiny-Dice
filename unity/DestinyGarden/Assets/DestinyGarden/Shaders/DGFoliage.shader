// Destiny Garden — living foliage.
// Vertex colours carry the albedo (rgb) and how much a vertex may sway (a: 0 root, 1 tip).
// TEXCOORD1 carries the instance root (xyz, world space) and the vitality threshold (w):
// an instance grows out of its root as the global _DGVitality rises past its threshold,
// so the whole island fills in from the saved state of the garden.
Shader "DestinyGarden/Foliage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _WindStrength ("Wind Strength", Float) = 0.07
        _WindSpeed ("Wind Speed", Float) = 1.25
        _WindScale ("Wind Scale", Float) = 0.32
        _Translucency ("Translucency", Float) = 0.45
        _CanopyLighting ("Canopy Lighting", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _Translucency;
                half _CanopyLighting;
            CBUFFER_END

            float _DGVitality;      // 0..1, set from the saved garden state
            half4 _DGAmbient;       // ambient light colour (set from C#)
            float3 _DGWindDirection;
            float _DGWindMotion;
            float _DGWindGust;      // 0..1 extra wind during a wind blessing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 root : TEXCOORD1;
            };

            float3 FoliagePositionWS(Attributes v)
            {
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float grow = saturate((_DGVitality - v.root.w) * 6.0);
                ws = lerp(v.root.xyz, ws, grow);
                float t = _Time.y * _WindSpeed;
                float phase = dot(ws.xz, _DGWindDirection.xz) * _WindScale;
                float across = dot(ws.xz, float2(-_DGWindDirection.z, _DGWindDirection.x));
                float wave = sin(t + phase + 0.34 * sin(across * 0.42)) + 0.28 * sin(t * 1.9 + phase * 2.1);
                float strength = _WindStrength * (1.0 + _DGWindGust * 2.5);
                return ws + _DGWindDirection * (wave * strength * v.color.a * grow * _DGWindMotion);
            }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD0;
                half fog : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = FoliagePositionWS(v);
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.positionWS = ws;
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);
                Light mainLight = GetMainLight();
                half ndl = dot(n, mainLight.direction);
                half wrap = saturate(ndl * 0.5 + 0.5);
                half trans = saturate(-ndl) * _Translucency;
                half3 albedo = i.color.rgb * _BaseColor.rgb;
                half3 col = albedo * (_DGAmbient.rgb * 0.9 + mainLight.color * (wrap * 0.30 + trans * 0.22));
                if (_CanopyLighting > 0.5)
                {
                    // Wood mostly receives direct light. Thin leaves also transmit some light.
                    half leaf = smoothstep(0.17, 0.40, i.color.a);
                    half sky = 0.54 + 0.46 * saturate(n.y * 0.5 + 0.5);
                    half shade = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1, 1, 1, 1)).shadowAttenuation;
                    half diffuse = saturate((ndl + 0.22) / 1.22);
                    half through = trans * leaf * 0.44;
                    half3 light = _DGAmbient.rgb * (sky * 0.77) +
                        mainLight.color * (diffuse * 0.44 + through) * lerp(0.28, 1.0, shade);
                    col = albedo * light;
                }
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVertex(Attributes v) : SV_POSITION
            {
                float3 ws = FoliagePositionWS(v);
                float3 normal = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 light = normalize(_LightPosition - ws);
                #else
                    float3 light = _LightDirection;
                #endif
                // Apply bias on the side facing the light for two-sided leaves.
                normal *= dot(normal, light) < 0.0 ? -1.0 : 1.0;
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(ws, normal, light)));
            }

            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
