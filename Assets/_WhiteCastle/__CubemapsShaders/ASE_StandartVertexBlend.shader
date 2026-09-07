// Made with Amplify Shader Editor v1.9.1.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "ASE/ASE_StandartVertexBlend"
{
	Properties
	{
		_HeightMapR("HeightMapR-----", 2D) = "white" {}
		_AlbedoR("AlbedoR", 2D) = "white" {}
		_NormalMapR("NormalMapR", 2D) = "bump" {}
		_SmoothR("SmoothR", 2D) = "white" {}
		[Toggle]_SmoothRoughR("Smooth/RoughR", Float) = 0
		_SmoothStrR("SmoothStrR", Float) = 0.001
		_NormalDepthR("NormalDepthR", Float) = 0.001
		_ParalaxOffsetR("ParalaxOffsetR", Float) = 0.001
		_BlendContrastR("BlendContrastR", Range( 0 , 20)) = 0.5
		_BlendFactorR("BlendFactorR", Range( -1 , 1)) = 0.5
		_TilingR("TilingR", Float) = 1
		_HeightMapG("HeightMapG-----", 2D) = "white" {}
		_AlbedoG("AlbedoG", 2D) = "white" {}
		_NormalMapG("NormalMapG", 2D) = "bump" {}
		_SmoothG("SmoothG", 2D) = "white" {}
		[Toggle]_SmoothRoughG("Smooth/RoughG", Float) = 0
		_NormalDepthG("NormalDepthG", Float) = 0.001
		_SmoothStrG("SmoothStrG", Float) = 0.001
		_ParalaxOffsetG("ParalaxOffsetG", Float) = 0.001
		_BlendFactorG("BlendFactorG", Range( -1 , 1)) = 0.5
		_BlendContrastG("BlendContrastG", Range( 0 , 20)) = 0.5
		_TilingG("TilingG", Float) = 1
		_HeightMapB("HeightMapB----", 2D) = "white" {}
		_AlbedoB("AlbedoB", 2D) = "white" {}
		_NormalMapB("NormalMapB", 2D) = "bump" {}
		_SmoothB("SmoothB", 2D) = "white" {}
		[Toggle]_SmoothRoughB("Smooth/RoughB", Float) = 0
		_NormalDepthB("NormalDepthB", Float) = 0.001
		_SmoothStrB("SmoothStrB", Float) = 0.001
		_ParalaxOffsetB("ParalaxOffsetB", Float) = 0.001
		_BlendFactorB("BlendFactorB", Range( -1 , 1)) = 0.5
		_BlendContrastB("BlendContrastB", Range( 0 , 20)) = 0.5
		_TilingB("TilingB", Float) = 1
		_Metall("Metall", Range( 0 , 1)) = 0.5
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		CGINCLUDE
		#include "UnityStandardUtils.cginc"
		#include "UnityCG.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 viewDir;
			INTERNAL_DATA
			float3 worldNormal;
			float3 worldPos;
			float4 vertexColor : COLOR;
		};

		uniform sampler2D _NormalMapR;
		uniform float _TilingR;
		uniform sampler2D _HeightMapR;
		uniform float _ParalaxOffsetR;
		uniform float4 _HeightMapR_ST;
		uniform float _NormalDepthR;
		uniform sampler2D _NormalMapG;
		uniform float _TilingG;
		uniform sampler2D _HeightMapG;
		uniform float _ParalaxOffsetG;
		uniform float4 _HeightMapG_ST;
		uniform float _NormalDepthG;
		uniform float _BlendContrastG;
		uniform float _BlendFactorG;
		uniform sampler2D _NormalMapB;
		uniform float _TilingB;
		uniform sampler2D _HeightMapB;
		uniform float _ParalaxOffsetB;
		uniform float4 _HeightMapB_ST;
		uniform float _NormalDepthB;
		uniform float _BlendContrastB;
		uniform float _BlendFactorB;
		uniform sampler2D _AlbedoR;
		uniform float _BlendContrastR;
		uniform float _BlendFactorR;
		uniform sampler2D _AlbedoG;
		uniform sampler2D _AlbedoB;
		uniform float _Metall;
		uniform float _SmoothRoughR;
		uniform sampler2D _SmoothR;
		uniform float _SmoothStrR;
		uniform float _SmoothRoughG;
		uniform sampler2D _SmoothG;
		uniform float _SmoothStrG;
		uniform float _SmoothRoughB;
		uniform sampler2D _SmoothB;
		uniform float _SmoothStrB;


		inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, float parallax, float refPlane, float2 tilling, float2 curv, int index )
		{
			float3 result = 0;
			int stepIndex = 0;
			int numSteps = ( int )lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) );
			float layerHeight = 1.0 / numSteps;
			float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
			uvs.xy += refPlane * plane;
			float2 deltaTex = -plane * layerHeight;
			float2 prevTexOffset = 0;
			float prevRayZ = 1.0f;
			float prevHeight = 0.0f;
			float2 currTexOffset = deltaTex;
			float currRayZ = 1.0f - layerHeight;
			float currHeight = 0.0f;
			float intersection = 0;
			float2 finalTexOffset = 0;
			while ( stepIndex < numSteps + 1 )
			{
			 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).r;
			 	if ( currHeight > currRayZ )
			 	{
			 	 	stepIndex = numSteps + 1;
			 	}
			 	else
			 	{
			 	 	stepIndex++;
			 	 	prevTexOffset = currTexOffset;
			 	 	prevRayZ = currRayZ;
			 	 	prevHeight = currHeight;
			 	 	currTexOffset += deltaTex;
			 	 	currRayZ -= layerHeight;
			 	}
			}
			int sectionSteps = 8;
			int sectionIndex = 0;
			float newZ = 0;
			float newHeight = 0;
			while ( sectionIndex < sectionSteps )
			{
			 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
			 	finalTexOffset = prevTexOffset + intersection * deltaTex;
			 	newZ = prevRayZ - intersection * layerHeight;
			 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).r;
			 	if ( newHeight > newZ )
			 	{
			 	 	currTexOffset = finalTexOffset;
			 	 	currHeight = newHeight;
			 	 	currRayZ = newZ;
			 	 	deltaTex = intersection * deltaTex;
			 	 	layerHeight = intersection * layerHeight;
			 	}
			 	else
			 	{
			 	 	prevTexOffset = finalTexOffset;
			 	 	prevHeight = newHeight;
			 	 	prevRayZ = newZ;
			 	 	deltaTex = ( 1 - intersection ) * deltaTex;
			 	 	layerHeight = ( 1 - intersection ) * layerHeight;
			 	}
			 	sectionIndex++;
			}
			return uvs.xy + finalTexOffset;
		}


		float4 CalculateContrast( float contrastValue, float4 colorTarget )
		{
			float t = 0.5 * ( 1.0 - contrastValue );
			return mul( float4x4( contrastValue,0,0,t, 0,contrastValue,0,t, 0,0,contrastValue,t, 0,0,0,1 ), colorTarget );
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 temp_cast_0 = (_TilingR).xx;
			float2 uv_TexCoord233 = i.uv_texcoord * temp_cast_0;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float2 OffsetPOM98 = POM( _HeightMapR, uv_TexCoord233, ddx(uv_TexCoord233), ddy(uv_TexCoord233), ase_worldNormal, ase_worldViewDir, i.viewDir, 16, 16, ( 0.001 * _ParalaxOffsetR ), 0.5, _HeightMapR_ST.xy, float2(0,0), 0 );
			float3 tex2DNode164 = UnpackScaleNormal( tex2D( _NormalMapR, OffsetPOM98 ), _NormalDepthR );
			float2 temp_cast_1 = (_TilingG).xx;
			float2 uv_TexCoord235 = i.uv_texcoord * temp_cast_1;
			float2 OffsetPOM212 = POM( _HeightMapG, uv_TexCoord235, ddx(uv_TexCoord235), ddy(uv_TexCoord235), ase_worldNormal, ase_worldViewDir, i.viewDir, 16, 16, ( 0.001 * _ParalaxOffsetG ), 0.5, _HeightMapG_ST.xy, float2(0,0), 0 );
			float4 clampResult148 = clamp( ( tex2D( _HeightMapG, OffsetPOM212 ) + _BlendFactorG ) , float4( 0,0,0,0 ) , float4( 1,0,0,0 ) );
			float clampResult150 = clamp( (clampResult148).r , 0.0 , 1.0 );
			float HeightMask151 = saturate(pow(((clampResult150*i.vertexColor.g)*4)+(i.vertexColor.g*2),_BlendContrastG));
			float4 temp_cast_2 = (HeightMask151).xxxx;
			float clampResult247 = clamp( CalculateContrast(_BlendContrastG,temp_cast_2).r , 0.0 , 1.0 );
			float temp_output_244_0 = ( clampResult247 * 1.0 );
			float3 lerpResult169 = lerp( tex2DNode164 , UnpackScaleNormal( tex2D( _NormalMapG, OffsetPOM212 ), _NormalDepthG ) , temp_output_244_0);
			float2 temp_cast_3 = (_TilingB).xx;
			float2 uv_TexCoord237 = i.uv_texcoord * temp_cast_3;
			float2 OffsetPOM213 = POM( _HeightMapB, uv_TexCoord237, ddx(uv_TexCoord237), ddy(uv_TexCoord237), ase_worldNormal, ase_worldViewDir, i.viewDir, 16, 16, ( 0.001 * _ParalaxOffsetB ), 0.5, _HeightMapB_ST.xy, float2(0,0), 0 );
			float4 clampResult155 = clamp( ( tex2D( _HeightMapB, OffsetPOM213 ) + _BlendFactorB ) , float4( 0,0,0,0 ) , float4( 1,0,0,0 ) );
			float clampResult157 = clamp( (clampResult155).r , 0.0 , 1.0 );
			float HeightMask158 = saturate(pow(((clampResult157*i.vertexColor.b)*4)+(i.vertexColor.b*2),_BlendContrastB));
			float4 temp_cast_4 = (HeightMask158).xxxx;
			float clampResult248 = clamp( CalculateContrast(_BlendContrastB,temp_cast_4).r , 0.0 , 1.0 );
			float temp_output_245_0 = ( clampResult248 * 1.0 );
			float3 lerpResult170 = lerp( lerpResult169 , UnpackScaleNormal( tex2D( _NormalMapB, OffsetPOM213 ), _NormalDepthB ) , temp_output_245_0);
			o.Normal = lerpResult170;
			float4 color183 = IsGammaSpace() ? float4(0,0,0,1) : float4(0,0,0,1);
			float4 tex2DNode122 = tex2D( _HeightMapR, OffsetPOM98 );
			float4 clampResult142 = clamp( ( tex2DNode122 + _BlendFactorR ) , float4( 0,0,0,0 ) , float4( 1,0,0,0 ) );
			float clampResult134 = clamp( (clampResult142).r , 0.0 , 1.0 );
			float HeightMask132 = saturate(pow(((clampResult134*i.vertexColor.r)*4)+(i.vertexColor.r*2),_BlendContrastR));
			float4 temp_cast_5 = (HeightMask132).xxxx;
			float clampResult246 = clamp( CalculateContrast(_BlendContrastR,temp_cast_5).r , 0.0 , 1.0 );
			float temp_output_243_0 = ( clampResult246 * 1.0 );
			float4 lerpResult133 = lerp( color183 , tex2D( _AlbedoR, OffsetPOM98 ) , temp_output_243_0);
			float4 lerpResult145 = lerp( lerpResult133 , tex2D( _AlbedoG, OffsetPOM212 ) , temp_output_244_0);
			float4 lerpResult160 = lerp( lerpResult145 , tex2D( _AlbedoB, OffsetPOM213 ) , temp_output_245_0);
			o.Albedo = lerpResult160.rgb;
			o.Metallic = _Metall;
			float4 color185 = IsGammaSpace() ? float4(0,0,0,1) : float4(0,0,0,1);
			float3 temp_cast_7 = (color185.r).xxx;
			float3 temp_cast_8 = (color185.r).xxx;
			float3 linearToGamma175 = LinearToGammaSpace( temp_cast_8 );
			float3 linearToGamma176 = LinearToGammaSpace( tex2D( _SmoothR, OffsetPOM98 ).rgb );
			float3 temp_cast_12 = (( (( _SmoothRoughR )?( ( 1.0 - linearToGamma176.x ) ):( linearToGamma176.x )) * _SmoothStrR )).xxx;
			float3 lerpResult179 = lerp( linearToGamma175 , temp_cast_12 , temp_output_243_0);
			float3 linearToGamma177 = LinearToGammaSpace( tex2D( _SmoothG, OffsetPOM212 ).rgb );
			float3 temp_cast_16 = (( (( _SmoothRoughG )?( ( 1.0 - linearToGamma177.x ) ):( linearToGamma177.x )) * _SmoothStrG )).xxx;
			float3 lerpResult180 = lerp( lerpResult179 , temp_cast_16 , temp_output_244_0);
			float3 linearToGamma178 = LinearToGammaSpace( tex2D( _SmoothB, OffsetPOM213 ).rgb );
			float3 temp_cast_20 = (( (( _SmoothRoughB )?( ( 1.0 - linearToGamma178.x ) ):( linearToGamma178.x )) * _SmoothStrB )).xxx;
			float3 lerpResult181 = lerp( lerpResult180 , temp_cast_20 , temp_output_245_0);
			o.Smoothness = lerpResult181.x;
			o.Alpha = 1;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				half4 color : COLOR0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				o.color = v.color;
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.viewDir = IN.tSpace0.xyz * worldViewDir.x + IN.tSpace1.xyz * worldViewDir.y + IN.tSpace2.xyz * worldViewDir.z;
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				surfIN.vertexColor = IN.color;
				SurfaceOutputStandard o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandard, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19108
Node;AmplifyShaderEditor.RangedFloatNode;64;-1927.174,-793.9301;Float;False;Constant;_ParalaxDepthCorrection;ParalaxDepthCorrection;20;0;Create;True;0;0;0;False;0;False;0.001;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;18;-1913.874,-713.0233;Float;False;Property;_ParalaxOffsetR;ParalaxOffsetR;7;0;Create;True;0;0;0;False;0;False;0.001;0.62;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;232;-2789.423,-1001.38;Float;False;Property;_TilingR;TilingR;11;0;Create;True;0;0;0;False;0;False;1;0.73;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;234;-2787.006,-832.1543;Float;False;Property;_TilingG;TilingG;22;0;Create;True;0;0;0;False;0;False;1;1.62;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;101;-1921.646,-2372.312;Float;True;Property;_HeightMapR;HeightMapR-----;0;0;Create;True;0;0;0;False;0;False;None;cc1a5fe039217ab4cb9aa2d112188eed;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.TextureCoordinatesNode;233;-2601.03,-1018.789;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;216;-1912.245,-618.0224;Float;False;Property;_ParalaxOffsetG;ParalaxOffsetG;19;0;Create;True;0;0;0;False;0;False;0.001;64.52;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;19;-1640.134,-954.9971;Float;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;63;-1565.86,-775.5615;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOcclusionMappingNode;98;-992.1188,-946.053;Inherit;False;0;16;False;;64;False;;8;0.02;0.5;False;1,1;False;0,0;8;0;FLOAT2;0,0;False;1;SAMPLER2D;;False;7;SAMPLERSTATE;;False;2;FLOAT;0.02;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT2;0,0;False;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;214;-1561.245,-658.322;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;217;-1905.744,-534.822;Float;False;Property;_ParalaxOffsetB;ParalaxOffsetB;30;0;Create;True;0;0;0;False;0;False;0.001;8.82;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;235;-2598.613,-849.5633;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexturePropertyNode;204;-1925.689,-2155.094;Float;True;Property;_HeightMapG;HeightMapG-----;12;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RangedFloatNode;236;-2787.005,-691.938;Float;False;Property;_TilingB;TilingB;33;0;Create;True;0;0;0;False;0;False;1;0.77;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOcclusionMappingNode;212;-991.6548,-725.5938;Inherit;False;0;16;False;;64;False;;8;0.02;0.5;False;1,1;False;0,0;8;0;FLOAT2;0,0;False;1;SAMPLER2D;;False;7;SAMPLERSTATE;;False;2;FLOAT;0.02;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT2;0,0;False;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;205;-1927.448,-1944.12;Float;True;Property;_HeightMapB;HeightMapB----;23;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;215;-1556.045,-550.4221;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;237;-2598.612,-709.347;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;122;-939.8845,-1817.401;Inherit;True;Property;_HeightR;HeightR;3;0;Create;True;0;0;0;False;0;False;-1;None;6ff081746bc7b924c9f1d747cc6d715e;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;124;-1394.554,-2192.583;Float;False;Property;_BlendFactorR;BlendFactorR;10;0;Create;True;0;0;0;False;0;False;0.5;-0.044;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOcclusionMappingNode;213;-993.2509,-512.7945;Inherit;False;0;16;False;;64;False;;10;0.02;0.5;False;1,1;False;0,0;8;0;FLOAT2;0,0;False;1;SAMPLER2D;;False;7;SAMPLERSTATE;;False;2;FLOAT;0.02;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT2;0,0;False;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;141;-562.0522,-1835.714;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;221;-1395.211,-1861.878;Float;False;Property;_BlendFactorG;BlendFactorG;20;0;Create;True;0;0;0;False;0;False;0.5;-0.31;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;144;-928,-1568;Inherit;True;Property;_HeightG;HeightG;7;0;Create;True;0;0;0;False;0;False;-1;None;38f4e32d0db972740b0dd0418b09c79f;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;223;-1390.506,-1483.457;Float;False;Property;_BlendFactorB;BlendFactorB;31;0;Create;True;0;0;0;False;0;False;0.5;-0.03;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;146;-560,-1648;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;159;-932.8328,-1334.811;Inherit;True;Property;_HeightB;HeightB;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;142;-154.2122,-1835.429;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;148;-160,-1648;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;153;-553.3851,-1422.542;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ComponentMaskNode;137;26.49091,-1822.323;Inherit;False;True;False;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;128;-1397.021,-2073.883;Float;False;Property;_BlendContrastR;BlendContrastR;8;0;Create;True;0;0;0;False;0;False;0.5;7.1;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;134;282.41,-1833.819;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;149;32,-1632;Inherit;False;True;False;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;155;-153.3856,-1422.542;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.VertexColorNode;123;100.0922,-2108.936;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;171;-464.2532,-133.1389;Inherit;True;Property;_SmoothR;SmoothR;3;0;Create;True;0;0;0;False;0;False;-1;None;3187605d8bde2db4a9e187183ee9ed87;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;156;38.61461,-1406.542;Inherit;False;True;False;False;False;1;0;COLOR;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;172;-525.7127,141.6703;Inherit;True;Property;_SmoothG;SmoothG;15;0;Create;True;0;0;0;False;0;False;-1;None;1679150b85e79914498cfe90857fa7ef;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.HeightMapBlendNode;132;683.8633,-1838.503;Inherit;False;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LinearToGammaNode;176;26.64871,-23.86283;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;150;288,-1648;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;222;-1393.544,-1760.282;Float;False;Property;_BlendContrastG;BlendContrastG;21;0;Create;True;0;0;0;False;0;False;0.5;6.1;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;224;-1388.839,-1381.861;Float;False;Property;_BlendContrastB;BlendContrastB;32;0;Create;True;0;0;0;False;0;False;0.5;20;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.LinearToGammaNode;177;26.64871,184.1371;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;173;-525.7127,365.6703;Inherit;True;Property;_SmoothB;SmoothB;26;0;Create;True;0;0;0;False;0;False;-1;None;adb6862a03027e24e876d96a0e648727;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.HeightMapBlendNode;151;692.6326,-1640.552;Inherit;False;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;157;294.6147,-1422.542;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleContrastOpNode;127;974.9489,-2331.39;Inherit;False;2;1;COLOR;0,0,0,0;False;0;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.BreakToComponentsNode;209;266.6483,40.13718;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.HeightMapBlendNode;158;699.2474,-1415.094;Inherit;False;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;206;282.6483,168.1371;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.LinearToGammaNode;178;42.6487,408.1372;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OneMinusNode;210;410.6485,136.1371;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;240;1238.869,-2311.769;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SimpleContrastOpNode;147;996.8099,-2155.193;Inherit;False;2;1;COLOR;0,0,0,0;False;0;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.BreakToComponentsNode;241;1238.871,-2155.733;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;229;856.7388,52.77544;Float;False;Property;_SmoothStrR;SmoothStrR;5;0;Create;True;0;0;0;False;0;False;0.001;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleContrastOpNode;154;1003.424,-1962.779;Inherit;False;2;1;COLOR;0,0,0,0;False;0;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.BreakToComponentsNode;184;282.6483,360.1372;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ColorNode;185;347.4311,-264.2567;Inherit;False;Constant;_BaseGloss;BaseGloss;24;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;207;426.6486,280.1372;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;211;570.6487,8.137177;Inherit;False;Property;_SmoothRoughR;Smooth/RoughR;4;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;246;1449.503,-2371.698;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;183;-89.1239,-2364.912;Inherit;False;Constant;_Color0;Color 0;24;0;Create;True;0;0;0;False;0;False;0,0,0,1;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;247;1453.377,-2180.011;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;208;586.6487,152.1371;Inherit;False;Property;_SmoothRoughG;Smooth/RoughG;16;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;218;-268.3976,-528.3894;Float;False;Property;_NormalDepthR;NormalDepthR;6;0;Create;True;0;0;0;False;0;False;0.001;0.82;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;242;1238.871,-1994.819;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.OneMinusNode;186;426.6486,440.1372;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LinearToGammaNode;175;968.4748,-264.049;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;243;1794.911,-2346.695;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;-107.9713,-1181.106;Inherit;True;Property;_AlbedoR;AlbedoR;1;0;Create;True;0;0;0;False;0;False;-1;None;478533f0de14d1e42aab544173d3cb69;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;230;855.3679,214.7764;Float;False;Property;_SmoothStrG;SmoothStrG;18;0;Create;True;0;0;0;False;0;False;0.001;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;219;-266.7686,-433.3884;Float;False;Property;_NormalDepthG;NormalDepthG;17;0;Create;True;0;0;0;False;0;False;0.001;1.26;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;226;1122.246,-28.66548;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;248;1466.931,-2023.179;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;179;1762.574,-277.8143;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;220;-260.2676,-350.188;Float;False;Property;_NormalDepthB;NormalDepthB;28;0;Create;True;0;0;0;False;0;False;0.001;1.17;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ToggleSwitchNode;187;586.6487,328.1372;Inherit;False;Property;_SmoothRoughB;Smooth/RoughB;27;0;Create;True;0;0;0;False;0;False;0;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;164;388.56,-965.9499;Inherit;True;Property;_NormalMapR;NormalMapR;2;0;Create;True;0;0;0;False;0;False;-1;None;f9271a62663ca5a40ad11ff6e123788b;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;269;375.8651,-748.1265;Inherit;True;Property;_NormalMapG;NormalMapG;14;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;133;1503.268,-1852.94;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;244;1786.569,-2235.781;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;231;853.8688,360.9376;Float;False;Property;_SmoothStrB;SmoothStrB;29;0;Create;True;0;0;0;False;0;False;0.001;0.13;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;121;-105.0141,-948.1488;Inherit;True;Property;_AlbedoG;AlbedoG;13;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;227;1139.696,100.4715;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;143;-133.7745,-735.8284;Inherit;True;Property;_AlbedoB;AlbedoB;24;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;180;1830.377,-102.2617;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;169;1112.155,-812.9184;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;245;1781.569,-2121.781;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;145;1512.573,-1613.09;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;166;397.5298,-517.3137;Inherit;True;Property;_NormalMapB;NormalMapB;25;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;228;1150.167,234.8439;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;160;1604.534,-1363.548;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;170;1218.656,-625.5627;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;268;-1558.071,-378.7976;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;17;-2291.514,-1277.892;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;67;-2479.907,-1260.483;Float;False;Property;_TilingBase;TilingBase;35;0;Create;True;0;0;0;False;0;False;1;0.79;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;199;2236.151,173.7634;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.LerpOp;181;1837.801,76.63841;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;238;-639.4146,-2237.85;Float;False;Property;_VertexContrast;VertexContrast-----;9;0;Create;True;0;0;0;False;0;False;0.5;15.56;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;251;-2170.582,-396.1133;Inherit;True;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;266;-1726.65,-369.1553;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;250;-2175.212,-666.5909;Inherit;True;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;249;-2227.162,-917.8525;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;167;1046.235,-1012.634;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;270;2375.397,-574.1384;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;ASE/ASE_StandartVertexBlend;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
Node;AmplifyShaderEditor.RangedFloatNode;200;1477.961,-425.1325;Float;False;Property;_Metall;Metall;34;0;Create;True;0;0;0;False;0;False;0.5;0.436;0;1;0;1;FLOAT;0
WireConnection;233;0;232;0
WireConnection;63;0;64;0
WireConnection;63;1;18;0
WireConnection;98;0;233;0
WireConnection;98;1;101;0
WireConnection;98;2;63;0
WireConnection;98;3;19;0
WireConnection;214;0;64;0
WireConnection;214;1;216;0
WireConnection;235;0;234;0
WireConnection;212;0;235;0
WireConnection;212;1;204;0
WireConnection;212;2;214;0
WireConnection;212;3;19;0
WireConnection;215;0;64;0
WireConnection;215;1;217;0
WireConnection;237;0;236;0
WireConnection;122;0;101;0
WireConnection;122;1;98;0
WireConnection;213;0;237;0
WireConnection;213;1;205;0
WireConnection;213;2;215;0
WireConnection;213;3;19;0
WireConnection;141;0;122;0
WireConnection;141;1;124;0
WireConnection;144;0;204;0
WireConnection;144;1;212;0
WireConnection;146;0;144;0
WireConnection;146;1;221;0
WireConnection;159;0;205;0
WireConnection;159;1;213;0
WireConnection;142;0;141;0
WireConnection;148;0;146;0
WireConnection;153;0;159;0
WireConnection;153;1;223;0
WireConnection;137;0;142;0
WireConnection;134;0;137;0
WireConnection;149;0;148;0
WireConnection;155;0;153;0
WireConnection;171;1;98;0
WireConnection;156;0;155;0
WireConnection;172;1;212;0
WireConnection;132;0;134;0
WireConnection;132;1;123;1
WireConnection;132;2;128;0
WireConnection;176;0;171;0
WireConnection;150;0;149;0
WireConnection;177;0;172;0
WireConnection;173;1;213;0
WireConnection;151;0;150;0
WireConnection;151;1;123;2
WireConnection;151;2;222;0
WireConnection;157;0;156;0
WireConnection;127;1;132;0
WireConnection;127;0;128;0
WireConnection;209;0;176;0
WireConnection;158;0;157;0
WireConnection;158;1;123;3
WireConnection;158;2;224;0
WireConnection;206;0;177;0
WireConnection;178;0;173;0
WireConnection;210;0;209;0
WireConnection;240;0;127;0
WireConnection;147;1;151;0
WireConnection;147;0;222;0
WireConnection;241;0;147;0
WireConnection;154;1;158;0
WireConnection;154;0;224;0
WireConnection;184;0;178;0
WireConnection;207;0;206;0
WireConnection;211;0;209;0
WireConnection;211;1;210;0
WireConnection;246;0;240;0
WireConnection;247;0;241;0
WireConnection;208;0;206;0
WireConnection;208;1;207;0
WireConnection;242;0;154;0
WireConnection;186;0;184;0
WireConnection;175;0;185;1
WireConnection;243;0;246;0
WireConnection;1;1;98;0
WireConnection;226;0;211;0
WireConnection;226;1;229;0
WireConnection;248;0;242;0
WireConnection;179;0;175;0
WireConnection;179;1;226;0
WireConnection;179;2;243;0
WireConnection;187;0;184;0
WireConnection;187;1;186;0
WireConnection;164;1;98;0
WireConnection;164;5;218;0
WireConnection;269;1;212;0
WireConnection;269;5;219;0
WireConnection;133;0;183;0
WireConnection;133;1;1;0
WireConnection;133;2;243;0
WireConnection;244;0;247;0
WireConnection;121;1;212;0
WireConnection;227;0;208;0
WireConnection;227;1;230;0
WireConnection;143;1;213;0
WireConnection;180;0;179;0
WireConnection;180;1;227;0
WireConnection;180;2;244;0
WireConnection;169;0;164;0
WireConnection;169;1;269;0
WireConnection;169;2;244;0
WireConnection;245;0;248;0
WireConnection;145;0;133;0
WireConnection;145;1;121;0
WireConnection;145;2;244;0
WireConnection;166;1;213;0
WireConnection;166;5;220;0
WireConnection;228;0;187;0
WireConnection;228;1;231;0
WireConnection;160;0;145;0
WireConnection;160;1;143;0
WireConnection;160;2;245;0
WireConnection;170;0;169;0
WireConnection;170;1;166;0
WireConnection;170;2;245;0
WireConnection;268;0;64;0
WireConnection;268;1;266;0
WireConnection;17;0;67;0
WireConnection;181;0;180;0
WireConnection;181;1;228;0
WireConnection;181;2;245;0
WireConnection;251;0;250;0
WireConnection;251;1;237;0
WireConnection;251;2;158;0
WireConnection;266;0;18;0
WireConnection;266;1;251;0
WireConnection;250;0;233;0
WireConnection;250;1;235;0
WireConnection;250;2;151;0
WireConnection;249;1;122;1
WireConnection;249;2;132;0
WireConnection;167;1;164;0
WireConnection;167;2;243;0
WireConnection;270;0;160;0
WireConnection;270;1;170;0
WireConnection;270;3;200;0
WireConnection;270;4;181;0
ASEEND*/
//CHKSM=41EA8E0B53132C7E4A87EEDD109BF3F4E58A276F