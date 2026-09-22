using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using PrisaControlada.Player;
using PrisaControlada.Items;

namespace PrisaControlada.Editor
{
    public static class FlashlightWeaponSetup
    {
        const string ARM_FBX = "Assets/MyAssets/flash/flashAnimation.fbx";
        const string FLASH_FBX = "Assets/MyAssets/flash/Flashlight.fbx";
        const string TEX_FOLDER = "Assets/MyAssets/flash/texturas/";
        const string PREFAB_PATH = "Assets/Prefabs/items/FlashlightWeapon.prefab";
        const string MAT_PATH = "Assets/Materials/Items/Flashlight.mat";
        const string CTRL_PATH = "Assets/Resources/FlashlightAnimator.controller";
        const string PLAYER_PATH = "Assets/Prefabs/player/Player.prefab";
        const int WeaponLayer = 8;

        [MenuItem("Tools/PrisaControlada/Weapons/Build Flashlight Weapon")]
        public static void BuildFlashlightWeapon()
        {
            BuildPrefab();
            try { PlaceInPlayerPrefab(); }
            catch (Exception ex) { Debug.LogError("[Flashlight] Inyeccion en Player.prefab fallo: " + ex); }
            try { PlaceInOpenScene(); }
            catch (Exception ex) { Debug.LogError("[Flashlight] Inyeccion en escena fallo: " + ex); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Flashlight] Setup completado.");
        }

        const string PICKUP_PATH = "Assets/Prefabs/items/FlashlightPickup.prefab";

        [MenuItem("Tools/PrisaControlada/Weapons/Build Flashlight Pickup")]
        public static void BuildFlashlightPickup()
        {
            var flashFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FLASH_FBX);
            if (flashFbx == null) { Debug.LogError("[Flashlight] No se encontro: " + FLASH_FBX); return; }

            var root = PrefabUtility.InstantiatePrefab(flashFbx) as GameObject;
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "FlashlightPickup";

            var flashMat = CreateFlashlightMaterial();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = flashMat;
                r.sharedMaterials = mats;
            }

            root.AddComponent<FlashlightGroundPickup>();
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.linearDamping = 1f;
            rb.angularDamping = 0.5f;

            var col = root.GetComponent<SphereCollider>();
            if (col == null) col = root.AddComponent<SphereCollider>();
            col.radius = 0.15f;
            col.center = Vector3.zero;

            CreatePickupLights(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PICKUP_PATH);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[Flashlight] Pickup creado: " + PICKUP_PATH);
        }

        static void CreatePickupLights(Transform parent)
        {
            var fwd = Vector3.forward;

            var spotMain = new GameObject("SpotMain");
            spotMain.transform.SetParent(parent, false);
            spotMain.transform.localRotation = Quaternion.LookRotation(fwd);
            var l1 = spotMain.AddComponent<Light>();
            l1.type = LightType.Spot;
            l1.color = new Color(1f, 0.96f, 0.84f);
            l1.intensity = 8f;
            l1.range = 40f;
            l1.spotAngle = 30f;
            l1.innerSpotAngle = 18f;
            l1.cookieSize = 1f;
            l1.enabled = false;

            var spotMid = new GameObject("SpotMid");
            spotMid.transform.SetParent(parent, false);
            spotMid.transform.localRotation = Quaternion.LookRotation(fwd);
            var l2 = spotMid.AddComponent<Light>();
            l2.type = LightType.Spot;
            l2.color = new Color(1f, 0.96f, 0.84f);
            l2.intensity = 3f;
            l2.range = 20f;
            l2.spotAngle = 55f;
            l2.innerSpotAngle = 33f;
            l2.cookieSize = 0.5f;
            l2.enabled = false;

            var spotWide = new GameObject("SpotWide");
            spotWide.transform.SetParent(parent, false);
            spotWide.transform.localRotation = Quaternion.LookRotation(fwd);
            var l3 = spotWide.AddComponent<Light>();
            l3.type = LightType.Spot;
            l3.color = new Color(1f, 0.96f, 0.84f);
            l3.intensity = 1.5f;
            l3.range = 12f;
            l3.spotAngle = 80f;
            l3.innerSpotAngle = 48f;
            l3.cookieSize = 0.3f;
            l3.enabled = false;
        }

        static void BuildPrefab()
        {
            var armFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ARM_FBX);
            if (armFbx == null) { Debug.LogWarning("[Flashlight] No se encontro: " + ARM_FBX); return; }

            var flashFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FLASH_FBX);
            if (flashFbx == null) { Debug.LogWarning("[Flashlight] No se encontro: " + FLASH_FBX); return; }

            Vector3 prevRootPos = Vector3.zero, prevModelPos = Vector3.zero, prevFlashPos = Vector3.zero;
            Quaternion prevRootRot = Quaternion.identity, prevModelRot = Quaternion.identity, prevFlashRot = Quaternion.identity;
            Vector3 prevModelScale = Vector3.one, prevFlashScale = Vector3.one;
            bool hasPrev = false;
            var prevPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prevPrefab != null)
            {
                hasPrev = true;
                prevRootPos = prevPrefab.transform.localPosition;
                prevRootRot = prevPrefab.transform.localRotation;
                var prevModel = prevPrefab.transform.GetChild(0);
                if (prevModel != null)
                {
                    prevModelPos = prevModel.localPosition;
                    prevModelRot = prevModel.localRotation;
                    prevModelScale = prevModel.localScale;
                    var prevFlash = FindChildRecursive(prevModel, "Flashlight");
                    if (prevFlash != null)
                    {
                        prevFlashPos = prevFlash.localPosition;
                        prevFlashRot = prevFlash.localRotation;
                        prevFlashScale = prevFlash.localScale;
                    }
                }
            }

            var model = PrefabUtility.InstantiatePrefab(armFbx) as GameObject;
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();

            // Crea el controller con los clips del FBX.
            var clips = FindArmClips();
            var ctrl = BuildController(clips);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.enabled = true;

            // Pone el brazo en pose de reposo.
            anim.Update(0f);

            var handR = FindBone(model.transform, "hand.r");
            if (handR == null)
            {
                Debug.LogWarning("[Flashlight] No se encontro 'hand.r' en " + ARM_FBX);
                UnityEngine.Object.DestroyImmediate(model);
                return;
            }

            var flashlight = PrefabUtility.InstantiatePrefab(flashFbx) as GameObject;
            PrefabUtility.UnpackPrefabInstance(flashlight, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            flashlight.transform.SetParent(handR, false);
            flashlight.transform.localPosition = Vector3.zero;
            flashlight.transform.localRotation = Quaternion.identity;
            flashlight.transform.localScale = new Vector3(0.0002f, 0.0002f, 0.0002f);

            var flashMat = CreateFlashlightMaterial();
            foreach (var r in flashlight.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = flashMat;
                r.sharedMaterials = mats;
            }

            var root = new GameObject("FlashlightWeapon");
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

            root.layer = WeaponLayer;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = WeaponLayer;

            root.AddComponent<FlashlightToggle>();
            root.AddComponent<WeaponSway>();

            CreateFlashlightLights(root.transform);

            if (hasPrev)
            {
                root.transform.localPosition = prevRootPos;
                root.transform.localRotation = prevRootRot;
                model.transform.localPosition = prevModelPos;
                model.transform.localRotation = prevModelRot;
                model.transform.localScale = prevModelScale;
                flashlight.transform.localPosition = prevFlashPos;
                flashlight.transform.localRotation = prevFlashRot;
                flashlight.transform.localScale = prevFlashScale;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("[Flashlight] Prefab creado: " + PREFAB_PATH);
        }

        // Busca clips de animacion del brazo en el FBX: clips que contengan "arm"
        // o que sean los primeros clips generici找到了.
        static AnimationClip[] FindArmClips()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(ARM_FBX);
            var clips = new List<AnimationClip>();
            foreach (var a in all)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview"))
                {
                    c.wrapMode = WrapMode.Loop;
                    var settings = AnimationUtility.GetAnimationClipSettings(c);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(c, settings);
                    clips.Add(c);
                }
            }
            Debug.Log("[Flashlight] Clips encontrados en " + ARM_FBX + ": " + clips.Count +
                      (clips.Count > 0 ? " (" + clips[0].name + (clips.Count > 1 ? ", " + clips[1].name : "") + ")" : ""));
            return clips.ToArray();
        }

        static AnimatorController BuildController(AnimationClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning("[Flashlight] No hay clips de animacion en " + ARM_FBX);
                return null;
            }

            var dir = Path.GetDirectoryName(CTRL_PATH);
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Resources");

            if (File.Exists(CTRL_PATH)) AssetDatabase.DeleteAsset(CTRL_PATH);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL_PATH);

            // Si hay mas de un clip, crea un blend tree 1D por "Speed" (idle/walk/run).
            // Si hay uno solo, lo usa como estado unico.
            var sm = ctrl.layers[0].stateMachine;

            if (clips.Length == 1)
            {
                var state = sm.AddState("Default", new Vector3(300, 0, 0));
                state.motion = clips[0];
                sm.defaultState = state;
            }
            else
            {
                // Primer clip = idle (umbral 0), segundo = walk/run.
                var bt = new BlendTree();
                bt.name = "ArmBlend";
                bt.blendType = BlendTreeType.Simple1D;
                bt.blendParameter = "Speed";
                bt.useAutomaticThresholds = false;
                bt.AddChild(clips[0], 0f);
                if (clips.Length > 1) bt.AddChild(clips[1], 1f);
                if (clips.Length > 2) bt.AddChild(clips[2], 2f);

                var state = sm.AddState("ArmBlend", new Vector3(300, 0, 0));
                state.motion = bt;
                sm.defaultState = state;
                AssetDatabase.AddObjectToAsset(bt, ctrl);

                ctrl.parameters = new[]
                {
                    new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f }
                };
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CTRL_PATH, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Flashlight] Controller creado: " + CTRL_PATH + " (" + clips.Length + " clips)");
            return ctrl;
        }

        static Transform FindChildRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindChildRecursive(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static void PlaceInPlayerPrefab()
        {
            var playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PATH);
            if (playerAsset == null) { Debug.LogError("[Flashlight] No existe " + PLAYER_PATH); return; }

            var playerRoot = PrefabUtility.LoadPrefabContents(PLAYER_PATH);
            try
            {
                Camera mainCam = null;
                foreach (var cam in playerRoot.GetComponentsInChildren<Camera>(true))
                {
                    if (cam.CompareTag("MainCamera")) { mainCam = cam; break; }
                }
                if (mainCam == null)
                {
                    Debug.LogError("[Flashlight] No se encontro MainCamera en Player. Cameras encontradas:");
                    foreach (var cam in playerRoot.GetComponentsInChildren<Camera>(true))
                        Debug.LogError("  " + cam.gameObject.name + " tag=" + cam.tag);
                    return;
                }
                var target = mainCam.transform;
                Debug.Log("[Flashlight] MainCamera encontrada: " + target.name);

                var toRemove = new List<GameObject>();
                foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "FlashlightWeapon") toRemove.Add(t.gameObject);
                foreach (var g in toRemove) UnityEngine.Object.DestroyImmediate(g);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
                if (prefab == null) { Debug.LogError("[Flashlight] No existe " + PREFAB_PATH); return; }

                var inst = UnityEngine.Object.Instantiate(prefab, target);
                inst.name = "FlashlightWeapon";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;

                PrefabUtility.SavePrefabAsset(playerRoot);
                Debug.Log("[Flashlight] FlashlightWeapon anidado en '" + target.name + "'. Guardado.");
            }
            catch (Exception ex) { Debug.LogError("[Flashlight] PlaceInPlayerPrefab error: " + ex); }
            finally { PrefabUtility.UnloadPrefabContents(playerRoot); }
        }

        static void PlaceInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject playerObj = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var pc = root.GetComponentInChildren<PlayerController>(true);
                if (pc != null) { playerObj = pc.gameObject; break; }
            }
            if (playerObj == null) return;

            foreach (var t in playerObj.GetComponentsInChildren<Transform>(true))
                if (t.name == "FlashlightWeapon") UnityEngine.Object.DestroyImmediate(t.gameObject);

            Camera mainCam = null;
            foreach (var cam in playerObj.GetComponentsInChildren<Camera>(true))
            {
                if (cam.CompareTag("MainCamera")) { mainCam = cam; break; }
            }
            if (mainCam == null) { Debug.LogWarning("[Flashlight] No se encontro MainCamera en escena."); return; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (prefab == null) return;

            var inst = PrefabUtility.InstantiatePrefab(prefab, mainCam.transform) as GameObject;
            inst.name = "FlashlightWeapon";
            Debug.Log("[Flashlight] FlashlightWeapon anidado en escena (MainCamera).");
        }

        static Transform FindCamera(Transform root)
        {
            var cam = root.GetComponentInChildren<Camera>(true);
            return cam != null ? cam.transform : root;
        }

        static Material CreateFlashlightMaterial()
        {
            Material mat;
            if (AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH) != null)
                mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
            else
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MAT_PATH);
            }

            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_FOLDER + "T_Flashlight_V01_BaseColor-T_Flashlight_V01_Opacity.png");
            if (baseColor != null) mat.SetTexture("_BaseMap", baseColor);

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_FOLDER + "T_Flashlight_V01_Normal.png");
            if (normal != null) { mat.EnableKeyword("_NORMALMAP"); mat.SetTexture("_BumpMap", normal); }

            var metalRough = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_FOLDER + "T_Flashlight_V01_Metalness-T_Flashlight_V01_Roughness.png");
            if (metalRough != null) { mat.EnableKeyword("_METALLICGLOSSMAP"); mat.SetTexture("_MetallicGlossMap", metalRough); }

            var emissive = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_FOLDER + "T_Flashlight_V01_Emissive.png");
            if (emissive != null) { mat.EnableKeyword("_EMISSION"); mat.SetTexture("_EmissionMap", emissive); mat.SetColor("_EmissionColor", Color.white); }

            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void CreateFlashlightLights(Transform parent)
        {
            var warm = new Color(1f, 0.95f, 0.85f);

            // Luz principal: haz estrecho, mucho rango
            var mainObj = new GameObject("SpotMain");
            mainObj.transform.SetParent(parent, false);
            mainObj.transform.localPosition = Vector3.zero;
            mainObj.transform.localRotation = Quaternion.identity;
            mainObj.layer = WeaponLayer;
            var main = mainObj.AddComponent<Light>();
            main.type = LightType.Spot;
            main.color = warm;
            main.intensity = 8f;
            main.range = 40f;
            main.spotAngle = 30f;
            main.innerSpotAngle = 20f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = 0.7f;
            main.renderingLayerMask = 1;
            main.enabled = false;

            // Luz media: cono medio
            var midObj = new GameObject("SpotMid");
            midObj.transform.SetParent(parent, false);
            midObj.transform.localPosition = Vector3.zero;
            midObj.transform.localRotation = Quaternion.identity;
            midObj.layer = WeaponLayer;
            var mid = midObj.AddComponent<Light>();
            mid.type = LightType.Spot;
            mid.color = warm;
            mid.intensity = 3f;
            mid.range = 20f;
            mid.spotAngle = 55f;
            mid.innerSpotAngle = 40f;
            mid.enabled = false;

            // Luz ambiental: cono amplio, poco rango, rellena oscuridad
            var wideObj = new GameObject("SpotWide");
            wideObj.transform.SetParent(parent, false);
            wideObj.transform.localPosition = Vector3.zero;
            wideObj.transform.localRotation = Quaternion.identity;
            wideObj.layer = WeaponLayer;
            var wide = wideObj.AddComponent<Light>();
            wide.type = LightType.Spot;
            wide.color = warm;
            wide.intensity = 1.5f;
            wide.range = 12f;
            wide.spotAngle = 80f;
            wide.innerSpotAngle = 60f;
            wide.enabled = false;
        }

        static Transform FindBone(Transform root, string bone)
        {
            string b = bone.ToLower().Replace("_", ".");
            return FindPredicate(root, tr =>
            {
                string n = tr.name.ToLower().Replace("_", ".");
                return n == b || n.EndsWith("." + b);
            });
        }

        static Transform FindPredicate(Transform t, Func<Transform, bool> pred)
        {
            if (pred(t)) return t;
            foreach (Transform c in t) { var r = FindPredicate(c, pred); if (r != null) return r; }
            return null;
        }
    }
}
