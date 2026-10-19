// Native HDRP surface data is unchanged. Cut only raster geometry in front of protected targets.
#if SHADERPASS != SHADERPASS_SHADOWS && SHADERPASS != SHADERPASS_LIGHT_TRANSPORT && !defined(SHADER_STAGE_RAY_TRACING)
#define GetSurfaceAndBuiltinData LitMaisonOriginalSurface
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
#undef GetSurfaceAndBuiltinData

#include "Assets/CombatRealTime/Camera/LitTacticalVisibilityMask.hlsl"
float _LitMaskVersion, _LitMaskEnabled;
float4 _LitMaskTarget0, _LitMaskTarget1, _LitMaskSettings, _LitMaskCameraPosition, _LitMaskCameraForward;

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput,
    out SurfaceData surfaceData, out BuiltinData builtinData)
{
    float alpha;
    LitTacticalMask_float(GetAbsolutePositionWS(posInput.positionWS), 1, _LitMaskEnabled,
        _LitMaskTarget0, _LitMaskTarget1, _LitMaskSettings, _LitMaskCameraPosition,
        _LitMaskCameraForward, _LitMaskVersion, alpha);
    LitMaisonOriginalSurface(input, V, posInput, surfaceData, builtinData);
}
#else
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl"
#endif
