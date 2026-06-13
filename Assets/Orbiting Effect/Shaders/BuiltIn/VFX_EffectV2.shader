Shader "VFX/VFX_EffectV2"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] 
        _Cull("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend("src Blend",int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend("DstBlend",int) = 10
    //-----------------------------------------------------------------------------
        [Toggle(_USEPARTICLECUSTOMDATA)]_USEPARTICLECUSTOMDATA("UseParticleCustomData", Float) = 1
    //-----------------------------------------------------------------------------
        [Header(VertexOffsetPart)]
        [Toggle(_USEVERTEXOFFSETPART)]_USEVERTEXOFFSETPART("UseVertexOffsetPart", Float) = 0
        _OffsetLength("OffsetLength", Float) = 0
        _VertexOffsetTexUseA2R("VertexOffsetTexUseA2R", Range(0, 1)) = 0
        [NoScaleOffset]_VertexOffsetTex("VertexOffsetTex", 2D) = "white" {}
        _VertexOffsetTexTillingOffset("VertexOffsetTexTillingOffset", Vector) = (1, 1, 0, 0)
        _VertexOffsetTexMoveSpeed("VertexOffsetTexMoveSpeed", Vector) = (0, 0, 0, 0)

        [Header(Remap)]
        [Toggle(_USEREMAPPART)]_USEREMAPPART("UseRemapPart", Float) = 1
        _RemapTexRotate("RemapTexRotate", Range(0, 1)) = 0
        [NoScaleOffset]_RemapColTex("RemapColTex", 2D) = "white" {}
        _RemapColTexTillingOffset("RemapColTexTillingOffset", Vector) = (1, 1, 0, 0)
        _RemapColTexMoveSpeed("RemapColTexMoveSpeed", Vector) = (0, 0, 0, 0)
       
        [Header(MainTex)]
        _MainTexRotate("MainTexRotate", Range(0, 1)) = 0
        _MainTexAlphaByA2R("MainTexAlphaByA2R", Range(0, 1)) = 0
        [HDR]_MainColor("MainColor", Color) = (1, 1, 1, 1)
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
        _MainTexTillingOffset("MainTexTillingOffset", Vector) = (1, 1, 0, 0)
        _MainTexMoveSpeed("MainTexSpped", Vector) = (0, 0, 0, 0)
        _MainTexWarpStrength("MainTexWarpStrength", Vector) = (0, 0, 0, 0)

        [Header(AlphaPart)]
        [Toggle(_USEALPHAPART)]_USEALPHAPART("UseAlphaPart", Float) = 1        
        _AlphaTexAlphaByA2R("AlphaTexAlphaByA2R", Range(0, 1)) = 0
        [NoScaleOffset]_AlphaTex("AlphaTex", 2D) = "white" {}
        _AlphaTexTillingOffset("AlphaTexTillingOffset", Vector) = (1, 1, 0, 0)
        _AlphaTexMoveSpeed("AlphaTexMoveSpeed", Vector) = (0, 0, 0, 0)
        _AlphaTexWarpStrength("AlphaTexWarpStrength", Vector) = (0, 0, 0, 0)

        [Header(DissolvePart)]
        [Toggle(_USEDISSOLVEPART)]_USEDISSOLVEPART("UseDissolvePart", Float) = 1
        _DissolveTexAlphaByA2R("DissolveTexAlphaByA2R", Range(0, 1)) = 0
        [NoScaleOffset]_DissolveTex("DissolveTex", 2D) = "white" {}
        _DissolveTexTillingOffset("DissolveTexTillingOffset", Vector) = (1, 1, 0, 0)
        _DissolveTexSpeed("DissolveTexSpeed", Vector) = (0, 0, 0, 0)
        _DissolveTexWarpStrength("DissolveTexWarpStrength", Vector) = (0, 0, 0, 0)
        _DissolveMaxCountMul("DissolveMaxCountMul", Float) = 1
        _DissolveStep("DissolveStep", Range(0, 1)) = 0
        [HDR]_DissolveEdgeCol("DissolveEdgeCol", Color) = (1, 1, 1, 1)
        _DissolveEdgeWidth("DissolveEdgeWidth", Range(0, 1)) = 0

        [Header(WarpPart)]
        [Toggle(_USEWARPPART)]_USEWARPPART("USEWARPPART", Float) = 1
        _WarpTexAlphaByA2R("WarpTexAlphaByA2R", Range(0, 1)) = 0
        [NoScaleOffset]_WarpTex("WarpTex", 2D) = "white" {}
        _WarpTexTillingOffset("WarpTexTillingOffset", Vector) = (1, 1, 0, 0)
        _WarpTexMoveSpeed("WarpTexMoveSpeed", Vector) = (0, 0, 0, 0)

        [Header(FresnelPart)]
        [Toggle(_USEALPHAMULFRESNEL)]_USEALPHAMULFRESNEL("UseAlphaMulFresnel", Float) = 0
        _UseAlphaMulLength("UseAlphaMulLength", Range(0, 1)) = 1
        _Front2Back("Front2Back", Range(0, 1)) = 0
        _FPower("FPower", Range(0.1, 10)) = 1
        _FEmissionLength("FEmissionLength", Range(0, 1)) = 0
        _FEmssionFront2Back("FEmssionFront2Back", Range(0, 1)) = 0
        _FEmissionPower("FEmissionPower", Range(0.1, 10)) = 1
        [HDR]_FMissionColor("FMissionColor", Color) = (0, 0, 0, 0)

        [Header(FinalAlphaSet)]
        _AlphaPow("AlphaPow", Float) = 1
        _AlphaMul("AlphaMul", Float) = 1
        _FinalAlpha("FinalAlpha", Range(0, 1)) = 1
    }
        SubShader
        {
             Tags
            {
                "RenderType" = "Transparent"
                "Queue" = "Transparent"
            }
            LOD 100

            Pass
            {
                Blend[_SrcBlend][_DstBlend]
                Cull[_Cull]
                ZWrite Off

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma shader_feature _USEPARTICLECUSTOMDATA

            #pragma shader_feature _USEVERTEXOFFSETPART

            #pragma shader_feature _USEREMAPPART
          
            #pragma shader_feature _USEALPHAPART
       
            #pragma shader_feature _USEDISSOLVEPART
       
            #pragma shader_feature _USEWARPPART
          
            #pragma shader_feature _USEALPHAMULFRESNEL

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 vertexCol : COLOR;
                float4 uv0 : TEXCOORD0;
#ifdef _USEPARTICLECUSTOMDATA
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
#endif
            };
            struct v2f
            {
                float4 uv0 : TEXCOORD0;
#ifdef _USEPARTICLECUSTOMDATA
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 vertexCol:TEXCOORD3;
                UNITY_FOG_COORDS(4)
            #ifdef _USEALPHAMULFRESNEL
                float3 worldNormal : TEXCOORD5;
                float3 worldViewDir : TEXCOORD6;
            #endif
#else
                float4 vertexCol:TEXCOORD1;
                UNITY_FOG_COORDS(2)
            #ifdef _USEALPHAMULFRESNEL
                float3 worldNormal : TEXCOORD3;
                float3 worldViewDir : TEXCOORD4;
            #endif
#endif
                float4 vertex : SV_POSITION;
            };

        
        #ifdef _USEVERTEXOFFSETPART
            float _OffsetLength;
            float _VertexOffsetTexUseA2R;
            float2 _VertexOffsetTexMoveSpeed;
            sampler2D _VertexOffsetTex;
            float4 _VertexOffsetTexTillingOffset;
        #endif

        #ifdef _USEREMAPPART
            sampler2D _RemapColTex;
            float4 _RemapColTexTillingOffset;
            float2 _RemapColTexMoveSpeed;
            float _RemapTexRotate;
        #endif
            float _MainTexRotate;
            float _MainTexAlphaByA2R;
            float4 _MainColor;
            sampler2D _MainTex;
            float4 _MainTexTillingOffset;
            float2 _MainTexMoveSpeed;
            float2 _MainTexWarpStrength;

        #ifdef _USEALPHAPART
            float _AlphaTexAlphaByA2R;
            sampler2D _AlphaTex;
            float4 _AlphaTexTillingOffset;
            float2 _AlphaTexMoveSpeed;
            float2 _AlphaTexWarpStrength;
        #endif

        #ifdef _USEDISSOLVEPART
            float _DissolveTexAlphaByA2R;
            sampler2D _DissolveTex;           
            float4 _DissolveTexTillingOffset;
            float2 _DissolveTexSpeed;
            float2 _DissolveTexWarpStrength;
            float _DissolveMaxCountMul;
            float _DissolveStep;
            float4 _DissolveEdgeCol;
            float _DissolveEdgeWidth;
        #endif

        #ifdef _USEWARPPART
            float _WarpTexAlphaByA2R;
            sampler2D _WarpTex;
            float4 _WarpTexTillingOffset;
            float2 _WarpTexMoveSpeed;
        #endif

        #ifdef _USEALPHAMULFRESNEL
            float _UseAlphaMulLength;
            float _Front2Back;
            float _FPower;
            float _FEmissionLength;
            float _FEmssionFront2Back;
            float _FEmissionPower;
            float4 _FMissionColor;
        #endif
            float _AlphaPow, _AlphaMul, _FinalAlpha;
               

            float2 RotateTex(float2 UV, float2 Center, float Rotation)
            {
                //rotation matrix
                UV -= Center;
                float s = sin(Rotation);
                float c = cos(Rotation);

                //center rotation matrix
                float2x2 rMatrix = float2x2(c, -s, s, c);
                rMatrix *= 0.5;
                rMatrix += 0.5;
                rMatrix = rMatrix * 2 - 1;

                //multiply the UVs by the rotation matrix
                UV.xy = mul(UV.xy, rMatrix);
                UV += Center;

                return UV;
            }

            float4 MySamplerTex(float Rotate, float2 uv,sampler2D InputTex,float4 TexTilling,float2 TexMoveSpeed,float2 NiuQu,float2 CustomData)
            {
                float rotateLength = Rotate * UNITY_TWO_PI;
                float2 rotate_uv = RotateTex(uv,float2(0.5,0.5), rotateLength);
                float2 vec2 = rotate_uv * TexTilling.xy + TexTilling.zw;
                vec2 += _Time.y * TexMoveSpeed;
                vec2 += NiuQu;
                vec2 += CustomData;
                float4 col = tex2D(InputTex, vec2);
                return col;
            }

         

            v2f vert (appdata v)
            {
                v2f o;
                float3 displacementAmount = float3(0,0,0);
#ifdef _USEVERTEXOFFSETPART
                float2 offsetTexUV = v.uv0.xy * _VertexOffsetTexTillingOffset.xy + _VertexOffsetTexTillingOffset.zw;
                offsetTexUV += _Time.y * _VertexOffsetTexMoveSpeed;
                float4 texDisplacement = tex2Dlod(_VertexOffsetTex, float4(offsetTexUV, 0, 0));
                float displacement = lerp(texDisplacement.a, texDisplacement.r, _VertexOffsetTexUseA2R);
                displacement *= _OffsetLength;
                displacementAmount += v.normal * displacement;
#endif
                v.vertex.xyz += displacementAmount;
                o.vertex = UnityObjectToClipPos(v.vertex); 
                o.vertexCol = v.vertexCol;
                o.uv0 = v.uv0;

#ifdef _USEPARTICLECUSTOMDATA
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
#endif
                UNITY_TRANSFER_FOG(o,o.vertex);

#ifdef _USEALPHAMULFRESNEL
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
#endif
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 MainTexUVCustomData = 0;
                float2 RongJieTexUVCustomData = 0;
                float2 NiuQuTexUVCustomData = 0;
                float RongJieStepCustomData = 0;
            #ifdef _USEPARTICLECUSTOMDATA
                 MainTexUVCustomData = i.uv0.zw;
                 RongJieTexUVCustomData = i.uv1.xy;
                 NiuQuTexUVCustomData = i.uv1.zw;
                 RongJieStepCustomData = i.uv2.x;
            #endif

                float2 uv = i.uv0.xy;
                float nStrength = 0;


            #ifdef _USEWARPPART
                float4 nCol = MySamplerTex(0, uv, _WarpTex, _WarpTexTillingOffset, _WarpTexMoveSpeed, float2(0, 0), NiuQuTexUVCustomData);
                nStrength = lerp(nCol.a, nCol.r, _WarpTexAlphaByA2R);
                
            #endif

                float FAlphaMul=1.0;               
                float4 FColor = float4(0, 0, 0, 0);
            #ifdef _USEALPHAMULFRESNEL
                float3 worldNormal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.worldViewDir);
                float rimFactor = dot(worldNormal, viewDir);
                float saturateRimFactor =pow(saturate(rimFactor), _FPower);
                float OneMinusSaturateRimFactor = 1 - saturateRimFactor;
                FAlphaMul = lerp( saturateRimFactor, OneMinusSaturateRimFactor, _Front2Back);
                FAlphaMul = lerp(1.0, FAlphaMul, _UseAlphaMulLength);

                float absRimFactor = abs(rimFactor);
                float powAbsRimFactor = pow(absRimFactor, _FEmissionPower);
                float OneMinusAbsRimFactor = 1 - powAbsRimFactor;
                float fColorFactor = lerp(OneMinusAbsRimFactor, powAbsRimFactor,_FEmssionFront2Back);
                FColor = _FMissionColor * fColorFactor;
                FColor *= _FEmissionLength;


            #endif

                float4 col = MySamplerTex(_MainTexRotate,uv,_MainTex, _MainTexTillingOffset, _MainTexMoveSpeed, _MainTexWarpStrength * nStrength, MainTexUVCustomData);
                col.a = lerp(col.a, col.r, _MainTexAlphaByA2R);          
                col *= _MainColor;

                col += FColor;
                col.a *= FAlphaMul;
                col *= i.vertexCol;
                

            #ifdef _USEREMAPPART
                float4 remapCol = MySamplerTex(_RemapTexRotate,uv, _RemapColTex, _RemapColTexTillingOffset, _RemapColTexMoveSpeed, float2(0, 0), float2(0, 0));
                remapCol.a = 1;
                col *= remapCol;
            #endif

            #ifdef _USEALPHAPART
                float4 alphaCol = MySamplerTex(0,uv,_AlphaTex, _AlphaTexTillingOffset, _AlphaTexMoveSpeed, _AlphaTexWarpStrength * nStrength, float2(0, 0));
                float alphaPartA = alphaCol.a;
                //#ifdef _ALPHATEXUSER
                alphaPartA = lerp(alphaCol.a, alphaCol.r, _AlphaTexAlphaByA2R);
                //#endif
                col.a *= alphaPartA;
            #endif

            #ifdef _USEDISSOLVEPART

                float value1 = max(RongJieStepCustomData,_DissolveStep) - 0.01;
                float value2 = saturate(value1* _DissolveMaxCountMul);
                float4 rongJieCol = MySamplerTex(0,uv, _DissolveTex, _DissolveTexTillingOffset, _DissolveTexSpeed, _DissolveTexWarpStrength* nStrength, RongJieTexUVCustomData);
                float value3 = lerp(rongJieCol.a, rongJieCol.r, _DissolveTexAlphaByA2R);
              
                float aFactor1 = smoothstep(value1, value2, value3);
                col.a *= aFactor1;

                float gValue1 = smoothstep(0, 0.01, _DissolveStep) * _DissolveEdgeWidth + value1;
                float gValue2 = saturate(saturate(gValue1) * _DissolveMaxCountMul);
                float aFactor2 = smoothstep(gValue1, gValue2, value3);

                float aFactor3 = saturate(aFactor1 - aFactor2);
                col.rgb += _DissolveEdgeCol.rgb * aFactor3;
            #endif

                col.a = pow(col.a, _AlphaPow);
                col.a = saturate(col.a * _AlphaMul) * _FinalAlpha;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
