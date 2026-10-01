// Age of Sakura - 3D-Toon (style 1.0.0, see Docs/grafikstil-3d-toon.md)
//
// Port of the three.js reference (MeshToonMaterial + OutlineEffect) to URP:
//  * ForwardLit: hemisphere light + one sun, sun term quantised to 3 bands, receives the sun's PCF-soft shadow.
//    Lighting is computed in linear space in every project colour space (the project itself runs in Gamma).
//  * Outline: inverted hull extruded in clip space, constant screen-space width (NDC, like OutlineEffect), dark brown.
//    The extrusion direction is the smoothed normal baked into TEXCOORD3 (hard-edged meshes would tear open otherwise).
//  * _Unlit = 1: foliage. Colour comes from baked vertex colours; nothing is lit in realtime.
// Materials live in Resources/Shaders so builds keep this shader; GameArt creates the materials at runtime.
Shader "AgeOfSakura/Toon"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        _TexMode("Texture mode: 0 none, 1 projected along the dominant axis (object space, in world units), 2 uv0", Float) = 0
        _TexScale("Texture tiles per world unit (mode 1)", Float) = 1
        [HDR] _EmissionColor("Emission (windows, lantern glow)", Color) = (0, 0, 0, 1)
        _Unlit("1 = baked vertex-colour shading (foliage)", Float) = 0
        _Cutoff("Alpha cutoff (leaf cards)", Float) = 0
        _OutlineColor("Outline colour", Color) = (0.16, 0.09, 0.04, 1)
        _OutlineThickness("Outline thickness (NDC)", Float) = 0.0058
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [HideInInspector] _SrcBlend("Src", Float) = 1
        [HideInInspector] _DstBlend("Dst", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _TexMode;
            half _TexScale;
            half4 _EmissionColor;
            half _Unlit;
            half _Cutoff;
            half4 _OutlineColor;
            half _OutlineThickness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // set by ToonStyle.ApplyLighting (linear, already scaled by intensity / pi)
        float4 _AoS_HemiSky;
        float4 _AoS_HemiGround;
        float4 _AoS_Gradient;
        float4 _AoS_FoliageTint;
        float4 _AoS_Grade;  // x: saturation delta, y: S-curve contrast amount, z: warm/cool split (see ToonStyle.ApplyLighting)
        float4 _AoS_Params; // x: glow multiplier (windows and lantern flames burn stronger at night)
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 projPos : TEXCOORD2;   // object-space position in world units (projected textures)
                float3 projNormal : TEXCOORD3;
                float2 uv : TEXCOORD4;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                o.projPos = v.positionOS.xyz * scale;
                o.projNormal = v.normalOS;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 SampleAlbedo(Varyings i)
            {
                half4 tex = half4(1, 1, 1, 1);
                if (_TexMode > 1.5)
                {
                    tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                }
                else if (_TexMode > 0.5)
                {
                    float3 w = pow(abs(i.projNormal), 4.0);
                    w /= max(w.x + w.y + w.z, 1e-4);
                    half4 tx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.projPos.zy * _TexScale);
                    half4 ty = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.projPos.xz * _TexScale);
                    half4 tz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.projPos.xy * _TexScale);
                    tex = tx * w.x + ty * w.y + tz * w.z;
                }
                return tex * _BaseColor * i.color;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 albedo = SampleAlbedo(i);
                if (_Cutoff > 0.0) clip(albedo.a - _Cutoff);

                half3 rgb = albedo.rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                rgb = SRGBToLinear(rgb);
                #endif

                half3 emission = _EmissionColor.rgb;
                #if defined(UNITY_COLORSPACE_GAMMA)
                emission = SRGBToLinear(emission);
                #endif
                emission *= _AoS_Params.x;

                half3 lit;
                if (_Unlit > 0.5)
                {
                    lit = rgb * _AoS_FoliageTint.rgb + emission;
                }
                else
                {
                    float3 n = normalize(i.normalWS);
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                    Light sun = GetMainLight(shadowCoord);

                    // three-step toon ramp, sampled like a nearest-filtered 3 texel gradient map over dot(n, l) * 0.5 + 0.5
                    float t = saturate(dot(n, sun.direction) * 0.5 + 0.5);
                    int band = min(2, (int)floor(t * 3.0));
                    float ramp = band == 0 ? _AoS_Gradient.x : (band == 1 ? _AoS_Gradient.y : _AoS_Gradient.z);

                    half3 hemi = lerp(_AoS_HemiGround.rgb, _AoS_HemiSky.rgb, n.y * 0.5 + 0.5);
                    half3 direct = sun.color * (ramp * sun.shadowAttenuation);
                    lit = rgb * (hemi + direct) + emission;
                }

                lit = saturate(lit);
                #if defined(UNITY_COLORSPACE_GAMMA)
                lit = LinearToSRGB(lit);
                #endif

                // colour grade in display space (all deltas, so an unset global means "neutral"): saturation, S-curve contrast, warm highlights / cool shadows
                half luma = dot(lit, half3(0.299, 0.587, 0.114));
                lit = lerp(luma.xxx, lit, 1.0 + _AoS_Grade.x);
                lit = lerp(lit, lit * lit * (3.0 - 2.0 * lit), _AoS_Grade.y);
                lit += _AoS_Grade.z * (luma - 0.5) * half3(1.0, 0.0, -1.0);
                lit = saturate(lit);
                return half4(lit, albedo.a);
            }
            ENDHLSL
        }

        // Inverted-hull outline. Extruded in clip space so the line keeps its pixel width at any zoom.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Front

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 outlineDir : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVert(Attributes v)
            {
                Varyings o;
                float3 dirOS = dot(v.outlineDir, v.outlineDir) > 1e-4 ? v.outlineDir : v.normalOS;
                float4 clip = TransformObjectToHClip(v.positionOS.xyz);
                float3 dirWS = TransformObjectToWorldNormal(dirOS);
                float2 d = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;
                float len = length(d);
                d = len > 1e-5 ? d / len : float2(0, 0);
                d.x *= _ScreenParams.y / _ScreenParams.x;
                clip.xy += d * (_OutlineThickness * clip.w);
                o.positionCS = clip;
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target
            {
                return half4(_OutlineColor.rgb, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target
            {
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
