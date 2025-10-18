Shader "Custom/TransparentShadow"
{
    Properties
    {
        _ShadowColor("Shadow Color", Color) = (0.2, 0.2, 0.2, 0.8)
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "AutoLight.cginc"

    struct v2f
    {
        float4 pos : SV_POSITION;
        float3 worldPos : TEXCOORD0;
        UNITY_LIGHTING_COORDS(1, 2)
    };

    fixed4 _ShadowColor;

    v2f vert (appdata_full v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        UNITY_TRANSFER_LIGHTING(o, v.texcoord1);
        return o;
    }

    fixed4 frag (v2f i) : SV_Target
    {
        
        float atten = 1 - UNITY_SHADOW_ATTENUATION(i, i.worldPos);
        return fixed4(_ShadowColor.rgb, _ShadowColor.a * atten);
    }

    ENDCG


    SubShader
    {
        Tags { "Queue"="Geometry" }
        Pass
        {
            ColorMask 0
            ZWrite on
            CGPROGRAM
            #pragma vertex vert_no
            #pragma fragment frag_no

            struct v2f_no {
                float4 pos : SV_POSITION;
            };

            v2f_no vert_no (appdata_full v)
            {
                v2f_no o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag_no(v2f_no i) : SV_Target
            {
                return fixed4(0,0,0,0);
            }

            ENDCG
        }


        // directional light (only one)
        Pass
        {
            Tags { "Queue"="Transparent" "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            ENDCG
        }

        // other lights (point lights, spot lights ...etc)
        pass
        {
            Tags { "Queue"="Transparent" "LightMode" = "ForwardAdd" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            ENDCG
        }
    }

    Fallback "VertexLit"
}
