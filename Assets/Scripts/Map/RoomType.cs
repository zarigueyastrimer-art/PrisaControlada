namespace PrisaControlada.Map
{
    //TP2 - Matías Morilla
    // Tipos de sala del mapa procedural. El generator usa el tipo para aplicar
    // las reglas de aparicion (nunca combate tras combate, historia poco
    // comun, etc.). La herramienta Tools > PrisaControlada > Mapa >
    // Preparar Anclajes lo infiere del nombre del prefab.
    //
    // Enum en vez de numeros sueltos: en el inspector se elige de una lista y
    // en el codigo RoomType.Combate no se confunde con un 2 cualquiera.
    public enum RoomType
    {
        // Sala inicial colocada a mano. Solo tiene puerta de salida.
        Hub = 0,

        Pasillo = 1,
        Combate = 2,
        Loot = 3,
        Puzzle = 4,

        // Peso aleatorio bajo: aparece poco y nunca dos veces seguidas.
        Historia = 5,
    }
}
