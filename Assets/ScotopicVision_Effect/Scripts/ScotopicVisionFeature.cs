using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Render Feature que aplica la vision escotopica (Eigengrau + ruido neural +
/// vision periferica + blur de bastones) como un blit a pantalla completa,
/// despues del post-procesado.
///
/// Es el equivalente al material "NightWrite" del video de referencia. El
/// material se crea desde <see cref="Settings.shader"/> y queda expuesto en
/// <see cref="ActiveMaterial"/> para que el controlador setee los parametros
/// cada frame segun la luz que mida alrededor del jugador.
///
/// Robusto ante domain reloads: si <c>Create()</c> no corrio (o los objetos
/// quedaron desfasados tras recompilar), <see cref="AddRenderPasses"/> los
/// re-resuelve solos cada frame. Asi el efecto nunca "desaparece".
/// </summary>
public class ScotopicVisionFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Punto de inyeccion. AfterRenderingPostProcessing = es el ultimo toque: " +
                 "la retina pone su ruido y su gris por encima de todo.")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        [Tooltip("Shader Hidden/PCHZ/ScotopicVision.")]
        public Shader shader;
    }

    public Settings settings = new Settings();

    /// <summary>Nombre del shader fallback por si se pierde la referencia serializada.</summary>
    private const string kShaderName = "Hidden/PCHZ/ScotopicVision";

    /// <summary>Material activo que el controlador puede setear. null si no hay shader.</summary>
    public static Material ActiveMaterial { get; private set; }

    private Material _material;
    private ScotopicPass _pass;

    public override void Create()
    {
        EnsureObjects();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Auto-sanado: si tras un domain reload algo quedo null/desfasado, lo
        // recreamos aqui. Corre antes de encolar el pass, así que siempre hay
        // material y pass validos.
        EnsureObjects();

        if (_material == null || _pass == null)
            return;

        // Solo la cámara del juego: no la de preview, reflection probe ni scene.
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        renderer.EnqueuePass(_pass);
    }

    /// <summary>
    /// Garantiza que settings, shader, material y pass existan y esten
    /// sincronizados. Llamar desde Create() y desde AddRenderPasses().
    /// </summary>
    private void EnsureObjects()
    {
        if (settings == null)
            settings = new Settings();

        // Si se perdio la referencia serializada al shader (pasa tras ciertos
        // reloads), la resolvemos por nombre.
        if (settings.shader == null)
            settings.shader = Shader.Find(kShaderName);

        if (settings.shader == null)
        {
            // Sin shader no hay nada que aplicar; limpiamos para no dejar
            // pass viejos apuntando a material destruido.
            ActiveMaterial = null;
            return;
        }

        // (Re)crear el material solo si no existe o su shader cambio.
        if (_material == null || _material.shader != settings.shader)
        {
            if (_material != null)
                CoreUtils.Destroy(_material);
            _material = CoreUtils.CreateEngineMaterial(settings.shader);
        }

        ActiveMaterial = _material;

        // (Re)crear el pass si no existe o si el material/evento cambiaron.
        if (_pass == null)
        {
            _pass = new ScotopicPass(_material);
        }
        else
        {
            _pass.SetMaterial(_material);
        }
        _pass.renderPassEvent = settings.renderPassEvent;
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        _pass = null;

        if (_material != null)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        ActiveMaterial = null;
    }

    // --------------------------------------------------------------- pass
    class ScotopicPass : ScriptableRenderPass
    {
        private Material _material;
        private RTHandle _tempColor;

        public ScotopicPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("ScotopicVision");
        }

        /// <summary>Permite re-apuntar el pass a un material recreado.</summary>
        public void SetMaterial(Material material)
        {
            _material = material;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Render target temporal del mismo tamaño que la cámara, sin depth.
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref _tempColor, desc,
                FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: "_ScotopicVisionTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
                return;

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // cámara -> temporal, pasando por el shader escotópico.
                Blitter.BlitCameraTexture(cmd, source, _tempColor, _material, 0);
                // temporal -> cámara (copia final).
                Blitter.BlitCameraTexture(cmd, _tempColor, source);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _tempColor?.Release();
            _tempColor = null;
        }
    }
}
