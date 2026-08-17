using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    // Crea los materiales del revolver (arma y brazos) desde las texturas de
    // Assets/MyAssets/guns/revolver/textures y arms/textures, y los aplica a los
    // renderers (skinned o mesh) del viewmodel en la escena, del Player.prefab y
    // del RevolverPickup.prefab.
    public static class RevolverMaterialsSetup
    {
        private const string MatFolder = "Assets/MyAssets/guns/revolver/Materials";
        private const string GunMatPath = MatFolder + "/RevolverGun.mat";
        private const string ArmsMatPath = MatFolder + "/RevolverArms.mat";

        private const string GunBase = "Assets/MyAssets/guns/revolver/textures/Magnum_BaseColor.png";
        private const string GunNormal = "Assets/MyAssets/guns/revolver/textures/Magnum_Normal.png";
        private const string GunMetallic = "Assets/MyAssets/guns/revolver/textures/Magnum_Metallic.png";
        private const string GunAo = "Assets/MyAssets/guns/revolver/textures/Magnum_AO.png";

        private const string ArmsBase = "Assets/MyAssets/guns/revolver/arms/textures/arm2Color.png";
        private const string ArmsNormal = "Assets/MyAssets/guns/revolver/arms/textures/arm2Normal.png";

        private const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";
        private const string PickupPrefabPath = "Assets/Prefabs/items/RevolverPickup.prefab";

        [MenuItem("Tools/PrisaControlada/Weapons/Setup Revolver Materials")]
        public static void SetupMaterials()
        {
            Material gunMat = BuildMaterial(GunMatPath, "RevolverGun", GunBase, GunNormal, GunMetallic, GunAo, 0.45f);
            Material armsMat = BuildMaterial(ArmsMatPath, "RevolverArms", ArmsBase, ArmsNormal, null, null, 0.6f, false);
            if (gunMat == null || armsMat == null)
            {
                Debug.LogError("[RevolverMaterialsSetup] Faltan texturas o shader URP. No se aplicaron materiales.");
                return;
            }

            int scene = ApplyInScene(gunMat, armsMat);
            int player = ApplyInPrefab(PlayerPrefabPath, gunMat, armsMat);
            int pickup = ApplyInPrefab(PickupPrefabPath, gunMat, armsMat);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[RevolverMaterialsSetup] Materiales listos: {GunMatPath} y {ArmsMatPath}. Renderers afectados -> escena: {scene}, Player.prefab: {player}, RevolverPickup.prefab: {pickup}");
        }

        // ---------- Creacion de materiales ----------

        private static Material BuildMaterial(string assetPath, string displayName,
            string basePath, string normalPath, string metallicPath, string aoPath,
            float smoothness, bool metallic = true)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[RevolverMaterialsSetup] No existe el shader 'Universal Render Pipeline/Lit'. Revisa el URP del proyecto.");
                return null;
            }

            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            if (baseTex == null) return null;

            EnsureFolder(MatFolder);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = displayName };
                AssetDatabase.CreateAsset(mat, assetPath);
            }

            mat.shader = shader;
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);

            Texture2D normalTex = normalPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            Texture2D metallicTex = metallicPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath) : null;
            if (metallicTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicTex);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                mat.SetFloat("_Metallic", metallic ? 0.7f : 0f);
                mat.DisableKeyword("_METALLICGLOSSMAP");
            }

            Texture2D aoTex = aoPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath) : null;
            if (aoTex != null)
            {
                mat.SetTexture("_OcclusionMap", aoTex);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Aplicacion ----------

        private static int ApplyInScene(Material gunMat, Material armsMat)
        {
            int count = 0;
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
                if (TryApplyByRenderer(r, gunMat, armsMat)) count++;
            return count;
        }

        private static int ApplyInPrefab(string prefabPath, Material gunMat, Material armsMat)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null) return 0;

            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null) return 0;

            int count = 0;
            foreach (Renderer r in contents.GetComponentsInChildren<Renderer>(true))
                if (TryApplyByRenderer(r, gunMat, armsMat)) count++;

            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                Debug.Log("[RevolverMaterialsSetup] Prefab actualizado: " + prefabPath);
            }
            PrefabUtility.UnloadPrefabContents(contents);
            return count;
        }

        private static bool TryApplyByRenderer(Renderer renderer, Material gunMat, Material armsMat)
        {
            string n = renderer.gameObject.name.ToLowerInvariant();
            Material target = null;
            if (n.Contains("arm")) target = armsMat;
            else if (n.Contains("gun")) target = gunMat;

            if (target == null)
            {
                // El renderer puede estar anidado (ej: RevolverGunMesh/Magnum357_Mesh):
                // se busca un ancestro cuyo nombre indique arma o brazo.
                Transform t = renderer.transform.parent;
                while (t != null && target == null)
                {
                    string pn = t.name.ToLowerInvariant();
                    if (pn.Contains("arm")) target = armsMat;
                    else if (pn.Contains("gun")) target = gunMat;
                    t = t.parent;
                }
            }

            if (target == null) return false;

            var mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) mats = new Material[1];
            for (int i = 0; i < mats.Length; i++) mats[i] = target;
            renderer.sharedMaterials = mats;
            EditorUtility.SetDirty(renderer);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            if (idx <= 0) return;
            string parent = path.Substring(0, idx);
            string name = path.Substring(idx + 1);
            EnsureFolder(parent);
            if (AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}