Shader "UI/LegacyTextShaderFade2WayOverlayCrossfadeHue"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}

        // Bottom fade
        _FadeBottomStart ("Fade Bottom Start (0-1)", Range(0,1)) = 0.0
        _FadeBottomEnd   ("Fade Bottom End (0-1)",   Range(0,1)) = 0.10

        // Top fade
        _FadeTopStart ("Fade Top Start (0-1)", Range(0,1)) = 0.90
        _FadeTopEnd   ("Fade Top End (0-1)",   Range(0,1)) = 1.00

        // -------- Moving overlay (shared across all UI) --------
        _OverlayTex     ("Overlay Gradient", 2D) = "white" {}
        _OverlayColor   ("Overlay Color (tint)", Color) = (1,1,1,1)
        _OverlayShare   ("Overlay Share (0..1)", Range(0,1)) = 0.5
        _OverlayScale   ("Overlay Scale (xy)", Vector) = (1,1,0,0)
        _OverlayOffset  ("Overlay Offset (xy)", Vector) = (0,0,0,0)
        _OverlaySpeed   ("Overlay Speed (xy units/sec)", Vector) = (0.05,0,0,0)

        // 0 = ScreenSpace, 1 = WorldSpace
        _OverlaySpaceMode ("Overlay Space Mode (0=Screen,1=World)", Float) = 0
        // 0 = take A as mask, 1 = use luminance of RGB as mask
        _OverlayUseLuma ("Overlay uses Luminance (0=A,1=RGB)", Float) = 0

        // -------- Hue Shift controls (material-driven) --------
        _OverlayHueShift ("Overlay Hue Shift (0..1)", Range(0,1)) = 0.0
        _OverlaySatMul   ("Overlay Saturation Mult", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Lighting Off
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float _FadeBottomStart, _FadeBottomEnd;
            float _FadeTopStart,    _FadeTopEnd;

            sampler2D _OverlayTex;
            float4 _OverlayColor;
            float  _OverlayShare;
            float4 _OverlayScale;   // xy
            float4 _OverlayOffset;  // xy
            float4 _OverlaySpeed;   // xy
            float  _OverlaySpaceMode;
            float  _OverlayUseLuma;

            float  _OverlayHueShift; // 0..1
            float  _OverlaySatMul;   // saturation multiplier

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;   // Legacy Text color (Inspector)
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float2 uvFont    : TEXCOORD0;
                float4 color     : COLOR;
                float  fadePosY  : TEXCOORD1;  // NDC Y 0..1
                float2 uvBase    : TEXCOORD2;  // overlay UV basis
            };

            // --- RGB<->HSV helpers (compact) ---
            float3 rgb2hsv(float3 c) {
                float4 K = float4(0., -1./3., 2./3., -1.);
                float4 p = c.g < c.b ? float4(c.bg, K.wz) : float4(c.gb, K.xy);
                float4 q = c.r < p.x ? float4(p.xyw, c.r) : float4(c.r, p.yzx);
                float d = q.x - min(q.w, q.y);
                float e = 1e-10;
                float3 hsv;
                hsv.x = abs(q.z + (q.w - q.y) / (6.*d + e)); // hue 0..1
                hsv.y = d / (q.x + e);
                hsv.z = q.x;
                return hsv;
            }
            float3 hsv2rgb(float3 c) {
                float4 K = float4(1., 2./3., 1./3., 3.);
                float3 p = abs(frac(c.xxx + K.xyz) * 6. - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex  = UnityObjectToClipPos(v.vertex);
                o.uvFont  = v.texcoord;
                o.color   = v.color;

                float2 ndc = (o.vertex.xy / o.vertex.w) * 0.5 + 0.5;
                o.fadePosY = ndc.y;

                if (_OverlaySpaceMode < 0.5) {
                    o.uvBase = ndc;
                } else {
                    float4 wp = mul(unity_ObjectToWorld, v.vertex);
                    o.uvBase = wp.xy;
                }
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 fontTex = tex2D(_MainTex, i.uvFont);
                fixed4 col = i.color;
                col.a *= fontTex.a;

                if (_FadeBottomEnd > _FadeBottomStart) {
                    float fb = saturate((i.fadePosY - _FadeBottomStart) / max(0.0001, (_FadeBottomEnd - _FadeBottomStart)));
                    col.a *= fb;
                }
                if (_FadeTopEnd > _FadeTopStart) {
                    float ft = 1.0 - saturate((i.fadePosY - _FadeTopStart) / max(0.0001, (_FadeTopEnd - _FadeTopStart)));
                    col.a *= ft;
                }
                if (col.a <= 0.0001) return col;

                // Moving overlay UV (wrapped)
                float2 uvAnim = i.uvBase * _OverlayScale.xy + _OverlayOffset.xy + _Time.y * _OverlaySpeed.xy;
                uvAnim = frac(uvAnim);

                fixed4 ov = tex2D(_OverlayTex, uvAnim);

                // Mask from A or luminance
                float g = lerp(ov.a, dot(ov.rgb, fixed3(0.299, 0.587, 0.114)), step(0.5, _OverlayUseLuma));
                g = saturate(g);

                // Overlay RGB tinted, then hue-shifted & sat-scaled
                float3 ovRGB = ov.rgb * _OverlayColor.rgb;

                // apply hue shift on overlay independently
                float3 hsv = rgb2hsv(ovRGB);
                hsv.x = frac(hsv.x + _OverlayHueShift); // rotate hue
                hsv.y = saturate(hsv.y * _OverlaySatMul);
                ovRGB = hsv2rgb(hsv);

                // Crossfade (keine Alpha-Änderung)
                float w = saturate(g * _OverlayShare) * _OverlayColor.a;
                col.rgb = lerp(col.rgb, ovRGB, w);

                return col;
            }
            ENDCG
        }
    }
}
