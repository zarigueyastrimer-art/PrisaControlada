using PrisaControlada.Map;
using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Inspector del generador de mapa: ademas de todos los campos normales,
    // muestra las probabilidades por tipo como 5 SLIDERS de 0 a 100% que
    // siempre suman 100: si bajas uno, los demas suben en la misma proporcion.
    // Solo cambian los valores; la logica de sorteo es de ProceduralMapGenerator.
    [CustomEditor(typeof(ProceduralMapGenerator))]
    public class ProceduralMapGeneratorEditor : UnityEditor.Editor
    {
        private const string WeightsField = "typeWeights";

        private SerializedProperty _typeWeights;

        private void OnEnable()
        {
            _typeWeights = serializedObject.FindProperty(WeightsField);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Todo el resto del inspector (pool, puertas, ventana, navmesh...),
            // con sus tooltips de siempre. typeWeights se dibuja abajo a mano.
            DrawPropertiesExcluding(serializedObject, "m_Script", WeightsField);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Probabilidad por tipo de sala", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Los 5 sliders suman 100%. Si bajás uno, el resto sube en proporción.\n" +
                "Es la probabilidad BASE del tipo: el cooldown de combate y el azar de " +
                "historia (historyWeightRange) la bajan puntualmente en algunos sorteos.",
                MessageType.Info);

            DrawTypeWeights();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTypeWeights()
        {
            if (_typeWeights == null || !_typeWeights.isArray || _typeWeights.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Sin filas en typeWeights.", MessageType.Warning);
                return;
            }

            int n = _typeWeights.arraySize;
            string[] nombres = new string[n];
            float[] pesos = new float[n];
            float suma = 0f;

            for (int i = 0; i < n; i++)
            {
                SerializedProperty entry = _typeWeights.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = entry.FindPropertyRelative("type");
                SerializedProperty wProp = entry.FindPropertyRelative("weight");

                nombres[i] = typeProp.enumDisplayNames[Mathf.Clamp(typeProp.enumValueIndex, 0,
                    typeProp.enumDisplayNames.Length - 1)];

                pesos[i] = Mathf.Max(0f, wProp.floatValue);
                suma += pesos[i];
            }

            bool cambio = false;
            for (int i = 0; i < n; i++)
            {
                // El slider muestra el PORCENTAJE (peso / suma * 100), que es
                // lo que el usuario ve y arrastra; la suma puede valer lo que sea.
                float pct = suma > 0f ? pesos[i] / suma * 100f : 0f;

                EditorGUI.BeginChangeCheck();
                float nueva = EditorGUILayout.Slider(nombres[i], pct, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    Reajustar(i, nueva, pesos, suma);
                    cambio = true;
                }
            }

            if (cambio)
                serializedObject.ApplyModifiedProperties();
        }

        // Pone al tipo 'cambiado' en 'nuevaPct' del total y reescala al resto en
        // la misma proporcion, de modo que la suma se conserve. Los pesos quedan
        // ademas normalizados a que sumen 100 (asi el numero guardado ES el %).
        private void Reajustar(int cambiado, float nuevaPct, float[] pesos, float suma)
        {
            int n = pesos.Length;
            float nuevoFrac = Mathf.Clamp01(nuevaPct / 100f);

            // Caso raro: todo en 0. El valor del slider pasa a ser el peso directo.
            if (suma <= 0f)
            {
                for (int i = 0; i < n; i++)
                {
                    SetWeight(i, i == cambiado ? Mathf.Max(0f, nuevaPct) : 0f);
                }
                return;
            }

            // Participacion del tipo cambiado en la suma vieja (0..1). Esto es
            // peso/suma, NO peso/100: los pesos no tienen por qué sumar 100.
            float viejoFrac = pesos[cambiado] / suma;
            float restoViejo = 1f - viejoFrac;
            float restoNuevo = 1f - nuevoFrac;

            float[] nuevos = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i == cambiado)
                {
                    nuevos[i] = nuevoFrac;
                }
                else if (restoViejo > 0.0001f)
                {
                    // Su participacion dentro del resto viejo, aplicada al resto
                    // nuevo: entre los demas se conservan las proporciones.
                    nuevos[i] = (pesos[i] / suma) / restoViejo * restoNuevo;
                }
                else
                {
                    // El resto estaba en 0: se reparte lo que sobra en partes iguales.
                    nuevos[i] = restoNuevo / Mathf.Max(1, n - 1);
                }
            }

            // Normalizo a suma 100 para que el valor guardado sea el porcentaje.
            for (int i = 0; i < n; i++)
            {
                SetWeight(i, nuevos[i] * 100f);
            }
        }

        private void SetWeight(int i, float valor)
        {
            _typeWeights.GetArrayElementAtIndex(i)
                .FindPropertyRelative("weight").floatValue = Mathf.Max(0f, valor);
        }
    }
}
