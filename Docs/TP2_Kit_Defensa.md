# Prisa Controlada — Kit de defensa TP2

> Objetivo: que cada integrante pueda abrir el código, explicar una decisión en aproximadamente 30 segundos y sostener el ejemplo sin depender de que otro compañero responda.

## 1. Orden recomendado de exposición

1. Mostrar la pestaña **01 - Obligatorios y extras** del UML.
2. Herencia: `EnemyBase` y sus cuatro derivadas.
3. Composición: `EnemyBase → DamageCollider`.
4. Clase abstracta: `EnemyBase`.
5. Interfaz: `IInteractable` y sus implementadores.
6. Encapsulamiento: `PlayerHealth.CurrentHealth` o `PickupItem.IsHeld`.
7. Extras: generic, eventos/delegados, struct, diccionario, enum y extensión.
8. Mostrar la pestaña **04 - Mapa procedural** para explicar la evolución desde TP1.
9. Ejecutar la demo al final.

---

## 2. Guiones de 30 segundos

### 2.1 Herencia — Agustín Labarque

> “La herencia principal está en `EnemyBase`. La clase base define navegación, percepción, daño, ataque y muerte. `ZombieEnemy`, `RunnerEnemy`, `BlindEnemy` y `NemesisEnemy` reutilizan ese contrato, pero sobrescriben comportamiento y estadísticas distintos. No son cuatro nombres: el ciego escucha, el runner persigue rápido y el Nemesis no muere.”

**Archivos:**

- `Assets/Scripts/Enemies/EnemyBase.cs`
- `Assets/Scripts/Enemies/ZombieEnemy.cs`
- `Assets/Scripts/Enemies/RunnerEnemy.cs`
- `Assets/Scripts/Enemies/BlindEnemy.cs`
- `Assets/Scripts/Enemies/NemesisEnemy.cs`

---

### 2.2 Composición — Agustín Labarque o Matías Morilla

> “La composición más clara es `EnemyBase → DamageCollider`: durante el ataque, el enemigo crea un objeto hijo, le agrega `DamageCollider`, lo configura con el objetivo y el daño, y lo destruye cuando termina. La parte existe porque el todo la crea y controla su ciclo de vida. También se puede mostrar `Ragdoll → RagdollComponent` y el generador procedural administra sus `SpawnedRoom`.”

**Archivos:**

- `Assets/Scripts/Enemies/EnemyBase.cs` — `SpawnDamageCollider`
- `Assets/Scripts/Enemies/DamageCollider.cs`
- `Assets/Scripts/Ragdoll/Ragdoll.cs`
- `Assets/Scripts/Map/ProceduralMapGenerator.cs`

---

### 2.3 Clase abstracta — Agustín Labarque

> “`EnemyBase` es abstracta porque representa un enemigo con operaciones comunes, pero cada subclase debe definir cómo se comporta. En particular, `ApplyDefaultStats` es abstracto: el compilador obliga a `ZombieEnemy`, `RunnerEnemy`, `BlindEnemy` y `NemesisEnemy` a definir sus estadísticas. La base también provee hooks virtuales de inicialización, daño, ataque y muerte, que las derivadas especializan. Así no se puede instanciar un enemigo genérico sin comportamiento propio.”

**Archivo:** `Assets/Scripts/Enemies/EnemyBase.cs`

---

### 2.4 Interfaz — Francisco Larrondo

> “`IInteractable` unifica puertas, cajones, notas, audio, pickups y munición, aunque esos tipos no compartan herencia. El inventario obtiene la interfaz, valida si el collider impactado es interactivo y llama `TryInteract`. Antes había una cadena de `if` por tipo; ahora el jugador sólo conoce el contrato. La interfaz tiene nueve implementadores directos, así que no es un caso forzado.”

**Archivos:**

- `Assets/Scripts/Items/IInteractable.cs`
- `Assets/Scripts/Player/InventoryController.cs`
- `Assets/Scripts/Player/InteractionCrosshair.cs`
- `Assets/Scripts/Player/ProximityHighlighter.cs`

---

### 2.5 Encapsulamiento — Ian Rosenberg o Francisco Larrondo

> “En `PlayerHealth`, `maxHealth` es privado y `CurrentHealth` se expone con `get; private set`. Quien consume la salud puede verla, pero sólo `PlayerHealth` puede modificarla, evitando que un HUD o un enemigo rompa la transición de vida. `PickupItem.IsHeld` y `LootSpawner.LastSpawned` siguen el mismo patrón.”

**Archivos:**

- `Assets/Scripts/Player/PlayerHealth.cs`
- `Assets/Scripts/Items/PickupItem.cs`
- `Assets/Scripts/Items/LootSpawner.cs`

---

### 2.6 Genéricos — Matías Morilla

> “`WeightedTable<T>` implementa un sorteo ponderado genérico. La misma clase se instancia como `WeightedTable<LootEntry>`, `WeightedTable<EnemyEntry>` y `WeightedTable<RoomDefinition>`. Internamente guarda `List<T>` y `List<float>`; `Roll` devuelve `T`, sin castear a `object` ni retipar el resultado. Así el algoritmo se usa para loot, enemigos y salas.”

**Archivos:**

- `Assets/Scripts/Core/WeightedTable.cs`
- `Assets/Scripts/Items/LootSpawner.cs`
- `Assets/Scripts/Enemies/EnemyLootSpawner.cs`
- `Assets/Scripts/Map/ProceduralMapGenerator.cs`

---

### 2.7 Eventos y delegados — Ian Rosenberg

> “Los eventos evitan que el productor conozca al consumidor. `PlayerController` publica `OnNoiseMade` y los enemigos se suscriben para recordar al jugador; `PlayerHealth` publica daño, curación y muerte; el inventario publica cambios; las puertas notifican cambios de navegación. En stamina además hay delegates nombrados, `StaminaChangedHandler` y `StaminaExhaustedHandler`, y el HUD se suscribe y se da de baja.”

**Archivos:**

- `Assets/Scripts/Player/PlayerController.cs`
- `Assets/Scripts/Player/PlayerHealth.cs`
- `Assets/Scripts/Player/InventoryController.cs`
- `Assets/Scripts/Items/DoorController.cs`
- `Assets/Scripts/Player/StaminaBarHUD.cs`

---

### 2.8 Structs — Agustín Labarque o Matías Morilla

> “`NoiseStamp` es un struct de valor con tiempo, nivel y posición de un ruido. Zombie y Blind lo usan como memoria de los sonidos percebidos. Al ser valor, cada sello se copia y no comparte estado mutable con el anterior. `CodeEntry` es el segundo ejemplo: identifica la pista visible y guarda el código real de una fila del puzzle.”

**Archivos:**

- `Assets/Scripts/Enemies/NoiseStamp.cs`
- `Assets/Scripts/Enemies/ZombieEnemy.cs`
- `Assets/Scripts/Enemies/BlindEnemy.cs`
- `Assets/Scripts/Map/PuzzleCodeController.cs`

---

### 2.9 Diccionarios — Ian Rosenberg

> “`ProximityHighlighter` guarda `Dictionary<GameObject, PickupArrow>`. La clave es el pickup real, no la cantidad de pickups ni un índice; el valor es la flecha que se le asigna. Eso permite recuperar y reciclar la flecha por identidad. `ZombieEnemy` también usa diccionarios `Transform → Vector3` para asociar offsets de impacto a huesos concretos.”

**Archivos:**

- `Assets/Scripts/Player/ProximityHighlighter.cs`
- `Assets/Scripts/Enemies/ZombieEnemy.cs`

---

### 2.10 Enums — Agustín Labarque o Matías Morilla

> “Usamos enums para representar dominio y estados: `EnemyType`, `RoomType`, `ZombieState`, `RunnerState`, `BlindState` y `PickupWeaponKind`. Por ejemplo, `RoomType.Combate` expresa una regla del generador; un entero `2` no explicaría por sí solo el significado.”

**Archivos:**

- `Assets/Scripts/Enemies/EnemyFactory.cs`
- `Assets/Scripts/Map/RoomType.cs`
- `Assets/Scripts/Enemies/ZombieEnemy.cs`
- `Assets/Scripts/Enemies/RunnerEnemy.cs`
- `Assets/Scripts/Enemies/BlindEnemy.cs`

---

### 2.11 Extensión de clases — Matías Morilla

> “`TransformExtensions.FindRecursive` es un método de extensión de `Transform`. El parámetro `this` permite escribir `transform.FindRecursive(...)` sin modificar Unity ni inventar una jerarquía. `Rig` lo usa realmente para encontrar huesos anidados dentro del esqueleto.”

**Archivos:**

- `Assets/Scripts/Extensions/TransformExtensions.cs`
- `Assets/Scripts/Ragdoll/Rig.cs`

---

## 3. Reparto sugerido para la exposición

| Parte | Integrante |
|---|---|
| Abrir el UML y explicar la sincronización TP1 → TP2 | Matías Morilla |
| Herencia, abstracta y composición de enemigos | Agustín Labarque |
| Interfaz, items, pickups, puertas e inventario | Francisco Larrondo |
| Eventos/delegados, encapsulamiento, stamina y HUD | Ian Rosenberg |
| Genérico, mapa procedural, structs y extensión | Matías Morilla |
| Composición del ragdoll y efectos visuales | Francisco Larrondo / Ian Rosenberg |

El reparto es funcional: cada persona debe abrir y explicar su propio código.

---

## 4. Preguntas probables y respuestas

### “¿Por qué `WeightedTable<T>` no hace `object`?”

> “Porque el tipo `T` es parte del contrato. La lista interna guarda `T` y `Roll` devuelve `T`; el compilador verifica el uso en cada instancia. Un cast a `object` obligaría a recuperar el tipo después y perdería seguridad de compilación.”

### “¿Por qué no usaron la cantidad como clave del diccionario?”

> “La cantidad puede repetirse y no identifica un pickup. Usamos `GameObject` como clave porque representa la identidad concreta del objeto. Eso permite asociarle una flecha sin ambigüedad.”

### “¿Por qué `EnemyBase` es abstracta si tiene muchos métodos implementados?”

> “Abstracta no significa incompleta. La base implementa el comportamiento común y define `ApplyDefaultStats` como abstracto para que cada tipo de enemigo realmente deba especializar sus estadísticas. Lo importante es que sus cuatro derivadas cambian el comportamiento real; no hay una subclase artificial.”

### “¿Por qué usan una interfaz si hay una clase base de pickups?”

> “Puertas, cajones, notas y munición no son pickups y no tienen sentido bajo `PickupItem`. La interfaz agrupa sólo la capacidad de interactuar. `PickupItem` también puede implementarla sin perder su herencia propia.”

### “¿Por qué un enum de estados y no una máquina de estados?”

> “Los enums sólo nombran estados. En `ZombieEnemy`, `RunnerEnemy` y `BlindEnemy` existe además una máquina de estados real con transiciones y acciones; no estamos llamando “máquina de estados” a una cadena de `if`.”

### “¿Qué cambió desde TP1?”

> “El proyecto evolucionó de una propuesta de secuencia de blits a un generador procedural con streaming de salas. La versión vigente usa `ProceduralMapGenerator`, `SpawnedRoom`, `RoomDefinition`, `RoomTrigger` y `RuntimeNavMesh`. El UML TP2 muestra esa arquitectura real, no clases futuras.”

### “¿Por qué `NoiseStamp` es struct y no class?”

> “Es un registro pequeño de valor con tres campos. No tiene identidad propia ni comportamiento; copiarlo es barato y evita estado mutable compartido. Se serializa en listas para recordar ruido durante un intervalo.”

### “¿Qué pasa si no hay más stamina?”

> “El controller detecta la transición a cero, actualiza su estado y emite `OnStaminaExhausted(true)`. El HUD recibe el evento y cambia el color. Luego se regenera; al llegar al máximo emite `OnStaminaExhausted(false)` y vuelve a permitir correr.”

---

## 5. Qué no afirmar

- No afirmar que los power-ups de TP1 están implementados: actualmente no existen esas clases.
- No afirmar que todas las APIs obsoletas de Unity fueron migradas; el proyecto compila, pero conserva advertencias de APIs deprecated.
- No llamar “composición” a una simple referencia sin control del ciclo de vida.
- No decir que `NoiseStamp` es una clase; es un struct.
- No usar la cantidad de pickups como clave de diccionario.
- No presentar `Blit`, `BlitScheduler`, `LevelManager`, `RoomInstance` ni `ProceduralGenerator` como clases vigentes.
- No prometer que `WeightedTable<T>` se utiliza sólo para loot: ahora se usa también con enemigos y salas.

---

## 6. Checklist antes de presentar

### Código

- [ ] Abrir `Assets/Scripts/Enemies/EnemyBase.cs` y mostrar `SpawnDamageCollider`.
- [ ] Abrir `Assets/Scripts/Items/IInteractable.cs` y mostrar sus implementadores con `Shift+F12`.
- [ ] Abrir `Assets/Scripts/Core/WeightedTable.cs` y sus tres consumidores.
- [ ] Abrir `Assets/Scripts/Player/PlayerController.cs` y mostrar los eventos.
- [ ] Abrir `Assets/Scripts/Player/ProximityHighlighter.cs` y mostrar el diccionario.
- [ ] Abrir `Assets/Scripts/Extensions/TransformExtensions.cs` y `Rig.cs`.

### UML

- [ ] `PrisaControlada_UML_TP2.drawio` abre sin errores.
- [ ] La pestaña 01 se entiende en menos de un minuto.
- [ ] Los rombos llenos y las realizaciones se distinguen con claridad.

### Demo

- [ ] `Assets/Scenes/MainMenu.unity` inicia.
- [ ] `Assets/Scenes/Prototype.unity` carga sin excepciones visibles.
- [ ] Se puede agarrar y guardar un pickup.
- [ ] Se puede abrir una puerta y recoger munición.
- [ ] Se puede entrar a una sala nueva del mapa procedural.
- [ ] El HUD de stamina cambia al correr y se agota.

### Autoría

- [ ] Cada cambio importante tiene `//TP2 - Nombre Apellido`.
- [ ] Cada integrante ensaya su parte sin buscar la respuesta en tiempo real.
