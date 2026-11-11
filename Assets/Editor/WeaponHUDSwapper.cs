using UnityEditor;
using UnityEngine;
using PrisaControlada.Player;

namespace PrisaControlada.EditorTools
{
    // Elimina el HUD del revolver (WeaponHUD + WeaponHUD_Canvas) del prefab del
    // jugador y agrega el nuevo AmmoOverviewHUD (municion general con Tab).
    public static class WeaponHUDSwapper
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";

        [MenuItem("PrisaControlada/Swap WeaponHUD -> AmmoOverviewHUD")]
        public static void Swap()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null)
            {
                Debug.LogError("[HUDSwap] No se pudo abrir: " + PlayerPrefabPath);
                return;
            }

            int removedComponents = 0;
            int removedObjects = 0;

            // 1) Eliminar componentes WeaponHUD (aunque esten en hijos).
            WeaponHUD[] huds = root.GetComponentsInChildren<WeaponHUD>(true);
            foreach (WeaponHUD hud in huds)
            {
                Object.DestroyImmediate(hud);
                removedComponents++;
            }

            // 2) Eliminar el GameObject WeaponHUD_Canvas completo (con sus hijos).
            Transform canvasT = FindDeep(root.transform, "WeaponHUD_Canvas");
            if (canvasT != null)
            {
                Object.DestroyImmediate(canvasT.gameObject);
                removedObjects++;
            }

            // 3) Agregar el nuevo HUD de municion general en la raiz del jugador.
            AmmoOverviewHUD overview = root.GetComponent<AmmoOverviewHUD>();
            if (overview == null)
                overview = root.AddComponent<AmmoOverviewHUD>();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log($"[HUDSwap] Listo. WeaponHUD removidos: {removedComponents} | " +
                      $"canvas eliminados: {removedObjects} | AmmoOverviewHUD agregado.");
            EditorUtility.DisplayDialog("HUD",
                $"HUD del revolver eliminado ({removedComponents} componentes, {removedObjects} canvas).\n" +
                "Nuevo AmmoOverviewHUD agregado (Tab para ver munición).", "OK");
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
