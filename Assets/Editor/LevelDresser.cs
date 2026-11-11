using UnityEditor;
using UnityEngine;
using System.IO;

namespace PrisaControlada.EditorTools
{
    public static class LevelDresser
    {
        private const string TexFolder = "Assets/Textures/Deco";
        private const string MatFolder = "Assets/Materials/Deco";
        private const string PropsRootName = "Decoracion_Props";

        // Zona del JUEGO (blockout derecho). El centro comercial (izquierda, X < -50) NO se toca.
        private const float GameMinX = -45f;
        private const float GameMaxX = 15f;
        private const float GameMinZ = -45f;
        private const float GameMaxZ = 17f;

        private static bool IsInGameArea(Vector3 worldPos)
        {
            return worldPos.x >= GameMinX && worldPos.x <= GameMaxX
                && worldPos.z >= GameMinZ && worldPos.z <= GameMaxZ;
        }

        [MenuItem("PrisaControlada/Vestir Niveles/Aplicar Texturas (Suelo + Muros del Juego)")]
        public static void ApplyTextures()
        {
            EnsureFolders();

            Texture2D texFloor = LoadTex("brown_floor_tiles_diff_1k.jpg");
            Texture2D texWall = LoadTex("painted_plaster_wall_diff_1k.jpg");
            Texture2D texConcrete = LoadTex("concrete_floor_02_diff_1k.jpg");

            int floors = 0, walls = 0, skipped = 0;

            // ---- SUOLOS: solo Ground* cuyo centro este en la zona del juego ----
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!go.name.StartsWith("Ground")) continue;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                // Saltar suelos del centro comercial (lado izquierdo)
                if (!IsInGameArea(go.transform.position))
                {
                    skipped++;
                    continue;
                }

                // Tiling por tamaño real del plano
                Bounds b = mr.bounds;
                float repeatSize = 2f;
                Vector2 tiling = new Vector2(
                    Mathf.Max(1f, b.size.x / repeatSize),
                    Mathf.Max(1f, (Mathf.Approximately(b.size.z, 0) ? b.size.y : b.size.z) / repeatSize));

                Material mat = CreateMat("Floor_" + go.name.Replace(" ", "_"), texFloor, Color.white, tiling);
                mr.sharedMaterial = mat;
                floors++;
            }

            // ---- MUROS: solo RefRed* cuyo centro este en la zona del juego ----
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || r.sharedMaterial == null) continue;
                if (!r.sharedMaterial.name.Contains("RefRed")) continue;

                // Saltar muros del centro comercial
                if (!IsInGameArea(r.bounds.center))
                {
                    skipped++;
                    continue;
                }

                Vector3 s = r.bounds.size;
                bool isHorizontal = s.y < s.x * 0.4f && s.y < s.z * 0.4f;
                float repeat = 2f;
                Vector2 tiling = new Vector2(
                    Mathf.Max(1f, Mathf.Max(s.x, s.z) / repeat),
                    Mathf.Max(1f, s.y / repeat));

                if (isHorizontal)
                {
                    Material losa = CreateMat("Losa_" + Mathf.RoundToInt(s.x) + "x" + Mathf.RoundToInt(s.z), texConcrete, new Color(0.75f, 0.75f, 0.75f), tiling);
                    r.sharedMaterial = losa;
                }
                else
                {
                    Material wallMat = CreateMat("Wall_Plaster", texWall, new Color(0.88f, 0.86f, 0.80f), new Vector2(2f, 3f));
                    r.sharedMaterial = wallMat;
                }
                walls++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LevelDresser] Suelos: {floors}, muros/losas: {walls}, saltados (centro comercial): {skipped}");
            EditorUtility.DisplayDialog("Vestir Niveles",
                $"Solo zona del juego (derecha):\n- {floors} suelos texturizados\n- {walls} muros/losas texturizados\n- {skipped} objetos del centro comercial ignorados", "OK");
        }

        [MenuItem("PrisaControlada/Vestir Niveles/Colocar Decoracion (solo zona del juego)")]
        public static void PlaceProps()
        {
            EnsureFolders();

            var old = GameObject.Find(PropsRootName);
            if (old != null) Object.DestroyImmediate(old);

            // Materiales
            Material matCrate = CreateMat("Prop_Crate", LoadTex("concrete_wall_008_diff_1k.jpg"), new Color(0.55f, 0.40f, 0.25f), Vector2.one);
            Material matCrate2 = CreateMat("Prop_Crate2", LoadTex("painted_plaster_wall_diff_1k.jpg"), new Color(0.50f, 0.38f, 0.22f), Vector2.one);
            Material matBarrel = CreateMat("Prop_Barrel", LoadTex("anti_skid_tiles_diff_1k.jpg"), new Color(0.25f, 0.40f, 0.25f), Vector2.one);
            Material matBarrelBlue = CreateMat("Prop_BarrelBlue", LoadTex("anti_skid_tiles_diff_1k.jpg"), new Color(0.20f, 0.30f, 0.55f), Vector2.one);
            Material matPot = CreateMat("Prop_Pot", LoadTex("concrete_floor_02_diff_1k.jpg"), new Color(0.50f, 0.35f, 0.25f), Vector2.one);
            Material matPlant = CreateMat("Prop_Plant", null, new Color(0.12f, 0.40f, 0.10f), Vector2.one);
            Material matPlantDry = CreateMat("Prop_PlantDry", null, new Color(0.35f, 0.35f, 0.12f), Vector2.one);
            Material matTrash = CreateMat("Prop_Trash", LoadTex("painted_plaster_wall_diff_1k.jpg"), new Color(0.22f, 0.22f, 0.25f), Vector2.one);
            Material matPallet = CreateMat("Prop_Pallet", LoadTex("concrete_wall_008_diff_1k.jpg"), new Color(0.45f, 0.32f, 0.18f), new Vector2(3, 3));
            Material matBarrier = CreateMat("Prop_Barrier", LoadTex("anti_skid_tiles_diff_1k.jpg"), new Color(0.85f, 0.45f, 0.05f), new Vector2(2, 1));
            Material matSign = CreateMat("Prop_Sign", LoadTex("painted_plaster_wall_diff_1k.jpg"), new Color(0.80f, 0.75f, 0.10f), Vector2.one);
            Material matMetal = CreateMat("Prop_Metal", LoadTex("anti_skid_tiles_diff_1k.jpg"), new Color(0.45f, 0.45f, 0.48f), Vector2.one);
            Material matWood = CreateMat("Prop_Wood", LoadTex("concrete_wall_008_diff_1k.jpg"), new Color(0.40f, 0.28f, 0.15f), new Vector2(4, 1));

            AssetDatabase.SaveAssets();

            var root = new GameObject(PropsRootName);
            Undo.RegisterCreatedObjectUndo(root, "Place Props");
            Random.InitState(777);

            // ===== TODO DENTRO DE LA ZONA DEL JUEGO (derecha) =====

            // Cajas - repartidas por el mapa del juego
            PlaceCrateCluster(root.transform, new Vector3(-35, 0, -35), matCrate, 5);
            PlaceCrateCluster(root.transform, new Vector3(-20, 0, -30), matCrate2, 4);
            PlaceCrateCluster(root.transform, new Vector3(-5, 0, -38), matCrate, 3);
            PlaceCrateCluster(root.transform, new Vector3(10, 0, -25), matCrate2, 4);
            PlaceCrateCluster(root.transform, new Vector3(-30, 0, -15), matCrate, 4);
            PlaceCrateCluster(root.transform, new Vector3(-10, 0, -10), matCrate2, 5);
            PlaceCrateCluster(root.transform, new Vector3(5, 0, -5), matCrate, 3);
            PlaceCrateCluster(root.transform, new Vector3(-25, 0, 5), matCrate2, 4);
            PlaceCrateCluster(root.transform, new Vector3(0, 0, 10), matCrate, 5);
            PlaceCrateCluster(root.transform, new Vector3(12, 0, 5), matCrate2, 3);
            PlaceCrateCluster(root.transform, new Vector3(-38, 0, -20), matCrate, 3);

            // Barriles
            PlaceBarrelGroup(root.transform, new Vector3(-38, 0, -40), matBarrel, 3);
            PlaceBarrelGroup(root.transform, new Vector3(-15, 0, -35), matBarrelBlue, 2);
            PlaceBarrelGroup(root.transform, new Vector3(8, 0, -30), matBarrel, 2);
            PlaceBarrelGroup(root.transform, new Vector3(-28, 0, -8), matBarrelBlue, 3);
            PlaceBarrelGroup(root.transform, new Vector3(-2, 0, -20), matBarrel, 2);
            PlaceBarrelGroup(root.transform, new Vector3(10, 0, 12), matBarrelBlue, 2);
            PlaceBarrelGroup(root.transform, new Vector3(-20, 0, 12), matBarrel, 3);
            PlaceBarrelGroup(root.transform, new Vector3(-40, 0, -10), matBarrelBlue, 2);

            // Plantas
            PlacePlantPot(root.transform, new Vector3(-32, 0, -38), matPot, matPlant);
            PlacePlantPot(root.transform, new Vector3(-12, 0, -32), matPot, matPlantDry);
            PlacePlantPot(root.transform, new Vector3(5, 0, -35), matPot, matPlant);
            PlacePlantPot(root.transform, new Vector3(-25, 0, -18), matPot, matPlantDry);
            PlacePlantPot(root.transform, new Vector3(-5, 0, -8), matPot, matPlant);
            PlacePlantPot(root.transform, new Vector3(10, 0, -15), matPot, matPlantDry);
            PlacePlantPot(root.transform, new Vector3(-18, 0, 2), matPot, matPlant);
            PlacePlantPot(root.transform, new Vector3(0, 0, 14), matPot, matPlantDry);
            PlacePlantPot(root.transform, new Vector3(-35, 0, -5), matPot, matPlant);
            PlacePlantPot(root.transform, new Vector3(12, 0, -3), matPot, matPlantDry);

            // Basura
            PlaceTrashBag(root.transform, new Vector3(-36, 0, -32), matTrash);
            PlaceTrashBag(root.transform, new Vector3(-10, 0, -38), matTrash);
            PlaceTrashBag(root.transform, new Vector3(8, 0, -20), matTrash);
            PlaceTrashBag(root.transform, new Vector3(-22, 0, -5), matTrash);
            PlaceTrashBag(root.transform, new Vector3(-2, 0, 5), matTrash);
            PlaceTrashBag(root.transform, new Vector3(12, 0, 10), matTrash);
            PlaceTrashBag(root.transform, new Vector3(-30, 0, 10), matTrash);

            // Pallets
            PlacePallet(root.transform, new Vector3(-30, 0, -28), matPallet);
            PlacePallet(root.transform, new Vector3(-8, 0, -25), matPallet);
            PlacePallet(root.transform, new Vector3(5, 0, -12), matPallet);
            PlacePallet(root.transform, new Vector3(-15, 0, 8), matPallet);
            PlacePallet(root.transform, new Vector3(-38, 0, 5), matPallet);

            // Barreras
            PlaceBarrier(root.transform, new Vector3(-28, 0, -22), matBarrier);
            PlaceBarrier(root.transform, new Vector3(-5, 0, -30), matBarrier);
            PlaceBarrier(root.transform, new Vector3(10, 0, -8), matBarrier);
            PlaceBarrier(root.transform, new Vector3(-18, 0, -12), matBarrier);

            // Carteles
            PlaceSign(root.transform, new Vector3(-30, 0, -35), matSign, matMetal);
            PlaceSign(root.transform, new Vector3(-5, 0, -15), matSign, matMetal);
            PlaceSign(root.transform, new Vector3(8, 0, 5), matSign, matMetal);

            // Tablones
            PlacePlank(root.transform, new Vector3(-34, 0, -28), matWood);
            PlacePlank(root.transform, new Vector3(-12, 0, -35), matWood);
            PlacePlank(root.transform, new Vector3(3, 0, -28), matWood);
            PlacePlank(root.transform, new Vector3(-20, 0, -2), matWood);

            // Caños
            PlacePipeStack(root.transform, new Vector3(-25, 0, -40), matMetal);
            PlacePipeStack(root.transform, new Vector3(-3, 0, -3), matMetal);

            Debug.Log("[LevelDresser] Decoracion colocada SOLO en la zona del juego.");
            EditorUtility.DisplayDialog("Vestir Niveles",
                "Decoracion colocada SOLO en la zona del juego (derecha)!\n\n- 11 racimos de cajas\n- 8 grupos de barriles\n- 10 macetas\n- 7 bolsas de basura\n- 5 pallets\n- 4 barreras\n- 3 carteles\n- 4 tablones\n- 2 pilas de caños\n\nTodo dentro del area X -45..15, Z -45..17.", "OK");
        }

        [MenuItem("PrisaControlada/Vestir Niveles/Todo (Texturas + Decoracion)")]
        public static void DressAll()
        {
            ApplyTextures();
            PlaceProps();
        }

        // ===================== HELPERS =====================

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MatFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Deco");
        }

        private static Texture2D LoadTex(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TexFolder, fileName));
        }

        private static Material CreateMat(string name, Texture2D tex, Color tint, Vector2 tiling)
        {
            string path = Path.Combine(MatFolder, name + ".mat");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (tex != null)
                {
                    existing.SetTexture("_BaseMap", tex);
                    existing.mainTexture = tex;
                }
                existing.SetColor("_BaseColor", tint);
                existing.SetTextureScale("_BaseMap", tiling);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.SetTexture("_BaseMap", tex);
            }
            mat.SetColor("_BaseColor", tint);
            mat.SetTextureScale("_BaseMap", tiling);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ===================== PROPS =====================

        private static void PlaceCrateCluster(Transform parent, Vector3 center, Material mat, int count)
        {
            var cluster = new GameObject("Cajas");
            cluster.transform.SetParent(parent);
            cluster.transform.position = center;

            for (int i = 0; i < count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Caja";
                cube.transform.SetParent(cluster.transform);
                float size = Random.Range(0.55f, 1.0f);
                cube.transform.localScale = new Vector3(size, size, size);
                float ox = Random.Range(-1.3f, 1.3f);
                float oz = Random.Range(-1.3f, 1.3f);
                float oy = 0;
                if (i >= 2) oy = size;
                if (i >= 4) oy = size * 2;
                cube.transform.localPosition = new Vector3(ox, oy + size * 0.5f, oz);
                cube.transform.localRotation = Quaternion.Euler(0, Random.Range(-15f, 15f), 0);
                var mr = cube.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
        }

        private static void PlaceBarrelGroup(Transform parent, Vector3 center, Material mat, int count)
        {
            var group = new GameObject("Barriles");
            group.transform.SetParent(parent);
            group.transform.position = center;

            for (int i = 0; i < count; i++)
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "Barril";
                cyl.transform.SetParent(group.transform);
                cyl.transform.localPosition = new Vector3(Random.Range(-0.7f, 0.7f), 0.5f, Random.Range(-0.7f, 0.7f));
                cyl.transform.localScale = new Vector3(0.55f, 0.5f, 0.55f);
                var mr = cyl.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
        }

        private static void PlacePlantPot(Transform parent, Vector3 pos, Material potMat, Material plantMat)
        {
            var group = new GameObject("Planta");
            group.transform.SetParent(parent);
            group.transform.position = pos;

            var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pot.name = "Maceta";
            pot.transform.SetParent(group.transform);
            pot.transform.localPosition = new Vector3(0, 0.22f, 0);
            pot.transform.localScale = new Vector3(0.45f, 0.22f, 0.45f);
            var mrP = pot.GetComponent<MeshRenderer>();
            if (mrP != null) mrP.sharedMaterial = potMat;

            var plant = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            plant.name = "Follaje";
            plant.transform.SetParent(group.transform);
            plant.transform.localPosition = new Vector3(0, 0.65f, 0);
            plant.transform.localScale = new Vector3(0.65f, 0.85f, 0.65f);
            var mrL = plant.GetComponent<MeshRenderer>();
            if (mrL != null) mrL.sharedMaterial = plantMat;
            var col = plant.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void PlaceTrashBag(Transform parent, Vector3 pos, Material mat)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Basura";
            s.transform.SetParent(parent);
            s.transform.position = pos + Vector3.up * 0.28f;
            float sz = Random.Range(0.45f, 0.75f);
            s.transform.localScale = new Vector3(sz, sz * 0.65f, sz);
            var mr = s.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        private static void PlacePallet(Transform parent, Vector3 pos, Material mat)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "Pallet";
            p.transform.SetParent(parent);
            p.transform.position = pos + new Vector3(0, 0.06f, 0);
            p.transform.localScale = new Vector3(1.2f, 0.12f, 1.0f);
            var mr = p.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        private static void PlaceBarrier(Transform parent, Vector3 pos, Material mat)
        {
            var group = new GameObject("Barrera");
            group.transform.SetParent(parent);
            group.transform.position = pos;

            for (int side = -1; side <= 1; side += 2)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Pata";
                leg.transform.SetParent(group.transform);
                leg.transform.localPosition = new Vector3(side * 0.5f, 0.4f, 0);
                leg.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
                var mrL = leg.GetComponent<MeshRenderer>();
                if (mrL != null) mrL.sharedMaterial = mat;
            }
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Barra";
            bar.transform.SetParent(group.transform);
            bar.transform.localPosition = new Vector3(0, 0.7f, 0);
            bar.transform.localScale = new Vector3(1.1f, 0.12f, 0.06f);
            var mrB = bar.GetComponent<MeshRenderer>();
            if (mrB != null) mrB.sharedMaterial = mat;
        }

        private static void PlaceSign(Transform parent, Vector3 pos, Material signMat, Material poleMat)
        {
            var group = new GameObject("Cartel");
            group.transform.SetParent(parent);
            group.transform.position = pos;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Poste";
            pole.transform.SetParent(group.transform);
            pole.transform.localPosition = new Vector3(0, 0.9f, 0);
            pole.transform.localScale = new Vector3(0.06f, 0.9f, 0.06f);
            var mrP = pole.GetComponent<MeshRenderer>();
            if (mrP != null) mrP.sharedMaterial = poleMat;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Cartel";
            board.transform.SetParent(group.transform);
            board.transform.localPosition = new Vector3(0, 1.6f, 0);
            board.transform.localScale = new Vector3(0.7f, 0.5f, 0.04f);
            var mrB = board.GetComponent<MeshRenderer>();
            if (mrB != null) mrB.sharedMaterial = signMat;
        }

        private static void PlacePlank(Transform parent, Vector3 pos, Material mat)
        {
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Tablon";
            plank.transform.SetParent(parent);
            plank.transform.position = pos + new Vector3(0, 0.9f, 0);
            plank.transform.localScale = new Vector3(0.2f, 1.8f, 0.04f);
            plank.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(-12f, 12f));
            var mr = plank.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        private static void PlacePipeStack(Transform parent, Vector3 pos, Material mat)
        {
            var group = new GameObject("Tubos");
            group.transform.SetParent(parent);
            group.transform.position = pos;

            for (int i = 0; i < 4; i++)
            {
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.name = "Tubo";
                pipe.transform.SetParent(group.transform);
                float row = i / 2;
                float col = i % 2;
                pipe.transform.localPosition = new Vector3(col * 0.18f - 0.09f, 0.08f + row * 0.16f, Random.Range(-0.1f, 0.1f));
                pipe.transform.localScale = new Vector3(0.14f, 0.8f, 0.14f);
                pipe.transform.localRotation = Quaternion.Euler(90, Random.Range(-5f, 5f), 0);
                var mr = pipe.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
        }
    }
}
