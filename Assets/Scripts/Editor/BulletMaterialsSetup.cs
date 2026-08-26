using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    public static class BulletMaterialsSetup
    {
        private const string TexturesFolder = "Assets/MyAssets/guns/revolver/bullets/textures";
        private const string MaterialsFolder = "Assets/Materials/Items";
        private const string BulletFbx = "Assets/MyAssets/guns/revolver/bullets/source/Bullet.fbx";
        private const string AmmoBoxFbx = "Assets/MyAssets/guns/revolver/bullets/source/AmmoBox2.fbx";

        [MenuItem("Tools/PrisaControlada/Items/Setup Bullet Materials")]
        public static void SetupBulletMaterials()
        {
            EnsureFolder(MaterialsFolder);
            ConfigureNormalMap();

            Material ammoBoxMat = CreateMaterial("AmmoBox");
            Material bulletMat = CreateMaterial("Bullet");

            RemapModelMaterials(BulletFbx, bulletMat);
            RemapModelMaterials(AmmoBoxFbx, ammoBoxMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BulletMaterialsSetup] Materiales asignados a Bullet.fbx y AmmoBox2.fbx con las texturas.");
        }

        private static Material CreateMaterial(string name)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", LoadTexture("AmmoBox_BaseColor"));
            mat.SetTexture("_BumpMap", LoadTexture("AmmoBox_DX_Normal"));
            mat.SetTexture("_MetallicGlossMap", LoadTexture("AmmoBox_Metallic"));
            mat.SetTexture("_OcclusionMap", LoadTexture("AmmoBox_AO"));
            mat.SetTexture("_ParallaxMap", LoadTexture("AmmoBox_Height"));
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 0.6f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetFloat("_Parallax", 0.005f);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureNormalMap()
        {
            string path = $"{TexturesFolder}/AmmoBox_DX_Normal.jpeg";
            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
        }

        private static Texture2D LoadTexture(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesFolder}/{name}.jpeg");
        }

        private static void RemapModelMaterials(string modelPath, Material replacement)
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null)
            {
                Debug.LogWarning($"[BulletMaterialsSetup] No hay importer para {modelPath}");
                return;
            }

            int count = 0;
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (obj is Material material)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), replacement);
                    count++;
                }
            }

            importer.SaveAndReimport();
            Debug.Log($"[BulletMaterialsSetup] {count} material(es) remapeado(s) en {modelPath}");
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
