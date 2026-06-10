// Flat half-disk shader. Discards fragments behind the local +Z half-plane,
// so a primitive cylinder placed on its side renders as a half-disk on the floor.
Shader "MetaMove/HalfRing"
{
    Properties
    {
        _BaseColor ("Base colour (rgba)", Color) = (1,1,1,0.1)
        _Color     ("Color (mirror)",     Color) = (1,1,1,0.1)
        _HalfAxis  ("Half axis (0=+X,1=+Z,2=-X,3=-Z)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionOS : TEXCOORD0; };

            float4 _BaseColor, _Color;
            float  _HalfAxis;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Use whichever color property has the higher alpha (MPB-friendly).
                half4 c = _BaseColor.a >= _Color.a ? _BaseColor : _Color;

                // Discard the back half. Cylinder primitive: X/Z form the disk,
                // Y is the (very flat) thickness. Pick axis by _HalfAxis.
                float keep;
                if      (_HalfAxis < 0.5) keep = IN.positionOS.x;
                else if (_HalfAxis < 1.5) keep = IN.positionOS.z;
                else if (_HalfAxis < 2.5) keep = -IN.positionOS.x;
                else                       keep = -IN.positionOS.z;
                if (keep < 0.0) discard;

                return c;
            }
            ENDHLSL
        }
    }
}
