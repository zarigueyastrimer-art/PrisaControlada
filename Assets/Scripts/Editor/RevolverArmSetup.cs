using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    // Unity 6 no permite dos componentes Animator en el mismo GameObject.
    // El brazo se anima en una CAPA "Arm" del MISMO RevolverAnimator.controller
    // (que ya usa el Animator del root del arma). Este tool solo reconstruye
    // el controller (arma + capa de brazo); la escena lo referencia por guid.
    public static class RevolverArmSetup
    {
        private const string ArmClipPath = "Assets/Animations/Revolver/RevolverArmReload.anim";
        private const string OldArmControllerPath = "Assets/Animators/RevolverArmAnimator.controller";

        [MenuItem("Tools/PrisaControlada/Weapons/Setup Revolver Arm (capa en el controller)")]
        public static void SetupArm()
        {
            var controller = RevolverSetup.RebuildControllerAsset(out var clips, out var missing);

            var sb = new StringBuilder();
            sb.AppendLine("[RevolverArmSetup] Controller reconstruido (incluye la capa 'Arm'):");
            foreach (var kv in clips)
                sb.AppendLine($"  {kv.Key} -> {kv.Value.name} ({kv.Value.length:0.00}s)");

            sb.AppendLine("\nANIMACIONES FALTANTES:");
            if (missing.Count == 0) sb.AppendLine("  (ninguna)");
            else foreach (var m in missing) sb.AppendLine("  - " + m);

            sb.AppendLine("\nCapas del controller:");
            if (controller != null)
                for (int i = 0; i < controller.layers.Length; i++)
                    sb.AppendLine($"  [{i}] {controller.layers[i].name}");

            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                var weapon = cam.transform.Find("RevolverWeapon");
                if (weapon != null)
                {
                    var animator = weapon.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.runtimeAnimatorController = controller;
                        sb.AppendLine($"\nAnimator del arma en la escena actualizado (capas: {controller.layers.Length}).");
                    }
                    else
                    {
                        sb.AppendLine("\nWARN: RevolverWeapon no tiene Animator en la escena.");
                    }
                }
                else
                {
                    sb.AppendLine("\nWARN: no hay RevolverWeapon bajo la camara; ejecuta 'Setup Revolver' primero.");
                }
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OldArmControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(OldArmControllerPath);
                sb.AppendLine("\nEliminado el controller obsoleto: " + OldArmControllerPath);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log(sb.ToString());
        }
    }
}