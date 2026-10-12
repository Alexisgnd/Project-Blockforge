// =========================================================
// HOLOGRAMME (HDRP) - bloc selectionne sur la pince
// Shader non eclaire, additif, ecrit a la main : passe
// "SRPDefaultUnlit", que HDRP dessine avec les transparents.
// - Remplissage leger + contour de Fresnel (les aretes brillent)
// - Lignes de balayage fixes sur l'hologramme (le long de
//   _HoloUp), qui defilent lentement vers le haut
// - Scintillement discret, et apparition par balayage du bas
//   vers le haut (_Reveal 0 -> 1) avec une bande lumineuse
// - _Beam = 1 : cone de projection (degrade depuis la lentille,
//   bords doux) au lieu d'un objet
// - _Beam = 2 : aretes vives du bloc (maillage de lignes),
//   couleur pleine et lumineuse
// Les couleurs sont ecrites telles quelles dans le tampon HDRP
// (deja expose) : l'intensite ne depend pas de l'exposition.
// =========================================================
Shader "Blockforge/Hologram"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.2, 0.85, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _FillAlpha ("Fill", Range(0, 1)) = 0.16
        _RimPower ("Rim power", Float) = 2.2
        _RimStrength ("Rim strength", Float) = 1.1
        _LineDensity ("Scanlines per meter (world)", Float) = 900
        _LineStrength ("Scanline strength", Range(0, 1)) = 0.35
        _LineSpeed ("Scanline speed (lines/s)", Float) = 1.5
        _Reveal ("Reveal", Range(0, 1)) = 1
        _Flicker ("Flicker", Range(0, 1)) = 0.08
        _Beam ("Mode (0 objet, 1 cone, 2 aretes)", Float) = 0
        _HoloCenter ("Center (world)", Vector) = (0, 0, 0, 0)
        _HoloUp ("Up (world)", Vector) = (0, 1, 0, 0)
        _HoloHalfHeight ("Half height (m)", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
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

            float4 _Color;
            float _Intensity;
            float _FillAlpha;
            float _RimPower;
            float _RimStrength;
            float _LineDensity;
            float _LineStrength;
            float _LineSpeed;
            float _Reveal;
            float _Flicker;
            float _Beam;
            float4 _HoloCenter;
            float4 _HoloUp;
            float _HoloHalfHeight;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionRWS : TEXCOORD0;   // relatif a la camera (HDRP)
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionRWS = TransformObjectToWorld(input.positionOS);
                o.positionRWS = positionRWS;
                o.positionCS = TransformWorldToHClip(positionRWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 absWS = GetAbsolutePositionWS(input.positionRWS);
                float3 V = normalize(-input.positionRWS);           // la camera est a l'origine du repere relatif
                if (all(abs(input.positionRWS) < 1e-6))
                    V = float3(0, 0, 1);
                float3 N = normalize(input.normalWS);
                float ndv = saturate(abs(dot(N, V)));

                float3 up = normalize(_HoloUp.xyz);
                float h = dot(absWS - _HoloCenter.xyz, up);          // metres le long de l'axe
                float hn = h / max(2.0 * _HoloHalfHeight, 1e-4) + 0.5;

                float t = _Time.y;
                float flicker = 1.0 - _Flicker * Hash(floor(t * 20.0));

                // Lignes de balayage (hauteur absolue : continues d'une piece a l'autre)
                float lines = 0.5 + 0.5 * sin((h * _LineDensity - t * _LineSpeed) * 6.2831853);
                lines = smoothstep(0.35, 1.0, lines);

                float3 color;
                if (_Beam > 1.5)
                {
                    float front = _Reveal * 1.15 - 0.05;
                    clip(front - hn);
                    color = _Color.rgb * _Intensity * (0.75 + 0.25 * lines) * flicker;
                }
                else if (_Beam > 0.5)
                {
                    // Cone : uv.y = 0 a la lentille, 1 a l'hologramme ; bords doux
                    float along = saturate(input.uv.y);
                    float soft = pow(ndv, 1.5);
                    float fade = (1.0 - along) * (1.0 - along) * 0.85 + 0.15 * (1.0 - along);
                    float a = soft * fade * (0.55 + 0.45 * lines);
                    color = _Color.rgb * _Intensity * a * flicker;
                }
                else
                {
                    // Apparition du bas vers le haut, bande lumineuse au front
                    float front = _Reveal * 1.15 - 0.05;
                    clip(front - hn);
                    float band = saturate(1.0 - abs(hn - front) / 0.06) * step(_Reveal, 0.999);

                    float rim = pow(1.0 - ndv, _RimPower) * _RimStrength;
                    float a = _FillAlpha + rim + lines * _LineStrength * 0.35;
                    if (!frontFace)
                        a *= 0.45;                                    // faces arriere plus discretes
                    color = _Color.rgb * _Intensity * (a * flicker + band * 2.5);
                }
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
