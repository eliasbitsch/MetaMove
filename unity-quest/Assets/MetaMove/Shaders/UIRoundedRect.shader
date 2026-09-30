// Rounded rectangle for uGUI, evaluated per pixel instead of sampled from a texture.
//
// A 9-sliced sprite bakes the corner at one resolution and stretches it, so the curve softens as
// the panel grows or the viewer steps closer. Here the distance to the rounded edge is computed in
// the fragment shader and the edge is faded over exactly one pixel, which keeps corners crisp at
// any size or viewing distance — the same reason UI Toolkit's corners look clean.
Shader "MetaMove/UI Rounded Rect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _RectSize ("Rect Size (px)", Vector) = (100, 100, 0, 0)
        _Radius ("Corner Radius (px)", Float) = 24
        _BlurSize ("Backdrop Blur (uv)", Float) = 0
        _FeedC0 ("Feed corners (BL.xy, BR.xy)", Vector) = (0, 0, 1, 0)
        _FeedC1 ("Feed corners (TL.xy, TR.xy)", Vector) = (0, 1, 1, 1)
        _MetalRim ("Metal rim (0/1)", Float) = 0
        _MetalGloss ("Metal gloss", Float) = 1.2
        _MetalTint ("Metal keeps its colour (0..1)", Float) = 0
        _BorderWidth ("Border Width (px)", Float) = 0
        _BorderColor ("Border Color", Color) = (1,1,1,1)
        _GlowWidth ("Glow Width (px)", Float) = 0
        _GlowIntensity ("Glow Intensity", Float) = 1
        _GlowFalloff ("Glow Falloff Power", Float) = 1.5

        [Toggle] _GoggleShape ("Goggle Shape", Float) = 0
        _LensSeparation ("Lens Separation", Range(0, 1)) = 0.34
        _LensRadius ("Lens Radius", Range(0.1, 1.2)) = 0.62
        _LensBlend ("Lens Blend", Range(0, 1)) = 0.35

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _RectSize;
            float _Radius;
            float _BlurSize;
            // The four passthrough-feed UVs behind the panel's four corners (BL,BR,TL,TR), bilinearly
            // interpolated per fragment. A full quad mapping — unlike a scale+offset box it also carries
            // the head's roll and skew, so the frost stays glued to the world when the head tilts.
            // Defaults map to the whole feed (identity).
            float4 _FeedC0; // BL.xy, BR.xy
            float4 _FeedC1; // TL.xy, TR.xy
            float _BorderWidth;

            // Disk blur of the bound texture. Fed a plain RenderTexture (PassthroughCameraFeed re-renders
            // the external OES feed into one) so offset UVs actually sample different texels — the raw
            // WebCamTexture ignores computed coordinates and would stay sharp.
            //
            // A Vogel spiral scatters the taps evenly across a disk of radius _BlurSize: a coarse grid
            // with a large offset only makes a few ghost copies (looks almost sharp), while the spiral
            // fills the disk smoothly so it reads as real frosted glass.
            #define BLUR_TAPS 16
            fixed4 SampleTex(float2 uv)
            {
                if (_BlurSize <= 0.0001) return tex2D(_MainTex, uv);
                fixed4 sum = tex2D(_MainTex, uv);
                const float golden = 2.3999632; // golden angle, radians
                // Per-pixel random start angle: rotates the spiral differently for each fragment so the
                // disk taps dissolve into fine frost grain instead of a few visible blobs (the
                // "voxelated" look a fixed pattern gives at a large radius).
                float rnd = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
                float a0 = rnd * 6.2831853;
                [unroll] for (int k = 0; k < BLUR_TAPS; k++)
                {
                    float r = sqrt((k + 0.5) / BLUR_TAPS); // even area coverage across the disk
                    float a = a0 + k * golden;
                    float2 off = float2(cos(a), sin(a)) * r * _BlurSize;
                    sum += tex2D(_MainTex, uv + off);
                }
                return sum / (BLUR_TAPS + 1);
            }
            fixed4 _BorderColor;
            float _MetalRim;
            float _MetalGloss;
            float _MetalTint;
            float _GlowWidth;
            float _GlowIntensity;
            float _GlowFalloff;
            float _GoggleShape;
            float _LensSeparation;
            float _LensRadius;
            float _LensBlend;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // Signed distance to a rounded box: negative inside, zero on the edge.
            // The parameter is not called "point" — that is a reserved word in HLSL.
            float RoundedBoxSDF(float2 samplePos, float2 halfSize, float radius)
            {
                float2 q = abs(samplePos) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            // Polynomial smooth minimum. Merges two shapes with a soft fillet instead of the crease
            // a plain min() leaves behind.
            float SmoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / max(k, 1e-4));
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            // Two lens circles blended into one outline. A ski goggle is exactly this: two round
            // lenses joined across the bridge, which gives the gentle waist at top and bottom centre
            // that a rounded rectangle can never produce however much its corners are softened.
            float GoggleSDF(float2 samplePos, float2 halfSize)
            {
                float reference = min(halfSize.x, halfSize.y);
                float separation = halfSize.x * _LensSeparation;
                float radius = reference * _LensRadius;

                float left = length(samplePos - float2(-separation, 0.0)) - radius;
                float right = length(samplePos - float2(separation, 0.0)) - radius;

                return SmoothMin(left, right, reference * _LensBlend);
            }

            // Polished-metal bezel colour for the rim. A key light (upper-left) and a fill (lower-right)
            // shade the ring; a broad sheen plus a tight hot core give the gloss, a faked top-lit
            // environment adds reflectivity, and a glint sweeps around the bezel over time so it reads
            // like a moving reflection when the head turns. `samplePos` is centred on the panel (pixels);
            // _MetalGloss scales the overall shine.
            float3 MetalRim(float2 samplePos, float3 baseCol)
            {
                float2 dir = normalize(samplePos + 1e-5);
                float2 keyDir  = normalize(float2(-0.55, 1.0));
                float2 fillDir = normalize(float2(0.7, -0.5));
                float fKey  = dot(dir, keyDir);                    // -1 (away) .. 1 (toward key light)
                float fFill = dot(dir, fillDir);

                // Neutral steel with a hint of tint by default. _MetalTint pulls that the other way, up
                // to keeping the colour outright: a brushed bezel wants to be grey, but a bezel whose
                // job is to say *which* action this is cannot afford to have its colour averaged out —
                // 60 % steel turned yellow into white. Default 0 leaves every existing panel untouched.
                float keep = saturate(_MetalTint);
                float3 steel = lerp(baseCol, float3(0.82, 0.85, 0.90), 0.6 * (1.0 - keep));
                float shade = 0.42 + 0.5 * (fKey * 0.5 + 0.5);     // dark side -> light side

                // Faked environment reflection: bright "sky" toward the top of the ring, dark below.
                float3 env = lerp(float3(0.10, 0.11, 0.14), float3(0.96, 0.98, 1.0), saturate(dir.y * 0.5 + 0.5));

                float ang = atan2(samplePos.y, samplePos.x);
                float streak = 0.95 + 0.05 * sin(ang * 90.0);      // fine turned-metal streaks

                float sheen = pow(saturate(fKey), 5.0) * 0.8 + pow(saturate(fFill), 6.0) * 0.4; // broad gloss
                float hot   = pow(saturate(fKey), 48.0) * 2.0;     // tight specular hotspot
                float sweep = pow(saturate(sin(ang * 2.0 + _Time.y * 1.2)), 24.0) * 1.2; // travelling glint

                float3 c = steel * shade * streak;
                // The reflected environment is white sky over a dark floor, which washes a tinted bezel
                // back toward grey exactly where it is brightest. Tinting the reflection keeps the hue
                // through the highlight — coloured metal reflects coloured light.
                c = lerp(c, env * lerp(float3(1, 1, 1), baseCol, keep), 0.35);
                c += (sheen + hot + sweep) * _MetalGloss;          // gloss on top
                return saturate(c);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 halfSize = _RectSize.xy * 0.5;
                float2 position = (i.texcoord - 0.5) * _RectSize.xy;

                float radius = min(_Radius, min(halfSize.x, halfSize.y));
                float distance = _GoggleShape > 0.5
                    ? GoggleSDF(position, halfSize)
                    : RoundedBoxSDF(position, halfSize, radius);

                // fwidth gives how much the distance changes across one screen pixel, so the edge
                // always fades over a single pixel regardless of how close the panel is.
                float aa = max(fwidth(distance), 0.0001);

                float fill = 1.0 - smoothstep(-aa, aa, distance);

                fixed4 color = i.color;
                // Bilinear map the panel's 0..1 quad UV through the four projected feed corners, so the
                // frost carries head roll/skew (a scale+offset box could only shift, not rotate).
                float2 fBottom = lerp(_FeedC0.xy, _FeedC0.zw, i.texcoord.x); // BL→BR
                float2 fTop    = lerp(_FeedC1.xy, _FeedC1.zw, i.texcoord.x); // TL→TR
                float2 feedUV  = lerp(fBottom, fTop, i.texcoord.y);
                color *= SampleTex(feedUV);

                // The passthrough cameras see a fixed cone in front of the headset. A panel that drifts
                // out of it has no image to be blurred: the sampler clamps, and the edge pixels smear
                // into long streaks across the glass. So fade the backdrop out as the sample leaves the
                // frame — the panel falls back to its flat frost tint, which is honest about there being
                // nothing to show. The margin is generous because the streaks appear before the edge,
                // where the last row of pixels is already stretched.
                if (_BlurSize > 0.0001)
                {
                    float2 edge = min(feedUV, 1.0 - feedUV);            // distance to the nearest border
                    float inFeed = smoothstep(0.0, 0.06, min(edge.x, edge.y));
                    color.a *= inFeed;
                }

                float alpha = color.a * fill;

                if (_BorderWidth > 0.0)
                {
                    float inner = distance + _BorderWidth;
                    float ring = (1.0 - smoothstep(-aa, aa, distance)) * smoothstep(-aa, aa, inner);

                    float3 borderRgb = _MetalRim > 0.5 ? MetalRim(position, _BorderColor.rgb) : _BorderColor.rgb;
                    color.rgb = lerp(color.rgb, borderRgb, ring);

                    // The border carries its own opacity instead of inheriting the fill's. Taking
                    // alpha from the fill alone meant a border drawn over a transparent panel was
                    // clipped away entirely, which is exactly what a frame with a hollow centre is.
                    alpha = max(alpha, ring * _BorderColor.a);
                }

                if (_GlowWidth > 0.0)
                {
                    // A soft halo spreading inward from the edge. The exponent sets how gradual the
                    // hand-off is: squared concentrates the light right at the border, while values
                    // near one spread it far inward and give a wide, gentle transition.
                    float inward = max(-distance, 0.0);
                    float falloff = saturate(1.0 - inward / _GlowWidth);
                    // Multiplied by the fill mask so the halo stays strictly inside the shape.
                    // Without this, points outside the rounded edge (distance > 0) have inward == 0,
                    // which makes falloff == 1 — a full-strength glow leaking into the quad's square
                    // corners as faint semi-transparent patches.
                    float glow = pow(falloff, max(_GlowFalloff, 0.1)) * _GlowIntensity * fill;

                    color.rgb = lerp(color.rgb, _BorderColor.rgb, saturate(glow));
                    alpha = max(alpha, glow * _BorderColor.a);
                }

                color.a = alpha;
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
