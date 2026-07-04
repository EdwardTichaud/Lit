using INab.VFXAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

public static class CharacterEffectRuntimeRepair
{
    private static bool sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        sceneHooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        RepairLoadedCharacterEffects();
        if (sceneHooked)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHooked = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RepairLoadedCharacterEffects();
    }

    private static void RepairLoadedCharacterEffects()
    {
#if UNITY_2023_1_OR_NEWER
        CharacterEffect[] effects = UnityEngine.Object.FindObjectsByType<CharacterEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        CharacterEffect[] effects = UnityEngine.Object.FindObjectsOfType<CharacterEffect>(true);
#endif
        for (int i = 0; i < effects.Length; i++)
        {
            RepairEffect(effects[i]);
        }
    }

    private static void RepairEffect(CharacterEffect effect)
    {
        if (effect == null || !effect.isActiveAndEnabled || effect.vfxComponent != null || effect.effectPrefab == null)
        {
            return;
        }

        if (effect.instantiatedEffectPrefab == null)
        {
            GameObject instance = UnityEngine.Object.Instantiate(effect.effectPrefab, effect.transform);
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;
            instance.name = effect.effectPrefab.name + " (Instance) [" + effect.effectName + "]";
            effect.instantiatedEffectPrefab = instance;
        }

        effect.vfxComponent = effect.instantiatedEffectPrefab.GetComponent<VisualEffect>();
        if (effect.vfxComponent == null)
        {
            effect.vfxComponent = effect.instantiatedEffectPrefab.GetComponentInChildren<VisualEffect>(true);
        }

        effect.vfxBinder = effect.instantiatedEffectPrefab.GetComponent<VFXPropertyBinder>();
        if (effect.vfxBinder == null)
        {
            effect.vfxBinder = effect.instantiatedEffectPrefab.GetComponentInChildren<VFXPropertyBinder>(true);
        }

        if (effect.vfxComponent == null)
        {
            return;
        }

        effect.ConfigureVFXBinders();
        effect.SetupVfxGraph();
    }
}
