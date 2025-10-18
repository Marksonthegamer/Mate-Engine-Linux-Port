Shader "Hidden/MateEngine/CavityBiRP"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthNormalsTexture;

            float _Radius;
            int _SampleCount;
            float _DepthIntensity;
            float _NormalIntensity;
            float _CavityIntensity;
            float _RidgeIntensity;
            float _Power;
            float4 _CavityColor;
            float4 _RidgeColor;
            int _BlendMode;
            float _DebugView;

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            void GetDepthNormal(float2 uv, out float depth01, out float3 normalVS)
            {
                float4 enc = tex2D(_CameraDepthNormalsTexture, uv);
                DecodeDepthNormal(enc, depth01, normalVS);
                normalVS = normalize(normalVS);
            }

            float4 Overlay(float4 baseCol, float4 blendCol)
            {
                float3 a = step(0.5, baseCol.rgb);
                float3 low = 2.0 * baseCol.rgb * blendCol.rgb;
                float3 high = 1.0 - 2.0 * (1.0 - baseCol.rgb) * (1.0 - blendCol.rgb);
                float3 r = lerp(low, high, a);
                return float4(r, 1.0);
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p,float2(127.1,311.7)), dot(p,float2(269.5,183.3)));
                return frac(sin(p)*43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);
                float2 texel = _MainTex_TexelSize.xy;

                float depth0; float3 n0;
                GetDepthNormal(i.uv, depth0, n0);

                float depthSum = 0.0;
                float depthW = 0.0;
                float normalSum = 0.0;
                float normalW = 0.0;

                int count = max(1, _SampleCount);
                float seed = Hash2(i.uv).x;

                [loop]
                for (int k = 0; k < 32; k++)
                {
                    if (k >= count) break;
                    float t = (k + seed) / count;
                    float a = t * 6.2831853;
                    float r = _Radius * (0.5 + 0.5 * frac(t * 3.236));
                    float2 off = float2(cos(a), sin(a)) * r * texel;

                    float2 uv = i.uv + off;

                    float depthS; float3 nS;
                    GetDepthNormal(uv, depthS, nS);

                    float w = 1.0 / (1.0 + 800.0 * dot(off, off));
                    float nd = saturate(1.0 - dot(n0, nS));
                    normalSum += nd * w;
                    normalW += w;

                    float dd = depthS - depth0;
                    depthSum += dd * w;
                    depthW += w;
                }

                float normalEdge = normalW > 0 ? normalSum / normalW : 0.0;
                float depthDelta = depthW > 0 ? depthSum / depthW : 0.0;

                float concave = saturate(depthDelta * _DepthIntensity);
                float convex = saturate(-depthDelta * _DepthIntensity);

                concave = pow(saturate(concave + normalEdge * _NormalIntensity), _Power);
                convex = pow(saturate(convex + normalEdge * _NormalIntensity * 0.5), _Power);

                float cavFactor = saturate(concave * _CavityIntensity);
                float ridgeFactor = saturate(convex * _RidgeIntensity);

                float4 cavCol = _CavityColor * cavFactor;
                float4 ridgeCol = _RidgeColor * ridgeFactor;

                if (_DebugView > 0.5) return float4(cavFactor, ridgeFactor, normalEdge, 1.0);

                if (_BlendMode == 0)
                {
                    return saturate(src + cavCol + ridgeCol);
                }
                else if (_BlendMode == 1)
                {
                    float4 blend = cavCol + ridgeCol + 0.5;
                    return Overlay(src, saturate(blend));
                }
                else
                {
                    float3 col = lerp(src.rgb, _CavityColor.rgb, cavFactor);
                    col = saturate(col + ridgeCol.rgb);
                    return float4(col, 1.0);
                }
            }
            ENDCG
        }
    }
    Fallback Off
}
