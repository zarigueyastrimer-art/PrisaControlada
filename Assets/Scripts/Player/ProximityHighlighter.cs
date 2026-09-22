using System.Collections.Generic;
using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // Resaltador de proximidad con DOS comportamientos independientes:
    //
    //  1) BORDE BLANCO (outline): se muestra al APUNTAR con el centro de la
    //     pantalla a cualquier interactuable (puertas, cajones, muebles, armas
    //     en el suelo, municion, linternas, etc). Desaparece al apartar la mira.
    //
    //  2) FLECHAS (una por pickup): se muestra UNA FLECHA POR CADA objeto
    //     AGARRABLE (arma, linterna, municion...) que este DENTRO del radio de
    //     captura. NO hace falta apuntar: basta con acercarse. Si hay 2 pickups
    //     juntos salen 2 flechas. Las flechas NUNCA aparecen sobre puertas ni
    //     muebles, solo pickups.
    [RequireComponent(typeof(PlayerController))]
    public class ProximityHighlighter : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Material outlineMaterial;

        [Header("Apuntar (borde blanco)")]
        [SerializeField] private float aimRange = 2f;
        [SerializeField] private float aimRangeDoor = 3.5f;
        [SerializeField] private LayerMask rayMask = -1;

        [Header("Proximidad (flechas)")]
        [SerializeField] private float pickupRange = 4f;
        [SerializeField] private float arrowFadeSpeed = 8f;
        [SerializeField] private float arrowBobSpeed = 2.5f;
        [SerializeField] private float arrowBobAmplitude = 0.1f;
        [SerializeField] private Vector3 arrowOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private float arrowScale = 0.3f;
        [SerializeField] private int maxArrows = 16;

        // Una flecha por pickup cercano, gestionadas con un pool.
        private readonly Dictionary<GameObject, PickupArrow> _arrowsByPickup = new Dictionary<GameObject, PickupArrow>();
        private readonly List<PickupArrow> _arrowPool = new List<PickupArrow>();
        private readonly List<GameObject> _nearbyPickups = new List<GameObject>();
        private readonly List<GameObject> _stalePickups = new List<GameObject>();
        private readonly Collider[] _proxHits = new Collider[32];

        private GameObject _outlined;
        private readonly List<GameObject> _outlineClones = new List<GameObject>();
        private readonly List<Mesh> _bakedMeshes = new List<Mesh>();

        private sealed class PickupArrow
        {
            public GameObject root;
            public CanvasGroup canvasGroup;
            public Image image;
            public float alpha;
        }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        private PickupArrow CreateArrow()
        {
            GameObject arrowCanvas = new GameObject("HighlightArrow");
            arrowCanvas.transform.SetParent(transform, false);

            var canvas = arrowCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 300;

            var rt = arrowCanvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(64, 64);
            rt.localScale = Vector3.one * arrowScale;

            GameObject holder = new GameObject("ArrowHolder", typeof(RectTransform));
            holder.transform.SetParent(arrowCanvas.transform, false);

            var holderRT = holder.GetComponent<RectTransform>();
            holderRT.anchorMin = Vector2.zero;
            holderRT.anchorMax = Vector2.one;
            holderRT.offsetMin = Vector2.zero;
            holderRT.offsetMax = Vector2.zero;

            var arrowImage = holder.AddComponent<Image>();
            if (arrowSprite != null)
                arrowImage.sprite = arrowSprite;
            arrowImage.color = Color.white;
            arrowImage.raycastTarget = false;
            arrowImage.preserveAspect = true;

            var cg = arrowCanvas.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            return new PickupArrow { root = arrowCanvas, canvasGroup = cg, image = arrowImage };
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                HideAllArrows();
                return;
            }

            // --- 1) Borde blanco: al apuntar a cualquier interactuable ---
            GameObject aimed = FindAimedInteractable();
            if (aimed != null)
            {
                if (_outlined != aimed)
                {
                    ClearOutline();
                    _outlined = aimed;
                    ApplyOutline(aimed);
                }
            }
            else
            {
                ClearOutline();
            }

            // --- 2) Flechas: una por cada pickup agarrable cercano (sin apuntar) ---
            RefreshNearbyPickups();
        }

        private void RefreshNearbyPickups()
        {
            _nearbyPickups.Clear();
            Vector3 origin = viewCamera.transform.position;

            int count = Physics.OverlapSphereNonAlloc(origin, pickupRange, _proxHits, rayMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _proxHits[i];
                if (hit == null) continue;

                GameObject pickup = GetPickupRoot(hit);
                if (pickup == null) continue;
                if (!_nearbyPickups.Contains(pickup))
                    _nearbyPickups.Add(pickup);
            }

            // Marcamos todas las flechas activas como "candidatas a reciclar".
            _stalePickups.Clear();
            foreach (var kv in _arrowsByPickup)
                _stalePickups.Add(kv.Key);

            // Nos aseguramos de que cada pickup cercano tenga su flecha.
            for (int i = 0; i < _nearbyPickups.Count; i++)
            {
                GameObject pickup = _nearbyPickups[i];
                _stalePickups.Remove(pickup);

                PickupArrow pa;
                if (!_arrowsByPickup.TryGetValue(pickup, out pa))
                {
                    pa = _arrowPool.Count > 0 ? _arrowPool[_arrowPool.Count - 1] : CreateArrow();
                    if (_arrowPool.Count > 0)
                        _arrowPool.RemoveAt(_arrowPool.Count - 1);
                    pa.root.SetActive(true);
                    _arrowsByPickup[pickup] = pa;
                }

                pa.alpha = Mathf.MoveTowards(pa.alpha, 1f, arrowFadeSpeed * Time.deltaTime);
                PositionArrow(pa, pickup);
                SetArrowAlpha(pa, pa.alpha);
            }

            // Las flechas de los pickups que ya no estan cerca se apagan y reciclan.
            for (int i = 0; i < _stalePickups.Count; i++)
            {
                GameObject pickup = _stalePickups[i];
                PickupArrow pa;
                if (!_arrowsByPickup.TryGetValue(pickup, out pa))
                    continue;

                pa.alpha = Mathf.MoveTowards(pa.alpha, 0f, arrowFadeSpeed * Time.deltaTime);
                if (pa.alpha <= 0.01f)
                {
                    _arrowsByPickup.Remove(pickup);
                    pa.root.SetActive(false);
                    if (_arrowPool.Count < maxArrows)
                        _arrowPool.Add(pa);
                }
                else
                {
                    SetArrowAlpha(pa, pa.alpha);
                }
            }
        }

        private void PositionArrow(PickupArrow pa, GameObject pickup)
        {
            Vector3 pos;
            Renderer rend = pickup.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Bounds b = rend.bounds;
                pos = new Vector3(b.center.x, b.max.y, b.center.z) + arrowOffset;
            }
            else
            {
                pos = pickup.transform.position + arrowOffset;
            }

            float bob = Mathf.Sin(Time.time * arrowBobSpeed) * arrowBobAmplitude;
            pos += new Vector3(0f, bob, 0f);

            pa.root.transform.position = pos;
            pa.root.transform.LookAt(viewCamera.transform);
            pa.root.transform.Rotate(0f, 180f, 0f);
        }

        private void SetArrowAlpha(PickupArrow pa, float alpha)
        {
            pa.canvasGroup.alpha = alpha;
            pa.root.SetActive(alpha > 0.01f);
        }

        private void HideAllArrows()
        {
            foreach (var kv in _arrowsByPickup)
            {
                kv.Value.root.SetActive(false);
                kv.Value.canvasGroup.alpha = 0f;
                if (_arrowPool.Count < maxArrows)
                    _arrowPool.Add(kv.Value);
            }
            _arrowsByPickup.Clear();
        }

        private GameObject FindAimedInteractable()
        {
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, aimRange, rayMask, QueryTriggerInteraction.Collide))
                return null;

            if (hit.collider.GetComponentInParent<DrawerController>() != null)
                return hit.collider.transform.parent != null ? hit.collider.transform.parent.gameObject : hit.collider.gameObject;
            if (hit.collider.GetComponentInParent<DoorController>() != null)
                return hit.collider.transform.parent != null ? hit.collider.transform.parent.gameObject : hit.collider.gameObject;
            if (hit.collider.GetComponentInParent<AmmoPickup>() != null)
                return hit.collider.gameObject;
            if (hit.collider.GetComponentInParent<GlockAmmoPickup>() != null)
                return hit.collider.gameObject;
            if (hit.collider.GetComponentInParent<GlockMagazinePickup>() != null)
                return hit.collider.gameObject;
            if (hit.collider.GetComponentInParent<PickupItem>() != null)
                return hit.collider.gameObject;

            return null;
        }

        private GameObject GetPickupRoot(Collider hit)
        {
            if (hit.GetComponentInParent<AmmoPickup>() != null)
                return hit.gameObject;
            if (hit.GetComponentInParent<ShotgunAmmoPickup>() != null)
                return hit.gameObject;
            if (hit.GetComponentInParent<GlockAmmoPickup>() != null)
                return hit.gameObject;
            if (hit.GetComponentInParent<GlockMagazinePickup>() != null)
                return hit.gameObject;

            PickupItem item = hit.GetComponentInParent<PickupItem>();
            if (item != null)
                return item.gameObject;

            return null;
        }

        private void ApplyOutline(GameObject target)
        {
            if (outlineMaterial == null) return;

            MeshFilter[] filters = target.GetComponentsInChildren<MeshFilter>();
            foreach (var filter in filters)
            {
                CreateStaticOutline(filter.transform, filter.sharedMesh);
            }

            SkinnedMeshRenderer[] skinned = target.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var sk in skinned)
            {
                CreateSkinnedOutline(sk);
            }
        }

        private void CreateStaticOutline(Transform parent, Mesh mesh)
        {
            if (mesh == null) return;

            GameObject clone = new GameObject("OutlineClone");
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;

            clone.AddComponent<MeshFilter>().sharedMesh = mesh;
            var cloneRenderer = clone.AddComponent<MeshRenderer>();
            cloneRenderer.sharedMaterial = outlineMaterial;
            cloneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cloneRenderer.receiveShadows = false;

            _outlineClones.Add(clone);
        }

        private void CreateSkinnedOutline(SkinnedMeshRenderer sk)
        {
            if (sk.sharedMesh == null) return;

            GameObject clone = new GameObject("OutlineClone");
            // BakeMesh hornea la malla en el espacio del ROOT BONE del esqueleto,
            // no del propio renderer. Por eso el clon se cuelga del root bone del
            // skinned renderer: si el modelo trae el root bone escalado u offset
            // (tipico en armas importadas como el revolver/escopeta), el borde
            // queda perfectamente alineado con la malla visible.
            Transform attach = sk.rootBone != null ? sk.rootBone : sk.transform;
            clone.transform.SetParent(attach, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;

            Mesh baked = new Mesh();
            sk.BakeMesh(baked);
            _bakedMeshes.Add(baked);

            clone.AddComponent<MeshFilter>().sharedMesh = baked;
            var cloneRenderer = clone.AddComponent<MeshRenderer>();
            cloneRenderer.sharedMaterial = outlineMaterial;
            cloneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cloneRenderer.receiveShadows = false;

            _outlineClones.Add(clone);
        }

        private void ClearOutline()
        {
            foreach (var clone in _outlineClones)
            {
                if (clone != null)
                    Destroy(clone);
            }
            _outlineClones.Clear();

            foreach (var mesh in _bakedMeshes)
            {
                if (mesh != null)
                    Destroy(mesh);
            }
            _bakedMeshes.Clear();
            _outlined = null;
        }

        private void OnDisable()
        {
            ClearOutline();
        }
    }
}