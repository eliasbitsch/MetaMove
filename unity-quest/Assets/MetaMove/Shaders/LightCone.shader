// Soft volumetric-looking light: additive, vertex alpha fades it out towards the floor,
// and faces seen edge-on fade too, so the cone has no hard silhouette. No real light,
// no shadows - costs one transparent draw call.
Shader "MetaMove/LightCone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.92, 0.75, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.45
        _EdgeSoftness ("Edge softness", Range(0.5, 8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeSoftness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(posWS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half edge = pow(saturate(facing), _EdgeSoftness);
                half a = i.color.a * edge * _Intensity;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
