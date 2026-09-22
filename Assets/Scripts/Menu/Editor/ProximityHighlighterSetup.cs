using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrisaControlada.Player
{
    public static class ProximityHighlighterSetup
    {
        [MenuItem("PrisaControlada/Configurar Resaltador de Objetos")]
        public static void SetupHighlighter()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[HighlighterSetup] No se encontro un GameObject llamado 'Player'.");
                return;
            }

            var highlighter = player.GetComponent<ProximityHighlighter>();
            if (highlighter == null)
                highlighter = player.AddComponent<ProximityHighlighter>();

            var so = new SerializedObject(highlighter);

            Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/VHSEffectURP/hud/MarcaItems.png");
            if (arrowSprite != null)
            {
                var spriteProp = so.FindProperty("arrowSprite");
                if (spriteProp != null)
                    spriteProp.objectReferenceValue = arrowSprite;
            }
            else
            {
                Debug.LogWarning("[HighlighterSetup] No se encontro MarcaItems.png como Sprite. Reconoce el sprite en el import settings.");
            }

            string shaderPath = "Assets/Shaders/WhiteOutline.shader";
            string matPath = "Assets/Shaders/WhiteOutline.mat";
            Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (outlineMat == null)
            {
                Shader outlineShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (outlineShader != null)
                {
                    outlineMat = new Material(outlineShader);
                    outlineMat.name = "WhiteOutline";
                    AssetDatabase.CreateAsset(outlineMat, matPath);
                }
            }

            if (outlineMat != null)
            {
                var matProp = so.FindProperty("outlineMaterial");
                if (matProp != null)
                    matProp.objectReferenceValue = outlineMat;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[HighlighterSetup] ProximityHighlighter configurado en el Player.");
        }
    }
}