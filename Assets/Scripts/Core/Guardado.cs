using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrisaControlada.Core
{
    //TP2 - Matías Morilla
    // Guardado de la partida en un archivo JSON dentro de
    // Application.persistentDataPath (por plataforma:
    // Windows: %USERPROFILE%\AppData\LocalLow\<empresa>\<juego>\).
    //
    // Guarda:
    //  - los ids de las notas coleccionadas (el menú los lee);
    //  - la dificultad elegida en la página "jugar" del menú;
    //  - los metros de la última muerte y el récord de cada dificultad.
    //
    // Cada operación lee-modifica-escribe el archivo completo, así que un
    // guardado nunca pisa los datos del otro.
    public static class Guardado
    {
        // Ids de dificultad, en el mismo orden que los botones BtnDif0/1/2.
        public const string DificultadFacil = "facil";
        public const string DificultadEstandar = "estandar";
        public const string DificultadDificil = "dificil";

        [Serializable]
        private class Datos
        {
            public List<string> notas = new List<string>();
            public float metros;
            public string dificultad = DificultadEstandar;
            public float mejorFacil;
            public float mejorEstandar;
            public float mejorDificil;
        }

        private const string Archivo = "partida.json";

        private static string Ruta => Path.Combine(Application.persistentDataPath, Archivo);

        // Lee el archivo. Si no existe o está roto, devuelve datos vacíos
        // (nunca tira excepción: un guardado malo no debería romper el juego).
        private static Datos Cargar()
        {
            try
            {
                if (File.Exists(Ruta))
                {
                    Datos datos = JsonUtility.FromJson<Datos>(File.ReadAllText(Ruta));
                    if (datos != null)
                    {
                        if (datos.notas == null)
                            datos.notas = new List<string>();
                        if (string.IsNullOrEmpty(datos.dificultad))
                            datos.dificultad = DificultadEstandar;
                        return datos;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Guardado] No se pudo leer '{Ruta}': {e.Message}");
            }

            return new Datos();
        }

        private static void Escribir(Datos datos)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Ruta, JsonUtility.ToJson(datos, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Guardado] No se pudo escribir '{Ruta}': {e.Message}");
            }
        }

        // ---------------- Notas ----------------

        // Ids de las notas ya coleccionadas (set para buscar rápido).
        public static HashSet<string> CargarNotas()
        {
            return new HashSet<string>(Cargar().notas);
        }

        // Suma una nota a la colección. Idempotente: re-leer la misma nota
        // no la duplica.
        public static void AgregarNota(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            Datos datos = Cargar();
            if (datos.notas.Contains(id)) return;

            datos.notas.Add(id);
            Escribir(datos);
        }

        // Borra todas las notas coleccionadas (tecla P) para volver a buscarlas.
        public static void ResetearNotas()
        {
            Datos datos = Cargar();
            if (datos.notas.Count == 0) return;

            datos.notas.Clear();
            Escribir(datos);
        }

        // ---------------- Dificultad y récords ----------------

        // Dificultad elegida en el menú "jugar" (queda guardada entre sesiones).
        public static void SetDificultad(string dificultad)
        {
            if (string.IsNullOrEmpty(dificultad)) return;

            Datos datos = Cargar();
            if (datos.dificultad == dificultad) return;

            datos.dificultad = dificultad;
            Escribir(datos);
        }

        public static string CargarDificultad()
        {
            return Cargar().dificultad;
        }

        // Llamado al morir: guarda los metros de esta corrida y, si es la
        // mejor hasta ahora PARA LA DIFICULTAD ACTUAL, actualiza el récord.
        public static void RegistrarMuerte(float metros)
        {
            Datos datos = Cargar();
            datos.metros = metros;

            if (metros > MejorDe(datos, datos.dificultad))
                AsignarMejor(datos, datos.dificultad, metros);

            Escribir(datos);
        }

        // Récord de metros de una dificultad (0 si todavía no se jugó).
        public static float CargarMejor(string dificultad)
        {
            return MejorDe(Cargar(), dificultad);
        }

        private static float MejorDe(Datos datos, string dificultad)
        {
            if (dificultad == DificultadFacil) return datos.mejorFacil;
            if (dificultad == DificultadDificil) return datos.mejorDificil;
            return datos.mejorEstandar;
        }

        private static void AsignarMejor(Datos datos, string dificultad, float valor)
        {
            if (dificultad == DificultadFacil) datos.mejorFacil = valor;
            else if (dificultad == DificultadDificil) datos.mejorDificil = valor;
            else datos.mejorEstandar = valor;
        }
    }
}
