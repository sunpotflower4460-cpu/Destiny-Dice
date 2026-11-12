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
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _Translucency;
            CBUFFER_END

            float _DGVitality;      // 0..1, set from the saved garden state
            half4 _DGAmbient;       // ambient light colour (set from C#)
            float _DGWindGust;      // 0..1 extra wind during a wind blessing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float4 root : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 normalWS : TEXCOORD0;
                half fog : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float grow = saturate((_DGVitality - v.root.w) * 6.0);
                ws = lerp(v.root.xyz, ws, grow);
                float t = _Time.y * _WindSpeed;
                float phase = ws.x * _WindScale + ws.z * _WindScale * 0.7;
                float wave = sin(t + phase) + 0.45 * sin(t * 1.9 + ws.z * _WindScale * 2.1);
                float strength = _WindStrength * (1.0 + _DGWindGust * 2.5);
                float sway = v.color.a;
                ws.x += wave * strength * sway;
                ws.z += wave * strength * 0.55 * sway;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
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
                // Same brightness range as the scene's Lit materials (sun intensity ~2.4).
                half3 col = albedo * (_DGAmbient.rgb * 0.9 + mainLight.color * (wrap * 0.30 + trans * 0.22));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
