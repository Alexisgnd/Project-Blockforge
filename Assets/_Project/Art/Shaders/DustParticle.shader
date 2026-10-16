// =========================================================
// PARTICULE DOUCE (HDRP) - poussiere des roues (RobotGroundFX),
// impacts et etincelles des armes (WeaponFireController),
// redressement du robot (RobotRecovery)
// Shader non eclaire, fondu alpha ou additif (_SrcBlend /
// _DstBlend + _Additive), ecrit a la main : passe
// "SRPDefaultUnlit", que HDRP dessine avec les transparents.
// Un disque doux calcule dans le shader (pas de texture).
// Couleur et alpha : _Tint du materiau (un materiau par regime
// de poussiere) ; la couleur par particule n'arrive pas dans un
// shader ecrit a la main sous HDRP (elle est nulle), on ne s'en
// sert pas. Le fondu vient de l'age de la particule, que le
// ParticleSystem transmet dans TEXCOORD0.z (flux de sommets
// AgePercent, voir RobotGroundFX) : apparition rapide puis
// disparition progressive.
// Couleurs ecrites telles quelles dans le tampon HDRP (deja
// expose) : un gris 0,7 reste un gris clair quelle que soit
// l'exposition.
// =========================================================
Shader "Blockforge/DustParticle"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge softness", Range(0.05, 1)) = 0.75
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
        _Additive ("Additive (rgb x alpha)", Float) = 0
        _FadeIn ("Fade in (part of life)", Range(0.01, 1)) = 0.12
        _FadeOut ("Fade out start (part of life)", Range(0, 0.99)) = 0.45
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Dust"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch glcore

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float4 _Tint;
            float _Softness;
            float _Additive;
            float _FadeIn;
            float _FadeOut;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;    // xy : uv, z : age (0..1)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(positionRWS);
                o.uv = input.uv;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 d = input.uv.xy * 2.0 - 1.0;
                float r = length(d);
                float a = smoothstep(1.0, 1.0 - _Softness, r);
                a *= a;
                float age = saturate(input.uv.z);
                float fade = smoothstep(0.0, _FadeIn, age) * (1.0 - smoothstep(_FadeOut, 1.0, age));
                float k = _Tint.a * a * fade;
                if (_Additive > 0.5)
                    return float4(_Tint.rgb * k, 1.0);   // Blend One One : la couleur porte l'alpha
                return float4(_Tint.rgb, k);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
