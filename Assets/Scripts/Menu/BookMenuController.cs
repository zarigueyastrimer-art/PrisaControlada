using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    /// <summary>
    /// Menú de libro: cada marcador (pestaña) es una página distinta.
    ///
    /// Al seleccionar un marcador se reordenan los hermanos para que el marcador activo
    /// quede por encima de la imagen del libro y el resto por debajo, dando la sensación
    /// de que estás "en el marcador correcto". La página asociada se pinta sobre el libro.
    /// </summary>
    [DisallowMultipleComponent]
    public class BookMenuController : MonoBehaviour
    {
        public enum TabAction
        {
            /// <summary>Sólo cambia de página.</summary>
            None,
            /// <summary>Cierra el juego (marcador rojo de salir).</summary>
            Quit
        }

        [Serializable]
        public class Tab
        {
            [Tooltip("Id de la pestaña (jugar, opciones, creditos, salir).")]
            public string id;

            [Tooltip("Marcador / pestaña del libro.")]
            public BookMarker marker;

            [Tooltip("Página que se pinta sobre el libro. Vacío si el marcador sólo ejecuta una acción.")]
            public GameObject page;

            [Tooltip("Acción extra al hacer click (Quit = cierra el juego).")]
            public TabAction action = TabAction.None;
        }

        [Header("Estructura")]
        [Tooltip("Padre que contiene marcadores + libro + páginas (se reordenan como hermanos).")]
        [SerializeField] private RectTransform bookRoot;

        [Tooltip("Imagen de fondo del libro: siempre por debajo del marcador activo.")]
        [SerializeField] private Graphic bookGraphic;

        [Tooltip("Contenedor de las páginas: siempre por encima del libro.")]
        [SerializeField] private RectTransform pagesRoot;

        [Header("Pestañas")]
        [SerializeField] private Tab[] tabs = new Tab[0];

        [Tooltip("Pestaña visible al abrir el menú (-1 = ninguna).")]
        [SerializeField] private int startIndex;

        /// <summary>Índice de la página que se está viendo, -1 si no hay ninguna.</summary>
        public int CurrentIndex { get; private set; } = -1;

        /// <summary>Id de la página que se está viendo.</summary>
        public string CurrentId =>
            CurrentIndex >= 0 && CurrentIndex < tabs.Length && tabs[CurrentIndex] != null
                ? tabs[CurrentIndex].id
                : string.Empty;

        /// <summary>Se dispara al cambiar de página (parámetro: índice de la pestaña).</summary>
        public event Action<int> PageChanged;

        private void Start()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null || tabs[i].marker == null) continue;

                int index = i;
                tabs[i].marker.Clicked += () => OnMarkerClicked(index);
            }

            if (startIndex >= 0 && startIndex < tabs.Length)
                Select(startIndex);
        }

        /// <summary>Cambia a la página con ese id.</summary>
        public void Select(string id)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null && tabs[i].id == id)
                {
                    Select(i);
                    return;
                }
            }

            Debug.LogWarning($"[Menu Libro] No existe la pestaña '{id}'.");
        }

        /// <summary>Cambia a la página de ese índice.</summary>
        public void Select(int index)
        {
            if (tabs == null || index < 0 || index >= tabs.Length || tabs[index] == null ||
                tabs[index].marker == null)
            {
                Debug.LogWarning($"[Menu Libro] Pestaña inválida: {index}");
                return;
            }

            // 1) Todos los marcadores al principio del padre, en orden fijo.
            int count = 0;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null && tabs[i].marker != null)
                    tabs[i].marker.transform.SetSiblingIndex(count++);
            }

            // 2) Libro y páginas justo después (por debajo del marcador activo).
            if (bookGraphic != null) bookGraphic.transform.SetSiblingIndex(count);
            if (pagesRoot != null) pagesRoot.SetSiblingIndex(count + 1);

            // 3) Marcador activo por encima de todo.
            tabs[index].marker.transform.SetAsLastSibling();

            // 4) Sólo se ve la página del marcador activo y el resto queda "guardado".
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                if (tab == null) continue;

                if (tab.page != null) tab.page.SetActive(i == index);
                if (tab.marker != null) tab.marker.SetSelected(i == index);
            }

            CurrentIndex = index;
            PageChanged?.Invoke(index);
        }

        private void OnMarkerClicked(int index)
        {
            if (index < 0 || index >= tabs.Length || tabs[index] == null) return;

            Select(index);
            // El cierre del juego ahora lo hace el botón dentro de la página de salir,
            // no al tocar directamente la pestaña.
        }
    }
}
