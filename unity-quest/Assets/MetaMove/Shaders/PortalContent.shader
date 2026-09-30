// Unlit texture drawn only where MetaMove/PortalMask wrote the stencil: the factory hall
// behind the wall breach. A vertical fade darkens the edges so the photo sinks into depth.
Shader "MetaMove/PortalContent"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Ref ("Stencil Ref", Int) = 4
        _Vignette ("Vignette", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On
            Cull Off
            Stencil { Ref [_Ref] Comp Equal }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Vignette;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint;
                float2 d = i.uv - 0.5;
                half v = saturate(1.0 - dot(d, d) * 2.2 * _Vignette);
                c.rgb *= v;
                c.a = 1;
                return c;
            }
            ENDHLSL
        }
    }
}
