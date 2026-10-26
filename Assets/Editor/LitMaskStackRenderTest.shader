// Test-only raster harness for the exact shared production cutout function.
Shader "Hidden/Lit/MaskStackRenderTest"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _ZTest("ZTest", Float) = 4
        _LitMaskVersion("Version", Float) = 1
        _LitMaskEnabled("Enabled", Float) = 0
        _LitMaskTarget0("Player", Vector) = (0,0,0,0)
        _LitMaskTarget1("Enemy", Vector) = (0,0,0,0)
        _LitMaskSettings("Settings", Vector) = (0.32,0.01,1,0.05)
        _LitMaskCameraPosition("Camera", Vector) = (0,0,0,0)
        _LitMaskCameraForward("Forward", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Pass
        {
            ZWrite On ZTest [_ZTest] Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #define SHADERPASS 0
            #define SHADERPASS_SHADOWS 100
            #define SHADERPASS_LIGHT_TRANSPORT 101
            float4x4 _TestVP, _TestObjectToWorld;
            float3 _TestCameraPosition, _TestCameraForward;
            float3 GetAbsolutePositionWS(float3 p) { return p; }
            float3 GetCameraRelativePositionWS(float3 p) { return p; }
            float3 GetCurrentViewPosition() { return _TestCameraPosition; }
            float3 GetViewForwardDir() { return _TestCameraForward; }
            float4 TransformWorldToHClip(float3 p) { return mul(_TestVP, float4(p, 1)); }
            #include "Assets/CombatRealTime/Camera/LitTacticalVisibilityMask.hlsl"
            float4 _Color, _LitMaskTarget0, _LitMaskTarget1, _LitMaskSettings, _LitMaskCameraPosition, _LitMaskCameraForward;
            float _LitMaskVersion, _LitMaskEnabled;
            struct v2f { float4 position : SV_POSITION; float3 world : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o; o.world = mul(_TestObjectToWorld, v.vertex).xyz;
                o.position = TransformWorldToHClip(o.world); return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float alpha;
                LitTacticalMask_float(i.world, 1, _LitMaskEnabled, _LitMaskTarget0, _LitMaskTarget1,
                    _LitMaskSettings, _LitMaskCameraPosition, _LitMaskCameraForward, _LitMaskVersion, alpha);
                return _Color;
            }
            ENDCG
        }
    }
}
