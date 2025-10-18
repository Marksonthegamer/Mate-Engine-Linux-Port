// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "@Xxuebi/Trail_T_001"
{
	Properties
	{
		_Tex_Main("Tex_Main", 2D) = "white" {}
		_Tex_Mask("Tex_Mask", 2D) = "white" {}
		[HDR]_Main_Color("Main_Color", Color) = (0.4386792,0.7203457,1,0)
		_Main_UVSpeed("Main_UVSpeed", Vector) = (0,0,0,0)
		_Mask_UVSpeed("Mask_UVSpeed", Vector) = (0,0,0,0)
		[HideInInspector] _tex4coord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Custom"  "Queue" = "Overlay+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		ZWrite Off
		Blend SrcAlpha One
		BlendOp Add
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Unlit keepalpha noshadow 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 uv_tex4coord;
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
		};

		uniform float4 _Main_Color;
		uniform sampler2D _Tex_Main;
		uniform float2 _Main_UVSpeed;
		uniform float4 _Tex_Main_ST;
		uniform sampler2D _Tex_Mask;
		uniform float2 _Mask_UVSpeed;
		uniform float4 _Tex_Mask_ST;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv0_Tex_Main = i.uv_texcoord * _Tex_Main_ST.xy + _Tex_Main_ST.zw;
			float2 panner10 = ( 1.0 * _Time.y * _Main_UVSpeed + uv0_Tex_Main);
			float4 tex2DNode2 = tex2D( _Tex_Main, panner10 );
			o.Emission = ( ( i.uv_tex4coord.z + 1.0 ) * ( _Main_Color * tex2DNode2.r * i.vertexColor ) ).rgb;
			float2 uv0_Tex_Mask = i.uv_texcoord * _Tex_Mask_ST.xy + _Tex_Mask_ST.zw;
			float2 panner17 = ( 1.0 * _Time.y * _Mask_UVSpeed + uv0_Tex_Mask);
			o.Alpha = ( i.vertexColor.a * tex2DNode2.r * tex2D( _Tex_Mask, panner17 ).r );
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=17700
781.3334;340;1080;697;1737.916;429.6861;1.710279;True;False
Node;AmplifyShaderEditor.Vector2Node;11;-1267.775,26.64764;Inherit;False;Property;_Main_UVSpeed;Main_UVSpeed;4;0;Create;True;0;0;False;0;0,0;0,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;9;-1290.137,-119.7658;Inherit;False;0;2;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;10;-978.679,-107.2365;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;15;-1245.15,368.3235;Inherit;False;Property;_Mask_UVSpeed;Mask_UVSpeed;5;0;Create;True;0;0;False;0;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;16;-1267.512,221.91;Inherit;False;0;8;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.VertexColorNode;6;-116.5753,-80.139;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;2;-713.3658,-126.8483;Inherit;True;Property;_Tex_Main;Tex_Main;0;0;Create;True;0;0;False;0;-1;None;cf4b0728ba7d41e4cac76deadb84708f;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;5;-346.9977,-398.8534;Inherit;False;Property;_Main_Color;Main_Color;3;1;[HDR];Create;True;0;0;False;0;0.4386792,0.7203457,1,0;3.56487,3.56487,3.56487,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;12;-56.99945,-543.7614;Inherit;False;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;17;-956.0536,234.4393;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;8;-713.9947,174.3029;Inherit;True;Property;_Tex_Mask;Tex_Mask;1;0;Create;True;0;0;False;0;-1;None;eb87a105364c5a34b8d964a5d7a015d7;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;204.3672,-214.7279;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;13;250.0005,-388.7614;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;7;184.8472,77.40984;Inherit;True;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;14;428.0005,-240.7614;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;595.2497,-265.7123;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;@Xxuebi/Trail_T_001;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;2;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;False;0;True;Custom;;Overlay;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;8;5;False;-1;1;False;-1;0;0;False;-1;0;False;-1;1;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;2;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;10;0;9;0
WireConnection;10;2;11;0
WireConnection;2;1;10;0
WireConnection;17;0;16;0
WireConnection;17;2;15;0
WireConnection;8;1;17;0
WireConnection;3;0;5;0
WireConnection;3;1;2;1
WireConnection;3;2;6;0
WireConnection;13;0;12;3
WireConnection;7;0;6;4
WireConnection;7;1;2;1
WireConnection;7;2;8;1
WireConnection;14;0;13;0
WireConnection;14;1;3;0
WireConnection;0;2;14;0
WireConnection;0;9;7;0
ASEEND*/
//CHKSM=DA84EA47721474AB0FE258680083EC21C8F891D7