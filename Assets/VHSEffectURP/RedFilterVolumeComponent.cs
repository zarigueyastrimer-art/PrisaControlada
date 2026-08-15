using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Post-processing Custom/Red Filter")]

[VolumeRequiresRendererFeatures(typeof(RedFilterRendererFeature))]

[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]

public sealed class RedFilterVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public static float s_Intensity = 0f;
    public static float s_RedSensitivity = 4f;
    public static float s_RedStrength = 1f;
    public static float s_Gamma = 1f;
    public static float s_BlurAmount = 0f;
    public static float s_BlurRadius = 3f;

    public static bool IsEffectActive()
    {
        return s_Intensity > 0f || s_Gamma > 1.001f || s_BlurAmount > 0.001f;
    }

    public static void SetNeutral()
    {
        s_Intensity = 0f;
        s_RedSensitivity = 4f;
        s_RedStrength = 1f;
        s_Gamma = 1f;
        s_BlurAmount = 0f;
        s_BlurRadius = 3f;
    }

    public RedFilterVolumeComponent()
    {
        displayName = "Red Filter";
    }

    [Tooltip("Overall intensity of the black & white / red-only filter")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("How strongly a pixel must be red to keep its color")]
    public ClampedFloatParameter redSensitivity = new ClampedFloatParameter(4f, 0.1f, 10f);

    [Tooltip("How much of the original red is preserved")]
    public ClampedFloatParameter redStrength = new ClampedFloatParameter(1f, 0f, 1f);

    public bool IsActive()
    {
        return intensity.GetValue<float>() > 0;
    }
}