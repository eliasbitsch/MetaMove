// Safety wall: blue cross/grid baseline, with per-fragment red glow only at
// the spots where the user's head/hands are close. Penetration rings on top.
Shader "MetaMove/SafetyWall"
{
    Properties
    {
        _ColorFar  ("Cross/grid colour (blue)", Color) = (0.2,0.55,1,1)
        _ColorRed  ("Red colour (stop)",         Color) = (1,0.05,0.05,1)
        _ColorRing ("Ring colour",               Color) = (1,0.1,0.1,1)

        _GridSize     ("Grid size (m)", Float) = 0.20
        _LineWidth    ("Line width (uv)", Range(0.005, 0.1)) = 0.025
        _CrossMin     ("Cross half-extent (far)",  Range(0.05, 0.5)) = 0.10
        _CrossMax     ("Cross half-extent (near)", Range(0.20, 0.5)) = 0.50

        _Proximity ("Proximity 0..1 (1=very close, fades alpha + grid fill)", Range(0,1)) = 0
        _GridFill  ("Grid fill 0..1 (cross→grid)", Range(0,1)) = 0
        _MaxAlpha  ("Max alpha", Range(0,1)) = 0.85

        // Probes: per-fragment red glow = saturate( max over probes of
        // inverseLerp(redOuter, redInner, distance(frag, probe)) ).
        // w component is redOuter range (m). 0 disables.
        _ProbeA ("Probe A (xyz, w=range m)", Vector) = (0,0,0,0)
        _ProbeB ("Probe B (xyz, w=range m)", Vector) = (0,0,0,0)
        _ProbeC ("Probe C (xyz, w=range m)", Vector) = (0,0,0,0)
        _RedInner ("Red inner radius (m, full red)", Float) = 0.15

        // Penetration rings (hand inside box). w=ring radius.
        _HandA ("Hand A (xyz, w=radius)", Vector) = (0,0,0,0)
        _HandB ("Hand B (xyz, w=radius)", Vector) = (0,0,0,0)
        _RingThickness ("Ring thickness", Range(0.001, 0.05)) = 0.012
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
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float4 _ColorFar, _ColorRed, _ColorRing;
            float  _GridSize, _LineWidth, _CrossMin, _CrossMax;
            float  _Proximity, _GridFill, _MaxAlpha;
            float4 _ProbeA, _ProbeB, _ProbeC;
            float  _RedInner;
            float4 _HandA, _HandB;
            float  _RingThickness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            float2 wallUV(float3 wp)
            {
                float3 n = abs(normalize(cross(ddx(wp), ddy(wp))));
                if (n.x > n.y && n.x > n.z) return wp.yz;
                if (n.y > n.z)               return wp.xz;
                return wp.xy;
            }

            float gridPattern(float2 uv)
            {
                float2 g = frac(uv / _GridSize) - 0.5;
                float ex = lerp(_CrossMin, _CrossMax, saturate(_GridFill));
                float lw = _LineWidth;
                float horiz = step(abs(g.y), lw) * step(abs(g.x), ex);
                float vert  = step(abs(g.x), lw) * step(abs(g.y), ex);
                return saturate(horiz + vert);
            }

            float ringMask(float3 wp, float4 hand)
            {
                float r = hand.w;
                if (r <= 0.0) return 0.0;
                float d = distance(wp, hand.xyz);
                float inside  = step(d, r);
                float outside = step(d, r - _RingThickness);
                return inside - outside;
            }

            // Per-probe red contribution: 1 inside _RedInner, 0 outside w (range), smooth between.
            float probeRed(float3 wp, float4 probe)
            {
                if (probe.w <= 0.0) return 0.0;
                float d = distance(wp, probe.xyz);
                return saturate(1.0 - smoothstep(_RedInner, probe.w, d));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = wallUV(IN.positionWS);
                float pattern = gridPattern(uv);

                float redLocal = max(probeRed(IN.positionWS, _ProbeA),
                                 max(probeRed(IN.positionWS, _ProbeB),
                                     probeRed(IN.positionWS, _ProbeC)));

                half3 col = lerp(_ColorFar.rgb, _ColorRed.rgb, redLocal);

                // Alpha: baseline by proximity, plus boost where red glows.
                float alpha = pattern * _MaxAlpha * saturate(_Proximity * 1.2);
                alpha = max(alpha, pattern * _MaxAlpha * redLocal);

                // Penetration rings — additive on top.
                float ring = saturate(ringMask(IN.positionWS, _HandA) + ringMask(IN.positionWS, _HandB));
                if (ring > 0.0)
                {
                    col = lerp(col, _ColorRing.rgb, ring);
                    alpha = max(alpha, ring * _MaxAlpha);
                }
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
