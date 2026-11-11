using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Reconstruye el prefab RunnerEnemy para que use el modelo real con UVs +
    // esqueleto (Assets/MyAssets/runer/Runner40k_Rigged.fbx, la descarga de
    // Mixamo con piel): body con SkinnedMeshRenderer, altura 2 m con los pies
    // en y=0, material Runner_Lit y Animator con RunnerAnimator + Avatar del
    // propio FBX (los clips viejos de Anim/ se reproducen por nombre de hueso).
    //
    // Uso: Tools > PrisaControlada > Replace Runner Prefab Model (Runer Real)
    public static class ReplaceRunnerPrefabModel
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/RunnerEnemy.prefab";
        private const string NewModelPath = "Assets/MyAssets/runer/Runner40k_Rigged.fbx";
        private const string MatPath = "Assets/Materials/Enemies/Runner_Lit.mat";
        private const string ControllerPath = "Assets/Resources/RunnerAnimator.controller";
        private const float TargetHeight = 2f;

        [MenuItem("Tools/PrisaControlada/Replace Runner Prefab Model (Runer Real)", false, 200)]
        public static void Replace()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NewModelPath);
            if (model == null)
            {
                Debug.LogError("[ReplaceRunner] No se encontro el modelo: " + NewModelPath +
                    "\nSubi Assets/MyAssets/runer/RunerUvs(creo).fbx a mixamo.com, riggealo y " +
                    "descarga 'Unarmed Idle' con Skin: With Skin bajo ese nombre.");
                return;
            }

            EnsureGenericRig(NewModelPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[ReplaceRunner] No se encontro el prefab: " + PrefabPath);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Borrar el body viejo: cualquier hijo directo con renderer o llamado "Body".
            for (int i = instance.transform.childCount - 1; i >= 0; i--)
            {
                var child = instance.transform.GetChild(i);
                if (child.name == "Body" || child.GetComponentInChildren<Renderer>(true) != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            // Instanciar el modelo nuevo como child "Body".
            var newBody = (GameObject)PrefabUtility.InstantiatePrefab(model);
            newBody.name = "Body";
            newBody.transform.SetParent(instance.transform, false);
            newBody.transform.localPosition = Vector3.zero;
            newBody.transform.localRotation = Quaternion.identity;
            newBody.transform.localScale = Vector3.one;

            // Altura 2 m y pies en y=0 (mismo criterio que EnemyFactory).
            FitHeightAndSnap(newBody);

            // Material del runer en todos los renderers (SkinnedMeshRenderer incluido).
            int renderers = 0;
            if (mat != null)
            {
                foreach (var r in newBody.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    renderers++;
                }
            }

            // Animator en la raiz del body: EnemyBase hace Find("Body").GetComponent<Animator>().
            var animator = newBody.GetComponent<Animator>();
            if (animator == null) animator = newBody.AddComponent<Animator>();
            if (ctrl != null) animator.runtimeAnimatorController = ctrl;
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(NewModelPath);
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ReplaceRunner] Prefab actualizado -> " + NewModelPath +
                $" | avatar: {(avatar != null ? avatar.name : "NINGUNO")}" +
                $" | material: {(mat != null ? mat.name : "ninguno")} en {renderers} renderer(s)" +
                $" | controller: {(ctrl != null ? ctrl.name : "NINGUNO")}");
        }

        // Garantiza Rig Generic + Avatar en el FBX (sino los clips no se reproducen)
        // y desactiva Optimize Game Objects (EnemyBase busca mixamorig:Head por nombre).
        private static void EnsureGenericRig(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            bool cambia = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                cambia = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                cambia = true;
            }
            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false;
                cambia = true;
            }

            if (cambia)
            {
                importer.SaveAndReimport();
                Debug.Log("[ReplaceRunner] Import settings corregidos en " + path + " (Rig: Generic + Avatar).");
            }
        }

        // Escala el modelo para que mida TargetHeight y baja hasta que los pies
        // queden en y = 0. Mide una sola vez (bounds del renderer; si el modo
        // editor todavia no los calculo, usa la malla).
        private static void FitHeightAndSnap(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[ReplaceRunner] El modelo no tiene renderers.");
                return;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            float altura = b.size.y;
            float piesLocales;
            if (altura >= 0.001f)
            {
                piesLocales = root.transform.InverseTransformPoint(
                    new Vector3(root.transform.position.x, b.min.y, root.transform.position.z)).y;
            }
            else
            {
                var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null || smr.sharedMesh == null)
                {
                    Debug.LogWarning("[ReplaceRunner] No se pudo medir el modelo; se deja escala 1.");
                    return;
                }
                altura = smr.sharedMesh.bounds.size.y;
                piesLocales = smr.sharedMesh.bounds.min.y;
                Debug.LogWarning("[ReplaceRunner] Bounds del renderer sin calcular en modo editor; se uso la malla.");
            }

            if (altura < 0.001f)
            {
                Debug.LogWarning("[ReplaceRunner] Altura invalida; se deja escala 1.");
                return;
            }

            float s = TargetHeight / altura;
            root.transform.localScale = new Vector3(s, s, s);

            var lp = root.transform.localPosition;
            root.transform.localPosition = new Vector3(lp.x, lp.y - s * piesLocales, lp.z);

            Debug.Log($"[ReplaceRunner] Altura {altura:0.00} m -> escala {s:0.00} (objetivo {TargetHeight:0.00} m), pies ajustados a y=0.");
        }
    }
}
