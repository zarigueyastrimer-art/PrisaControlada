using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class RedFilterRendererFeature : ScriptableRendererFeature
{
    private static class ShaderIDs
    {
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int RedSensitivity = Shader.PropertyToID("_RedSensitivity");
        public static readonly int RedStrength = Shader.PropertyToID("_RedStrength");
        public static readonly int Gamma = Shader.PropertyToID("_Gamma");
        public static readonly int BlurAmount = Shader.PropertyToID("_BlurAmount");
        public static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius");
        public static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        public static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
    }

    private RedFilterPass m_Pass;

    public override void Create()
    {
        m_Pass = new RedFilterPass(name);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        // The HUD slot previews render to RenderTextures with the same renderer;
        // the health filter must not apply to them (they keep their color).
        if (renderingData.cameraData.camera.targetTexture != null)
            return;

        if (!RedFilterVolumeComponent.IsEffectActive())
            return;

        m_Pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        m_Pass.ConfigureInput(ScriptableRenderPassInput.None);

        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
    }

    private class RedFilterPass : ScriptableRenderPass
    {
        private static Material mat
        {
            get
            {
                if (_mat == null) _mat = new Material(Shader.Find("Hidden/RedFilterURP"));
                return _mat;
            }
        }
        private static Material _mat;

        public RedFilterPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();

            float intensity = RedFilterVolumeComponent.s_Intensity;
            if (!RedFilterVolumeComponent.IsEffectActive()) return;

            TextureDesc desc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
            desc.name = "RedFilterSource";
            desc.clearBuffer = false;
            TextureHandle temp = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddUnsafePass<PassData>("Red Filter", out var passData, profilingSampler))
            {
                passData.intensity = intensity;
                passData.redSensitivity = RedFilterVolumeComponent.s_RedSensitivity;
                passData.redStrength = RedFilterVolumeComponent.s_RedStrength;
                passData.gamma = RedFilterVolumeComponent.s_Gamma;
                passData.blurAmount = RedFilterVolumeComponent.s_BlurAmount;
                passData.blurRadius = RedFilterVolumeComponent.s_BlurRadius;
                passData.source = resourcesData.activeColorTexture;
                passData.temp = temp;
                passData.target = resourcesData.cameraColor;

                builder.UseTexture(resourcesData.activeColorTexture);
                builder.UseTexture(temp, AccessFlags.ReadWrite);
                builder.UseTexture(resourcesData.cameraColor, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle temp;
            public TextureHandle target;
            public float intensity;
            public float redSensitivity;
            public float redStrength;
            public float gamma;
            public float blurAmount;
            public float blurRadius;
        }

        private static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            // Copy the source into a temp texture so we never read and write the same target.
            cmd.SetRenderTarget(data.temp);
            Blitter.BlitTexture(cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);

            // Filter the temp texture into the final camera color.
            MaterialPropertyBlock propertyBlock = context.renderGraphPool.GetTempMaterialPropertyBlock();
            propertyBlock.SetFloat(ShaderIDs.Intensity, data.intensity);
            propertyBlock.SetFloat(ShaderIDs.RedSensitivity, data.redSensitivity);
            propertyBlock.SetFloat(ShaderIDs.RedStrength, data.redStrength);
            propertyBlock.SetFloat(ShaderIDs.Gamma, data.gamma);
            propertyBlock.SetFloat(ShaderIDs.BlurAmount, data.blurAmount);
            propertyBlock.SetFloat(ShaderIDs.BlurRadius, data.blurRadius);
            propertyBlock.SetVector(ShaderIDs.BlitScaleBias, new Vector4(1, 1, 0, 0));
            propertyBlock.SetTexture(ShaderIDs.BlitTexture, data.temp);

            cmd.SetRenderTarget(data.target);
            cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1, propertyBlock);
        }
    }
}