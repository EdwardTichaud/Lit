// Made with Amplify Shader Editor v1.9.1.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "ASE/ASE_StandartVertexBlendTesselated"
{
	Properties
	{
		_HeightMapR("HeightMapR-----", 2D) = "white" {}
		_AlbedoR("AlbedoR", 2D) = "white" {}
		_NormalMapR("NormalMapR", 2D) = "white" {}
		[Toggle]_SmoothRoughR("Smooth/RoughR", Float) = 0
		_SmoothStrR("SmoothStrR", Float) = 0.001
		_NormalDepthR("NormalDepthR", Float) = 0.001
		_BlendContrastR("BlendContrastR", Range( 0 , 20)) = 0.5
		_BlendFactorR("BlendFactorR", Range( -1 , 1)) = 0.5
		_TilingR("TilingR", Float) = 1
		_HeightMapG("HeightMapG-----", 2D) = "white" {}
		_AlbedoG("AlbedoG", 2D) = "white" {}
		_NormalMapG("NormalMapG", 2D) = "white" {}
		[Toggle]_SmoothRoughG("Smooth/RoughG", Float) = 0
		_NormalDepthG("NormalDepthG", Float) = 0.001
		_SmoothStrG("SmoothStrG", Float) = 0.001
		_BlendFactorG("BlendFactorG", Range( -1 , 1)) = 0.5
		_BlendContrastG("BlendContrastG", Range( 0 , 20)) = 0.5
		_TilingG("TilingG", Float) = 1
		_HeightMapB("HeightMapB----", 2D) = "white" {}
		_AlbedoB("AlbedoB", 2D) = "white" {}
		_NormalMapB("NormalMapB", 2D) = "white" {}
		[Toggle]_SmoothRoughB("Smooth/RoughB", Float) = 0
		_NormalDepthB("NormalDepthB", Float) = 0.001
		_SmoothStrB("SmoothStrB", Float) = 0.001
		_BlendFactorB("BlendFactorB", Range( -1 , 1)) = 0.5
		_BlendContrastB("BlendContrastB", Range( 0 , 20)) = 0.5
		_TilingB("TilingB", Float) = 1
		_Metall("Metall", Range( 0 , 1)) = 0.5
		_TilingBase("TilingBase", Float) = 1
		_NormalBase("NormalBase", 2D) = "bump" {}
		_AlbedoBase("AlbedoBase", 2D) = "white" {}
		_SmoothStrBase("SmoothStrBase", Float) = 0.001
		_NormalDepthBase("NormalDepthBase", Float) = 0.001
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		CGPROGRAM
		#include "UnityStandardUtils.cginc"
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
		};

		uniform sampler2D _NormalBase;
		uniform float _TilingBase;
		uniform float _NormalDepthBase;
		uniform sampler2D _NormalMapR;
		uniform float _TilingR;
		uniform float _NormalDepthR;
		uniform float _BlendContrastR;
		uniform sampler2D _HeightMapR;
		uniform float _BlendFactorR;
		uniform sampler2D _NormalMapG;
		uniform float _TilingG;
		uniform float _NormalDepthG;
		uniform float _BlendContrastG;
		uniform sampler2D _HeightMapG;
		uniform float _BlendFactorG;
		uniform sampler2D _NormalMapB;
		uniform float _TilingB;
		uniform float _NormalDepthB;
		uniform float _BlendContrastB;
		uniform sampler2D _HeightMapB;
		uniform float _BlendFactorB;
		uniform sampler2D _AlbedoBase;
		uniform sampler2D _AlbedoR;
		uniform sampler2D _AlbedoG;
		uniform sampler2D _AlbedoB;
		uniform float _Metall;
		uniform float _SmoothStrBase;
		uniform float _SmoothRoughR;
		uniform float _SmoothStrR;
		uniform float _SmoothRoughG;
		uniform float _SmoothStrG;
		uniform float _SmoothRoughB;
		uniform float _SmoothStrB;


		float4 CalculateContrast( float contrastValue, float4 colorTarget )
		{
			float t = 0.5 * ( 1.0 - contrastValue );
			return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 temp_cast_0 = (_TilingBase).xx;
			float2 uv_TexCoord17 = i.uv_texcoord * temp_cast_0;
			float2 temp_cast_1 = (_TilingR).xx;
			float2 uv_TexCoord233 = i.uv_texcoord * temp_cast_1;
			float4 tex2DNode122 = tex2D( _HeightMapR, uv_TexCoord233 );
			float4 clampResult142 = clamp( ( tex2DNode122 + _BlendFactorR ) , float4( 0,0,0,0 ) , float4( 1,0,0,0 ) );
			float clampResult134 = clamp( (clampResult142).r , 0.0 , 1.0 );
			float HeightMask132 = saturate(pow(((clampResult134*i.vertexColor.r)*4)+(i.vertexColor.r*2),_BlendContrastR));
			float4 temp_cast_2 = (HeightMask132).xxxx;
			float clampResult246 = clamp( CalculateContrast(_BlendContrastR,temp_cast_2).r , 0.0 , 1.0 );
			float temp_output_243_0 = ( clampResult246 * 1.0 );
			float3 lerpResult167 = lerp( UnpackScaleNormal( tex2D( _NormalBase, uv_TexCoord17 ), _NormalDepthBase ) , UnpackScaleNormal( tex2D( _NormalMapR, uv_TexCoord233 ), _NormalDepthR ) , temp_output_243_0);
			float2 temp_cast_3 = (_TilingG).xx;
			float2 uv_TexCoord235 = i.uv_texcoord * temp_cast_3;
			float4 tex2DNode144 = tex2D( _HeightMapG, uv_TexCoord235 );
			float4 clampResult148 = clamp( ( tex2DNode144 + _BlendFactorG ) , float4( 0,0,0,0 ) , float4( 1,0,0,0 ) );
			float clampResult150 = clamp( (clampResult148).r , 0.0 , 1.0 );
			float HeightMask151 = saturate(pow(((clampResult150*i.vertexColor.g)*4)+(i.vertexColor.g*2),_BlendContrastG));
			float4 temp_cast_4 = (HeightMask151).xxxx;
			float clampResult247 = clamp( CalculateContrast(_BlendContrastG,temp_cast_4).r , 0.0 , 1.0 );
			float temp_output_244_0 = ( clampResult247 * 1.0 );
			float3 lerpResult169 = lerp( lerpResult167 , UnpackScaleNormal( tex2D( _NormalMapG, uv_TexCoord235 ), _NormalDepthG ) , temp_output_244_0);
			float2 temp_cast_5 = (_TilingB).xx;
			float2 uv_TexCoord237 = i.uv_texcoord * temp_cast_5;
			float4 tex2DNode159 = tex2D( _HeightMapB, uv_TexCoord237 );
			float4 clampResult155 = clamp( ( tex2DNode159 + _BlendFactorB ) , float4( 0,0,0,0 ) , float4( 1,0,0,0 ) );
			float clampResult157 = clamp( (clampResult155).r , 0.0 , 1.0 );
			float HeightMask158 = saturate(pow(((clampResult157*i.vertexColor.b)*4)+(i.vertexColor.b*2),_BlendContrastB));
			float4 temp_cast_6 = (HeightMask158).xxxx;
			float clampResult248 = clamp( CalculateContrast(_BlendContrastB,temp_cast_6).r , 0.0 , 1.0 );
			float temp_output_245_0 = ( clampResult248 * 1.0 );
			float3 lerpResult170 = lerp( lerpResult169 , UnpackScaleNormal( tex2D( _NormalMapB, uv_TexCoord237 ), _NormalDepthB ) , temp_output_245_0);
			o.Normal = lerpResult170;
			float4 tex2DNode1 = tex2D( _AlbedoR, uv_TexCoord233 );
			float4 lerpResult133 = lerp( tex2D( _AlbedoBase, uv_TexCoord17 ) , tex2DNode1 , temp_output_243_0);
			float4 tex2DNode121 = tex2D( _AlbedoG, uv_TexCoord235 );
			float4 lerpResult145 = lerp( lerpResult133 , tex2DNode121 , temp_output_244_0);
			float4 tex2DNode143 = tex2D( _AlbedoB, uv_TexCoord237 );
			float4 lerpResult160 = lerp( lerpResult145 , tex2DNode143 , temp_output_245_0);
			o.Albedo = lerpResult160.rgb;
			o.Metallic = _Metall;
			float lerpResult179 = lerp( _SmoothStrBase , ( (( _SmoothRoughR )?( ( 1.0 - tex2DNode1.a ) ):( tex2DNode1.a )) * _SmoothStrR ) , temp_output_243_0);
			float lerpResult180 = lerp( lerpResult179 , ( (( _SmoothRoughG )?( ( 1.0 - tex2DNode121.a ) ):( tex2DNode121.a )) * _SmoothStrG ) , temp_output_244_0);
			float lerpResult181 = lerp( lerpResult180 , ( (( _SmoothRoughB )?( ( 1.0 - tex2DNode143.a ) ):( tex2DNode143.a )) * _SmoothStrB ) , temp_output_245_0);
			o.Smoothness = lerpResult181;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19108
Node;AmplifyShaderEditor.RangedFloatNode;232;-2789.423,-1001.38;Float;False;Property;_TilingR;TilingR;10;0;Create;True;0;0;0;False;0;False;1;0.73;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;233;-2601.03,-1018.789;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;101;-1921.646,-2372.312;Float;True;Property;_HeightMapR;HeightMapR-----;0;0;Create;True;0;0;0;False;0;False;None;cc1a5fe039217ab4cb9aa2d112188eed;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RangedFloatNode;234;-2787.006,-832.1543;Float;False;Property;_TilingG;TilingG;20;0;Create;True;0;0;0;False;0;False;1;1.62;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;235;-2598.613,-849.5633;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;124;-1394.554,-2192.583;Float;False;Property;_BlendFactorR;BlendFactorR;9;0;Create;True;0;0;0;False;0;False;0.5;-0.044;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;236;-2787.005,-691.938;Float;False;Property;_TilingB;TilingB;30;0;Create;True;0;0;0;False;0;False;1;0.53;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;204;-1925.689,-2155.094;Float;True;Property;_HeightMapG;HeightMapG-----;11;0;Create;True;0;0;0;False;0;False;None;cc1a5fe039217ab4cb9aa2d112188eed;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SamplerNode;122;-939.8845,-1817.401;Inherit;True;Property;_HeightR;HeightR;3;0;Create;True;0;0;0;False;0;False;-1;None;6ff081746bc7b924c9f1d747cc6d715e;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;205;-1927.448,-1944.12;Float;True;Property;_HeightMapB;HeightMapB----;21;0;Create;True;0;0;0;False;0;False;None;70591861af6aa2f4eb4a7eea5a4ee9c4;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SimpleAddOpNode;141;-562.0522,-1835.714;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;221;-1395.211,-1861.878;Float;False;Property;_BlendFactorG;BlendFactorG;18;0;Create;True;0;0;0;False;0;False;0.5;-0.15;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;144;-928,-1568;Inherit;True;Property;_HeightG;HeightG;7;0;Create;True;0;0;0;False;0;False;-1;None;38f4e32d0db972740b0dd0418b09c79f;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;237;-2598.612,-709.347;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;146;-560,-1648;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;223;-1390.506,-1483.457;Float;False;Property;_BlendFactorB;BlendFactorB;28;0;Create;True;0;0;0;False;0;False;0.5;-0.42;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;142;-154.2122,-1835.429;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;159;-932.8328,-1334.811;Inherit;True;Property;_HeightB;HeightB;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;137;26.49091,-1822.323;Inherit;False;True;False;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;148;-160,-1648;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;153;-553.3851,-1422.542;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;155;-153.3856,-1422.542;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;134;282.41,-1833.819;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;128;-1397.021,-2073.883;Float;False;Property;_BlendContrastR;BlendContrastR;7;0;Create;True;0;0;0;False;0;False;0.5;7.1;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;123;100.0922,-2108.936;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;149;32,-1632;Inherit;False;True;False;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;156;38.61461,-1406.542;Inherit;False;True;False;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.HeightMapBlendNode;132;683.8633,-1838.503;Inherit;False;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;150;288,-1648;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;222;-1393.544,-1760.282;Float;False;Property;_BlendContrastG;BlendContrastG;19;0;Create;True;0;0;0;False;0;False;0.5;6.1;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;-107.9713,-1181.106;Inherit;True;Property;_AlbedoR;AlbedoR;1;0;Create;True;0;0;0;False;0;False;-1;None;478533f0de14d1e42aab544173d3cb69;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;121;-105.0141,-948.1488;Inherit;True;Property;_AlbedoG;AlbedoG;12;0;Create;True;0;0;0;False;0;False;-1;None;7f0da25b493f4e448af2a1541ef65ede;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;18;-1543.365,-1009.928;Float;False;Property;_ParalaxOffsetR;ParalaxOffsetR;6;0;Create;True;0;0;0;False;0;False;0.001;113.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;209;266.6483,40.13718;Inherit;False;FLOAT;1;0;FLOAT;0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleContrastOpNode;127;974.9489,-2331.39;Inherit;False;2;1;COLOR;0,0,0,0;False;0;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;278;-1506.518,-1128.265;Float;False;Constant;_ParalaxOffsetMult;ParalaxOffsetMult;7;0;Create;True;0;0;0;False;0;False;0.001;55.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.HeightMapBlendNode;151;692.6326,-1640.552;Inherit;False;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;157;294.6147,-1422.542;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;224;-1388.839,-1381.861;Float;False;Property;_BlendContrastB;BlendContrastB;29;0;Create;True;0;0;0;False;0;False;0.5;6.5;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleContrastOpNode;147;996.8099,-2155.193;Inherit;False;2;1;COLOR;0,0,0,0;False;0;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;210;410.6485,136.1371;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;67;-2479.907,-1260.483;Float;False;Property;_TilingBase;TilingBase;32;0;Create;True;0;0;0;False;0;False;1;1.23;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;240;1238.869,-2311.769;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;277;-1247.051,-1045.876;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;143;-133.7745,-735.8284;Inherit;True;Property;_AlbedoB;AlbedoB;22;0;Create;True;0;0;0;False;0;False;-1;None;f9bd85ea2601b824c87556c13e852933;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.HeightMapBlendNode;158;699.2474,-1415.094;Inherit;False;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;216;-1541.736,-914.9268;Float;False;Property;_ParalaxOffsetG;ParalaxOffsetG;17;0;Create;True;0;0;0;False;0;False;0.001;117.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;206;282.6483,168.1371;Inherit;False;FLOAT;1;0;FLOAT;0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;63;-1021.79,-1054.79;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;211;570.6487,8.137177;Inherit;False;Property;_SmoothRoughR;Smooth/RoughR;3;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;217;-1535.235,-831.7264;Float;False;Property;_ParalaxOffsetB;ParalaxOffsetB;27;0;Create;True;0;0;0;False;0;False;0.001;79.32;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;184;282.6483,360.1372;Inherit;False;FLOAT;1;0;FLOAT;0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.TextureCoordinatesNode;17;-2291.514,-1277.892;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;282;-274.7284,-456.1165;Float;False;Property;_NormalDepthBase;NormalDepthBase;36;0;Create;True;0;0;0;False;0;False;0.001;0.77;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;273;-1114.202,-698.8446;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;241;1238.871,-2155.733;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;279;-1251.97,-947.5003;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;218;-273.7967,-370.0156;Float;False;Property;_NormalDepthR;NormalDepthR;5;0;Create;True;0;0;0;False;0;False;0.001;0.9;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;246;1449.503,-2371.698;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;229;856.7388,52.77544;Float;False;Property;_SmoothStrR;SmoothStrR;4;0;Create;True;0;0;0;False;0;False;0.001;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleContrastOpNode;154;1003.424,-1962.779;Inherit;False;2;1;COLOR;0,0,0,0;False;0;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;207;426.6486,280.1372;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;186;426.6486,440.1372;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;230;855.3679,214.7764;Float;False;Property;_SmoothStrG;SmoothStrG;16;0;Create;True;0;0;0;False;0;False;0.001;0.9;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;281;359.6766,-1200.097;Inherit;True;Property;_NormalBase;NormalBase;33;0;Create;True;0;0;0;False;0;False;-1;None;bb01cdcfa52466047b4f31ccc511d21a;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;280;-1249.512,-839.2869;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;226;1122.246,-28.66548;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;208;586.6487,152.1371;Inherit;False;Property;_SmoothRoughG;Smooth/RoughG;14;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;214;-1017.175,-937.5512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;283;589.125,-248.1451;Float;False;Property;_SmoothStrBase;SmoothStrBase;35;0;Create;True;0;0;0;False;0;False;0.001;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;152;-319.7717,-2136.899;Inherit;True;Property;_AlbedoBase;AlbedoBase;34;0;Create;True;0;0;0;False;0;False;-1;None;6562a3503e200144ea7b6a9b116dae04;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;243;1794.911,-2346.695;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;247;1453.377,-2180.011;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;242;1238.871,-1994.819;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;274;-823.2021,-1038.845;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;164;370.916,-954.9224;Inherit;True;Property;_NormalMapR;NormalMapR;2;0;Create;True;0;0;0;False;0;False;-1;None;f9271a62663ca5a40ad11ff6e123788b;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;219;-272.1677,-275.0147;Float;False;Property;_NormalDepthG;NormalDepthG;15;0;Create;True;0;0;0;False;0;False;0.001;2.37;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;270;-577.0432,-1144.867;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;275;-818.2021,-923.8446;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;231;853.8688,360.9376;Float;False;Property;_SmoothStrB;SmoothStrB;26;0;Create;True;0;0;0;False;0;False;0.001;0.6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;248;1466.931,-2023.179;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;167;1046.235,-1012.634;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;179;1762.574,-277.8143;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;133;1503.268,-1852.94;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;227;1139.696,100.4715;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;187;586.6487,328.1372;Inherit;False;Property;_SmoothRoughB;Smooth/RoughB;24;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;220;-265.6667,-191.8141;Float;False;Property;_NormalDepthB;NormalDepthB;25;0;Create;True;0;0;0;False;0;False;0.001;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;215;-1011.975,-829.6513;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;244;1786.569,-2235.781;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;269;375.8651,-748.1265;Inherit;True;Property;_NormalMapG;NormalMapG;13;0;Create;True;0;0;0;False;0;False;-1;None;75588e4ad2a17f94e87902876c7db6ff;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;245;1781.569,-2121.781;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;166;397.5298,-517.3137;Inherit;True;Property;_NormalMapB;NormalMapB;23;0;Create;True;0;0;0;False;0;False;-1;None;52bc983b5e932de4897228a8f115db0f;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;145;1512.573,-1613.09;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;180;1830.377,-102.2617;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;276;-807.2021,-804.8446;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;169;1112.155,-812.9184;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;228;1150.167,234.8439;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;271;-567.7381,-905.0176;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;272;-475.7769,-655.4757;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;19;-1740.465,-630.8505;Float;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;170;1218.656,-625.5627;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;200;1659.264,-408.4822;Float;False;Property;_Metall;Metall;31;0;Create;True;0;0;0;False;0;False;0.5;0.443;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;181;1837.801,76.63841;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;238;-639.4146,-2237.85;Float;False;Property;_VertexContrast;VertexContrast-----;8;0;Create;True;0;0;0;False;0;False;0.5;15.56;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;160;1604.534,-1363.548;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LinearToGammaNode;178;42.6487,408.1372;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LinearToGammaNode;177;26.64871,184.1371;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LinearToGammaNode;176;26.64871,-23.86283;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ColorNode;183;-89.1239,-2364.912;Inherit;False;Constant;_Color0;Color 0;24;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;284;2148.618,-1036.723;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;ASE/ASE_StandartVertexBlendTesselated;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;233;0;232;0
WireConnection;235;0;234;0
WireConnection;122;0;101;0
WireConnection;122;1;233;0
WireConnection;141;0;122;0
WireConnection;141;1;124;0
WireConnection;144;0;204;0
WireConnection;144;1;235;0
WireConnection;237;0;236;0
WireConnection;146;0;144;0
WireConnection;146;1;221;0
WireConnection;142;0;141;0
WireConnection;159;0;205;0
WireConnection;159;1;237;0
WireConnection;137;0;142;0
WireConnection;148;0;146;0
WireConnection;153;0;159;0
WireConnection;153;1;223;0
WireConnection;155;0;153;0
WireConnection;134;0;137;0
WireConnection;149;0;148;0
WireConnection;156;0;155;0
WireConnection;132;0;134;0
WireConnection;132;1;123;1
WireConnection;132;2;128;0
WireConnection;150;0;149;0
WireConnection;1;1;233;0
WireConnection;121;1;235;0
WireConnection;209;0;1;4
WireConnection;127;1;132;0
WireConnection;127;0;128;0
WireConnection;151;0;150;0
WireConnection;151;1;123;2
WireConnection;151;2;222;0
WireConnection;157;0;156;0
WireConnection;147;1;151;0
WireConnection;147;0;222;0
WireConnection;210;0;209;0
WireConnection;240;0;127;0
WireConnection;277;0;278;0
WireConnection;277;1;18;0
WireConnection;143;1;237;0
WireConnection;158;0;157;0
WireConnection;158;1;123;3
WireConnection;158;2;224;0
WireConnection;206;0;121;4
WireConnection;63;0;122;1
WireConnection;63;1;277;0
WireConnection;211;0;209;0
WireConnection;211;1;210;0
WireConnection;184;0;143;4
WireConnection;17;0;67;0
WireConnection;241;0;147;0
WireConnection;279;0;278;0
WireConnection;279;1;216;0
WireConnection;246;0;240;0
WireConnection;154;1;158;0
WireConnection;154;0;224;0
WireConnection;207;0;206;0
WireConnection;186;0;184;0
WireConnection;281;1;17;0
WireConnection;281;5;282;0
WireConnection;280;0;278;0
WireConnection;280;1;217;0
WireConnection;226;0;211;0
WireConnection;226;1;229;0
WireConnection;208;0;206;0
WireConnection;208;1;207;0
WireConnection;214;0;144;1
WireConnection;214;1;279;0
WireConnection;152;1;17;0
WireConnection;243;0;246;0
WireConnection;247;0;241;0
WireConnection;242;0;154;0
WireConnection;274;0;63;0
WireConnection;274;1;273;0
WireConnection;164;1;233;0
WireConnection;164;5;218;0
WireConnection;270;1;274;0
WireConnection;270;2;132;0
WireConnection;275;0;214;0
WireConnection;275;1;273;0
WireConnection;248;0;242;0
WireConnection;167;0;281;0
WireConnection;167;1;164;0
WireConnection;167;2;243;0
WireConnection;179;0;283;0
WireConnection;179;1;226;0
WireConnection;179;2;243;0
WireConnection;133;0;152;0
WireConnection;133;1;1;0
WireConnection;133;2;243;0
WireConnection;227;0;208;0
WireConnection;227;1;230;0
WireConnection;187;0;184;0
WireConnection;187;1;186;0
WireConnection;215;0;159;1
WireConnection;215;1;280;0
WireConnection;244;0;247;0
WireConnection;269;1;235;0
WireConnection;269;5;219;0
WireConnection;245;0;248;0
WireConnection;166;1;237;0
WireConnection;166;5;220;0
WireConnection;145;0;133;0
WireConnection;145;1;121;0
WireConnection;145;2;244;0
WireConnection;180;0;179;0
WireConnection;180;1;227;0
WireConnection;180;2;244;0
WireConnection;276;0;215;0
WireConnection;276;1;273;0
WireConnection;169;0;167;0
WireConnection;169;1;269;0
WireConnection;169;2;244;0
WireConnection;228;0;187;0
WireConnection;228;1;231;0
WireConnection;271;0;270;0
WireConnection;271;1;275;0
WireConnection;271;2;151;0
WireConnection;272;0;271;0
WireConnection;272;1;276;0
WireConnection;272;2;158;0
WireConnection;170;0;169;0
WireConnection;170;1;166;0
WireConnection;170;2;245;0
WireConnection;181;0;180;0
WireConnection;181;1;228;0
WireConnection;181;2;245;0
WireConnection;160;0;145;0
WireConnection;160;1;143;0
WireConnection;160;2;245;0
WireConnection;178;0;143;4
WireConnection;177;0;121;4
WireConnection;176;0;1;4
WireConnection;284;0;160;0
WireConnection;284;1;170;0
WireConnection;284;3;200;0
WireConnection;284;4;181;0
ASEEND*/
//CHKSM=8BC6AB52EB090B1648FABD11F3E54A58E9902101