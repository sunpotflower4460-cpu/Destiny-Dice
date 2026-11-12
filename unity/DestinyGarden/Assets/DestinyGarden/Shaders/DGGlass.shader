// Destiny Garden — the thin glass of the bottle: almost invisible face-on,
// bright only where the glass turns away from the eye (fresnel), with a soft band of sky.
Shader "DestinyGarden/Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.78, 0.93, 1, 1)
        _BaseAlpha ("Base Alpha", Float) = 0.0
        _RimPower ("Rim Power", Float) = 4.5
        _RimStrength ("Rim Strength", Float) = 0.32
        _SpecStrength ("Spec Strength", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // Only the far wall: the camera stands just outside the glass, and the near wall seen at a
        // grazing angle would frost the whole view.
        Cull Front
        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _BaseAlpha;
                float _RimPower;
                float _RimStrength;
                float _SpecStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float facing = saturate(abs(dot(n, v)));
                float rim = pow(1.0 - facing, _RimPower);
                Light mainLight = GetMainLight();
                float3 r = reflect(-v, n);
                float spec = pow(saturate(dot(r, mainLight.direction)), 48.0) * _SpecStrength;
                // Two soft vertical bands of reflected sky along the bottle.
                float band = smoothstep(0.035, 0.0, abs(frac(i.uv.x * 2.0 + 0.13) - 0.5) - 0.44);
                float a = _BaseAlpha + rim * _RimStrength + spec + band * 0.035;
                half3 col = lerp(_Tint.rgb, half3(1, 1, 1), saturate(spec + band * 0.5));
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
}
