using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable, VolumeComponentMenu("Post-processing/Custom/Run Speed Peripheral Blur")]
public sealed class RunSpeedPeripheralBlur : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    private const string ShaderName = "Hidden/Lit/RunSpeedPeripheralBlur";

    [Tooltip("Intensite globale du flou peripherique.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Zone centrale protegee du flou.")]
    public ClampedFloatParameter centerRadius = new ClampedFloatParameter(0.32f, 0f, 1f);
    [Tooltip("Rayon a partir duquel le flou devient pleinement visible.")]
    public ClampedFloatParameter edgeStart = new ClampedFloatParameter(0.48f, 0f, 1f);
    [Tooltip("Distance entre les echantillons du flou radial.")]
    public ClampedFloatParameter sampleStep = new ClampedFloatParameter(0.006f, 0.001f, 0.03f);
    [Tooltip("Nombre d'echantillons par direction.")]
    public ClampedIntParameter samples = new ClampedIntParameter(6, 2, 12);

    private Material material;

    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    public bool IsActive()
    {
        return material != null && intensity.value > 0.001f;
    }

    public override void Setup()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Unable to find shader '{ShaderName}'. Run speed peripheral blur is disabled.");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (material == null)
        {
            return;
        }

        material.SetTexture("_MainTex", source);
        material.SetFloat("_Intensity", intensity.value);
        material.SetFloat("_CenterRadius", centerRadius.value);
        material.SetFloat("_EdgeStart", Mathf.Max(centerRadius.value + 0.001f, edgeStart.value));
        material.SetFloat("_SampleStep", sampleStep.value);
        material.SetInt("_Samples", samples.value);

        HDUtils.DrawFullScreen(cmd, material, destination, shaderPassId: 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
        material = null;
    }
}
