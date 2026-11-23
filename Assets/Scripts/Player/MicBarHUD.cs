using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    // Barra vertical del microfono (MicBarHUD_Canvas). Lee el nivel de voz de
    // MicNoiseMeter.CurrentLevel y "llena" la tira de barritas de abajo hacia
    // arriba: tres segmentos apilados (blancas abajo, amarillas en el medio,
    // rojas arriba) a los que se les aplica el shader PrisaControlada/MicBarReveal.
    //
    // El nivel global se reparte entre los segmentos segun su altura real: cada
    // uno recibe su propia fraccion de _Fill y el shader recorta la parte de
    // arriba que todavia no se mostro. UGUI no admite MaterialPropertyBlock,
    // por lo que cada segmento usa una instancia propia del material.
    //
    // Este script es solo logica: NO crea UI. La jerarquia (barra, segmentos e
    // icono) la arma la tool de Editor "PrisaControlada/Configurar MicBarHUD".
    [DisallowMultipleComponent]
    public class MicBarHUD : MonoBehaviour
    {
        [Header("UI (lo crea la tool de Editor)")]
        [Tooltip("Rect contenedor de la barra completa: define la altura total de referencia.")]
        [SerializeField] private RectTransform bar;

        [Tooltip("Segmentos de barritas ordenados de abajo hacia arriba: blancas, amarillas, rojas.")]
        [SerializeField] private RawImage[] segments;

        [Tooltip("MicNoiseMeter que aporta el nivel de voz. Si esta vacio, se busca en los padres.")]
        [SerializeField] private MicNoiseMeter mic;

        private Material[] _segmentMats;
        private readonly Vector3[] _corners = new Vector3[4];
        private static readonly int FillId = Shader.PropertyToID("_Fill");

        private void Awake()
        {
            if (mic == null)
                mic = GetComponentInParent<MicNoiseMeter>();

            if (bar == null || segments == null || segments.Length == 0)
            {
                Debug.LogError("[MicBarHUD] Faltan referencias de UI (bar/segments). " +
                               "Ejecuta PrisaControlada/Configurar MicBarHUD. Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            if (mic == null)
            {
                Debug.LogError("[MicBarHUD] No se encontro MicNoiseMeter en los padres. " +
                               "Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            // Una instancia de material por segmento: cada uno necesita su
            // propio _Fill y UGUI no soporta MaterialPropertyBlock.
            _segmentMats = new Material[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                RawImage img = segments[i];
                if (img == null) continue;

                // En UGUI el getter es "material" (no existe "sharedMaterial",
                // eso es de Renderer 3D). Devuelve el material serializado del
                // componente o, si no hay, el material por defecto de la UI (que
                // no tiene _Fill): hay que validar que sea el MicBarReveal.
                Material source = img.material;
                if (source == null || !source.HasProperty(FillId))
                {
                    Debug.LogError("[MicBarHUD] El segmento '" + img.name + "' no tiene el material " +
                                   "MicBarReveal (falta la propiedad _Fill). Ejecuta " +
                                   "PrisaControlada/Configurar MicBarHUD. Componente deshabilitado.", img);
                    enabled = false;
                    return;
                }

                Material instance = new Material(source);
                instance.hideFlags = HideFlags.HideAndDontSave;
                img.material = instance;
                _segmentMats[i] = instance;
            }
        }

        private void OnDestroy()
        {
            if (_segmentMats == null) return;

            for (int i = 0; i < _segmentMats.Length; i++)
            {
                if (_segmentMats[i] == null) continue;

                if (Application.isPlaying)
                    Destroy(_segmentMats[i]);
                else
                    DestroyImmediate(_segmentMats[i]);
            }
        }

        private void Update()
        {
            if (bar == null || segments == null || _segmentMats == null) return;

            // Nivel de voz 0..1 (ya suavizado por MicNoiseMeter).
            float level = mic != null ? Mathf.Clamp01(mic.CurrentLevel) : 0f;

            // Altura alcanzada en el espacio local de la barra (yMin = base).
            float threshold = bar.rect.yMin + level * bar.rect.height;

            for (int i = 0; i < segments.Length; i++)
            {
                RawImage img = segments[i];
                if (img == null || _segmentMats[i] == null) continue;

                // Min/Max Y del segmento en el espacio local de la barra:
                // funciona aunque el segmento este anidado o escalado y no
                // depende del orden de las esquinas de GetWorldCorners.
                img.rectTransform.GetWorldCorners(_corners);
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                for (int c = 0; c < 4; c++)
                {
                    Vector3 local = bar.InverseTransformPoint(_corners[c]);
                    if (local.y < minY) minY = local.y;
                    if (local.y > maxY) maxY = local.y;
                }

                float height = maxY - minY;
                float fill = height > 0.0001f
                    ? Mathf.Clamp01((threshold - minY) / height)
                    : 0f;

                _segmentMats[i].SetFloat(FillId, fill);
            }
        }
    }
}
