using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisaControlada.EditorTools
{
    public static class RevolverDiagnose
    {
        private const string RevolverPath = "Assets/MyAssets/guns/revolver/revolver.fbx";
        private const string ControllerPath = "Assets/Animators/RevolverAnimator.controller";
        private const string ReportPath = "Assets/MyAssets/guns/revolver/DIAGNOSE_Revolver.txt";

        [MenuItem("Tools/PrisaControlada/Weapons/Diagnose & Repair Revolver")]
        public static void DiagnoseAndRepair()
        {
            var sb = new StringBuilder();
            try
            {
                bool repaired = false;

                // 1. Clips del FBX
                sb.AppendLine("== CLIPS FBX ==");
                AnimationClip getClip = null;
                var clips = new Dictionary<string, AnimationClip>();
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RevolverPath))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        int curves = AnimationUtility.GetCurveBindings(clip).Length;
                        sb.AppendLine($"  {clip.name}  len={clip.length:0.00}s  curves={curves}");
                        clips[clip.name] = clip;
                        if (getClip == null && clip.name.ToLowerInvariant().Contains("get"))
                            getClip = clip;
                    }
                }

                // 2. Takes que Unity ve en el FBX (importer)
                sb.AppendLine("\n== TAKES DEL FBX SEGUN EL IMPORTER ==");
                var imp = (ModelImporter)AssetImporter.GetAtPath(RevolverPath);
                if (imp == null)
                {
                    sb.AppendLine("  no se pudo cargar el importer");
                }
                else
                {
                    var defs = imp.defaultClipAnimations;
                    sb.AppendLine($"  defaultClipAnimations ({defs.Length}):");
                    foreach (var d in defs)
                        sb.AppendLine($"    {d.name}");
                    var cfg = imp.clipAnimations;
                    sb.AppendLine($"  clipAnimations configurados ({cfg.Length}):");
                    foreach (var c in cfg)
                        sb.AppendLine($"    {c.name}");
                }
                sb.AppendLine("\n== BINDINGS (rutas unicas por clip) ==");
                var roleClips = new (string label, string match)[]
                {
                    ("get", "get"),
                    ("idle", "idel"),
                    ("walk", "walk"),
                    ("shoot", "shoot"),
                    ("reload", "reload"),
                };
                foreach (var rc in roleClips)
                {
                    AnimationClip c = null;
                    foreach (var kv in clips)
                        if (kv.Key.ToLowerInvariant().Contains(rc.match) && c == null)
                            c = kv.Value;
                    if (c == null) { sb.AppendLine($"  {rc.label}: (no clip)"); continue; }
                    var paths = new HashSet<string>();
                    foreach (var b in AnimationUtility.GetCurveBindings(c))
                        paths.Add(b.path);
                    sb.AppendLine($"  {rc.label} ({c.name}): {paths.Count} rutas unicas");
                    foreach (var p in paths)
                        sb.AppendLine($"    {p}");
                }

                sb.AppendLine("\n== BINDINGS CLIP DEL BRAZO (root|reloadArm1 y RevolverArmReload) ==");
                foreach (string needle in new[] { "root|reloadArm1", "RevolverArmReload" })
                {
                    AnimationClip ac = clips.TryGetValue(needle, out var c0) ? c0
                        : AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Revolver/RevolverArmReload.anim");
                    if (ac == null) { sb.AppendLine($"  {needle}: (no encontrado)"); continue; }
                    var apaths = new HashSet<string>();
                    foreach (var b in AnimationUtility.GetCurveBindings(ac))
                        apaths.Add(b.path);
                    sb.AppendLine($"  {needle} ({ac.length:0.00}s): {apaths.Count} rutas unicas, {AnimationUtility.GetCurveBindings(ac).Length} curvas");
                    foreach (var p in apaths)
                        sb.AppendLine($"    {p}");
                }

                // 3. Controller: detectar motions rotas
                sb.AppendLine("\n== CONTROLLER ==");
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                int nullMotions = 0;
                if (controller == null)
                {
                    sb.AppendLine("  NO EXISTE - se regenerara");
                    nullMotions = -1;
                }
                else
                {
                    sb.AppendLine($"  {controller.name} - {controller.layers.Length} capas");
                    int li = 0;
                    foreach (var layer in controller.layers)
                    {
                        sb.AppendLine($"  CAPA [{li}] '{layer.name}' weight={layer.defaultWeight}");
                        li++;
                        foreach (var st in layer.stateMachine.states)
                        {
                            var m = st.state.motion;
                            if (m == null && !st.state.writeDefaultValues) nullMotions++;
                            string speed = m == null ? "" : $" speed={st.state.speed:0.00}";
                            sb.AppendLine($"    {st.state.name}: motion={(m == null ? "(NULL)" : m.name)}{speed} wd={st.state.writeDefaultValues}");
                        }
                    }
                    sb.AppendLine($"  motions NULL (sin contar Empty): {nullMotions}");
                }

                // 4. Arma en escena
                sb.AppendLine("\n== ARMA EN ESCENA ==");
                var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindObjectOfType<Camera>();
                if (cam == null)
                {
                    sb.AppendLine("  no hay camara principal");
                }
                else
                {
                    var weapon = cam.transform.Find("RevolverWeapon");
                    if (weapon == null)
                    {
                        sb.AppendLine("  RevolverWeapon NO esta bajo la camara");
                    }
                    else
                    {
                        var anim = weapon.GetComponent<Animator>();
                        sb.AppendLine($"  RevolverWeapon pos={weapon.localPosition} rot={weapon.localRotation.eulerAngles}");
                        sb.AppendLine($"  Animator en el root del arma: {(anim != null ? "SI" : "NO")}");
                        if (anim != null)
                        {
                            sb.AppendLine($"    controller asignado: {(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}");
                            if (anim.runtimeAnimatorController is AnimatorController rac)
                                sb.AppendLine($"    capas del controller asignado: {rac.layers.Length}");
                        }

                        // 5. Verificar resolucion de bindings desde el root del arma
                        if (getClip != null)
                        {
                            var bs = AnimationUtility.GetCurveBindings(getClip);
                            int resolved = 0;
                            var unresolved = new List<string>();
                            foreach (var b in bs)
                            {
                                if (weapon.Find(b.path) != null) resolved++;
                                else if (unresolved.Count < 5) unresolved.Add(b.path);
                            }
                            sb.AppendLine($"  bindings que resuelven desde el root del arma (get): {resolved}/{bs.Length}");
                            foreach (var u in unresolved)
                                sb.AppendLine($"    NO resuelve: {u}");
                        }

                        // 5b. Resolucion de bindings del clip del BRAZO desde el root del arma
                        var armClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Revolver/RevolverArmReload.anim");
                        if (armClip != null)
                        {
                            var abs = AnimationUtility.GetCurveBindings(armClip);
                            int resolvedArm = 0;
                            var unresolvedArm = new List<string>();
                            foreach (var b in abs)
                            {
                                if (weapon.Find(b.path) != null) resolvedArm++;
                                else if (unresolvedArm.Count < 8) unresolvedArm.Add(b.path);
                            }
                            sb.AppendLine($"  bindings que resuelven desde el root del arma (brazo): {resolvedArm}/{abs.Length}");
                            foreach (var u in unresolvedArm)
                                sb.AppendLine($"    NO resuelve: {u}");
                        }

                        // 6. SkinnedMeshRenderers: que huesos usa cada mesh
                        sb.AppendLine("\n  SkinnedMeshRenderers del arma:");
                        foreach (var smr in weapon.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            sb.AppendLine($"    {smr.name}: {smr.bones.Length} huesos");
                            foreach (var bone in smr.bones)
                            {
                                string rel = bone.name;
                                Transform t = bone;
                                while (t != null && t != weapon)
                                {
                                    rel = t.name + "/" + rel;
                                    t = t.parent;
                                }
                                sb.AppendLine($"      {rel}");
                            }
                        }

                        // REPARACION 1: Animator en el root del arma
                        if (anim == null)
                        {
                            anim = weapon.gameObject.AddComponent<Animator>();
                            repaired = true;
                            sb.AppendLine("  -> Animator agregado al root del arma");
                        }
                        if (anim != null && controller != null && anim.runtimeAnimatorController == null)
                        {
                            anim.runtimeAnimatorController = controller;
                            repaired = true;
                            sb.AppendLine("  -> Controller asignado al Animator");
                        }
                    }
                }

                // REPARACION 2: regenerar controller si hace falta
                if (controller == null || nullMotions > 0)
                {
                    sb.AppendLine("\n  -> Regenerando controller (referencias rotas)...");
                    var rebuilt = RevolverSetup.RebuildControllerAsset(out var newClips, out var missing);
                    sb.AppendLine("     controller regenerado. Faltantes: " + (missing.Count == 0 ? "ninguna" : string.Join(", ", missing)));
                    controller = rebuilt;
                    if (getClip == null) getClip = newClips.TryGetValue("get", out var g) ? g : null;
                    repaired = true;

                    foreach (var a in UnityEngine.Object.FindObjectsOfType<Animator>())
                        if (a.runtimeAnimatorController != null && a.runtimeAnimatorController.name.StartsWith("RevolverAnimator"))
                            a.runtimeAnimatorController = controller;
                }

                if (repaired)
                {
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    sb.AppendLine("\n== SE APLICARON REPARACIONES - proba de nuevo ==");
                }
                else
                {
                    sb.AppendLine("\n== TODO OK en escena. Si sigue sin animar, el problema es del controller/clips. ==");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("\n== EXCEPCION ==");
                sb.AppendLine(e.ToString());
            }

            string folder = Path.GetDirectoryName(ReportPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                int idx = folder.LastIndexOf('/');
                if (idx > 0) AssetDatabase.CreateFolder(folder.Substring(0, idx), folder.Substring(idx + 1));
            }
            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.Refresh();

            Debug.Log("[RevolverDiagnose] Reporte: " + ReportPath + "\n" + sb.ToString());
        }
    }
}