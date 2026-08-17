using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrisaControlada.Player
{
    public class WeaponVFX : MonoBehaviour
    {
        [Header("Textures (si vacias, se cargan de Resources/VFX/Kenney)")]
        [SerializeField] private Texture2D muzzleFlashTex;
        [SerializeField] private Texture2D muzzleGlowTex;
        [SerializeField] private Texture2D muzzleSmokeTex;
        [SerializeField] private Texture2D impactSparkTex;

        [Header("Muzzle Flash")]
        [SerializeField] private float flashLife = 0.09f;
        [SerializeField] private float flashCoreSize = 0.6f;
        [SerializeField] private float flashGlowSize = 1.4f;
        [SerializeField] private float flashLightRange = 9f;
        [SerializeField] private float flashLightIntensity = 16f;
        [SerializeField] private Color flashColor = new Color(1f, 0.82f, 0.5f);
        [SerializeField] private Color glowColor = new Color(1f, 0.6f, 0.25f, 0.8f);

        [Header("Muzzle Smoke")]
        [SerializeField] private int smokeCount = 14;
        [SerializeField] private float smokeLife = 0.9f;
        [SerializeField] private float smokeSize = 0.38f;
        [SerializeField] private Color smokeColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);

        [Header("Barrel Smoke (post-shot)")]
        [SerializeField] private float barrelSmokeDuration = 1.8f;
        [SerializeField] private float barrelSmokeRate = 14f;
        [SerializeField] private float barrelSmokeLife = 1f;
        [SerializeField] private float barrelSmokeSize = 0.09f;
        [SerializeField] private Color barrelSmokeColor = new Color(0.95f, 0.95f, 0.95f, 0.5f);

        [Header("Impact")]
        [SerializeField] private int impactCount = 22;
        [SerializeField] private float impactLife = 0.45f;
        [SerializeField] private float impactSize = 0.14f;
        [SerializeField] private Color impactColor = new Color(1f, 0.72f, 0.3f);
        [SerializeField] private float impactFlashSize = 0.9f;
        [SerializeField] private Color impactFlashColor = new Color(1f, 0.85f, 0.5f, 0.9f);
        [SerializeField] private int impactDustCount = 6;
        [SerializeField] private float impactDustSize = 0.28f;
        [SerializeField] private Color impactDustColor = new Color(0.45f, 0.45f, 0.45f, 0.4f);

        [Header("Bullet Hole Decal")]
        [SerializeField] private Texture2D bulletHoleTex;
        [SerializeField] private float bulletHoleSize = 0.08f;
        [SerializeField] private float bulletHoleOffset = 0.01f;
        [SerializeField] private int maxBulletHoles = 50;
        [Tooltip("Capa del decal. 0 = mundo normal (lo renderiza la camara base con oclusion por profundidad). No usar la capa del arma (layer 8): esa la dibuja la camara overlay SIEMPRE encima.")]
        [SerializeField] private int bulletHoleLayer = 0;

        [Header("Tracer")]
        [SerializeField] private Color tracerColor = new Color(1f, 0.7f, 0.3f, 1f);
        [SerializeField] private float tracerSize = 0.02f;
        [SerializeField] private float tracerDuration = 0.15f;
        [SerializeField] private float tracerSpacing = 0.05f;

        [Header("Debug")]
        [SerializeField] private Color lineTraceColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color barrelLineColor = new Color(0.2f, 1f, 1f, 1f);
        [SerializeField] private float lineTraceWidth = 0.04f;

        private Material _flashMat;
        private Material _glowMat;
        private Material _smokeMat;
        private Material _sparkMat;
        private Material _decalMat;
        private Material _lineMat;
        private Transform _muzzle;
        private int _weaponLayer;
        private readonly List<Transform> _bulletHoles = new List<Transform>();

        public bool HasMuzzle => _muzzle != null;
        public Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : Vector3.zero;
        public Vector3 MuzzleForward => _muzzle != null ? _muzzle.forward : Vector3.forward;

        private void Awake()
        {
            if (muzzleFlashTex == null) muzzleFlashTex = Resources.Load<Texture2D>("VFX/Kenney/muzzle_01");
            if (muzzleGlowTex == null) muzzleGlowTex = Resources.Load<Texture2D>("VFX/Kenney/light_01");
            if (muzzleSmokeTex == null) muzzleSmokeTex = Resources.Load<Texture2D>("VFX/Kenney/smoke_02");
            if (impactSparkTex == null) impactSparkTex = Resources.Load<Texture2D>("VFX/Kenney/spark_01");
            if (bulletHoleTex == null) bulletHoleTex = Resources.Load<Texture2D>("Decals/bulletHole");

            _flashMat = CreateParticleMat(muzzleFlashTex);
            _glowMat = CreateParticleMat(muzzleGlowTex);
            _smokeMat = CreateParticleMat(muzzleSmokeTex);
            _sparkMat = CreateParticleMat(impactSparkTex);
            _decalMat = CreateDecalMat(bulletHoleTex);
            _lineMat = CreateLineMat();

            _weaponLayer = gameObject.layer;
            Transform root = transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = FindMuzzle(root.GetChild(i));
                if (t != null) { _muzzle = t; break; }
            }
            if (_muzzle == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>())
                {
                    if (t.name == "muzzle_end") { _muzzle = t; break; }
                }
            }
        }

        private static Transform FindMuzzle(Transform parent)
        {
            if (parent == null) return null;
            if (parent.name == "muzzle_end") return parent;
            foreach (Transform child in parent)
            {
                Transform deeper = FindMuzzle(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        public void PlayMuzzle()
        {
            if (_muzzle == null) return;

            SpawnBurst(_muzzle, Vector3.zero, _flashMat, 1, flashLife, flashCoreSize, flashColor, 0f, 0f, 0f);
            SpawnBurst(_muzzle, Vector3.zero, _glowMat, 1, flashLife * 1.4f, flashGlowSize, glowColor, 0f, 0f, 0f);

            var lightGo = new GameObject("VFX_FlashLight");
            lightGo.layer = _weaponLayer;
            lightGo.transform.position = _muzzle.position;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = flashLightRange;
            l.intensity = flashLightIntensity;
            l.color = flashColor;
            l.shadows = LightShadows.None;
            Destroy(lightGo, flashLife * 1.4f + 0.05f);

            SpawnBurst(null, _muzzle.position, _smokeMat, smokeCount, smokeLife, smokeSize, smokeColor, 0.3f, 1.6f, -0.1f);
        }

        public void PlayImpact(Vector3 point, Vector3 normal)
        {
            Quaternion rot = Quaternion.LookRotation(normal);

            Transform sparks = SpawnBurst(null, point, _sparkMat, impactCount, impactLife, impactSize, impactColor, 2.5f, 7f, 1.4f);
            if (sparks != null) sparks.rotation = rot;

            Transform flash = SpawnBurst(null, point, _glowMat, 1, 0.07f, impactFlashSize, impactFlashColor, 0f, 0f, 0f);
            if (flash != null) flash.rotation = rot;

            Transform dust = SpawnBurst(null, point + normal * 0.02f, _smokeMat, impactDustCount, 0.5f, impactDustSize, impactDustColor, 0.2f, 0.9f, 0.4f);
            if (dust != null) dust.rotation = rot;
        }

        public void PlayBarrelSmoke()
        {
            if (_muzzle == null) return;
            Vector3 worldStart = _muzzle.position + transform.forward * 0.12f;
            Vector3 localStart = _muzzle.InverseTransformPoint(worldStart);
            SpawnStream(_muzzle, localStart, _smokeMat, barrelSmokeDuration, barrelSmokeRate, barrelSmokeLife, barrelSmokeSize, barrelSmokeColor, 0.05f, 0.25f);
        }

        public void PlayBulletHole(Vector3 point, Vector3 normal)
        {
            if (_decalMat == null || bulletHoleTex == null)
            {
                Debug.LogWarning($"<color=#ff7777>[WeaponVFX] BulletHole ignorado: decalMat={( _decalMat != null )} tex={( bulletHoleTex != null )}</color>");
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "VFX_BulletHole";
            go.layer = bulletHoleLayer;
            go.transform.position = point + normal * bulletHoleOffset;
            go.transform.rotation = Quaternion.LookRotation(-normal);
            go.transform.localScale = Vector3.one * bulletHoleSize;

            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<MeshRenderer>();
            rend.material = _decalMat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            _bulletHoles.Add(go.transform);
            while (_bulletHoles.Count > maxBulletHoles)
            {
                Transform oldest = _bulletHoles[0];
                _bulletHoles.RemoveAt(0);
                if (oldest != null) Object.Destroy(oldest.gameObject);
            }
        }

        public void DrawLineTrace(Vector3 start, Vector3 end, Color color, float duration)
        {
            Debug.DrawLine(start, end, color, duration);
            SpawnLineDots(start, end, color, lineTraceWidth, duration, 0.15f, "VFX_LineDot");
            Debug.Log($"<color=#ff66ff>[WeaponVFX] LineTrace dibujado: {start} -> {end}</color>");
        }

        public void PlayTracer(Vector3 start, Vector3 end)
        {
            SpawnLineDots(start, end, tracerColor, tracerSize, tracerDuration, tracerSpacing, "VFX_Tracer");
        }

        private void SpawnLineDots(Vector3 start, Vector3 end, Color color, float width, float duration, float spacing, string goName)
        {
            if (_glowMat == null)
            {
                Debug.LogWarning("<color=#ff7777>[WeaponVFX] SpawnLineDots: _glowMat null</color>");
                return;
            }
            float len = Vector3.Distance(start, end);
            if (len < 0.001f) return;
            int count = Mathf.Max(2, Mathf.CeilToInt(len / spacing));
            for (int i = 0; i < count; i++)
            {
                Vector3 p = start + (end - start) * (i / (float)(count - 1));
                var go = new GameObject(goName);
                go.layer = _weaponLayer;
                go.transform.position = p;
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = duration;
                main.startSpeed = 0f;
                main.startSize = width;
                main.startColor = color;
                main.maxParticles = 4;
                main.gravityModifier = 0f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
                var sh = ps.shape;
                sh.enabled = false;
                var ren = go.GetComponent<ParticleSystemRenderer>();
                ren.material = _glowMat;
                ren.renderMode = ParticleSystemRenderMode.Billboard;
                ren.alignment = ParticleSystemRenderSpace.View;
                ps.Play();
                Destroy(go, duration + 0.3f);
            }
            Debug.Log($"<color=#ffb3ff>[WeaponVFX] {goName}: {count} dots</color>");
        }

        private void SpawnStream(Transform parent, Vector3 localPos, Material mat, float duration, float rate, float life, float size, Color color, float speedMin, float speedMax)
        {
            var go = new GameObject("VFX_SmokeStream");
            go.layer = _weaponLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.maxParticles = 200;
            main.gravityModifier = -0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.rateOverTime = rate;
            em.SetBursts(new ParticleSystem.Burst[0]);

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.015f;
            sh.randomDirectionAmount = 0.12f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 0.3f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a * 0.4f, 0f), new GradientAlphaKey(color.a, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1.1f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var ren = go.GetComponent<ParticleSystemRenderer>();
            ren.material = mat;
            ren.renderMode = ParticleSystemRenderMode.Billboard;
            ren.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
            Destroy(go, duration + life + 0.5f);
        }

        private Transform SpawnBurst(Transform parent, Vector3 worldPos, Material mat, int count, float life, float size, Color color, float speedMin, float speedMax, float gravity)
        {
            var go = new GameObject("VFX_Burst");
            go.layer = _weaponLayer;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = worldPos;
            }
            else
            {
                go.transform.position = worldPos;
            }

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = Mathf.Max(count * 4, 4);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.03f;
            sh.randomDirectionAmount = 1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var ren = go.GetComponent<ParticleSystemRenderer>();
            ren.material = mat;
            ren.renderMode = ParticleSystemRenderMode.Billboard;
            ren.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
            Destroy(go, life + 0.4f);
            return go.transform;
        }

        private static Material CreateParticleMat(Texture2D tex)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (sh.name.StartsWith("Universal"))
            {
                m.SetFloat("_Surface", 1f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            return m;
        }

        private static Material CreateDecalMat(Texture2D tex)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)CullMode.Back);
            m.renderQueue = (int)RenderQueue.Transparent;
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            return m;
        }

        private static Material CreateLineMat()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            return m;
        }
    }
}
