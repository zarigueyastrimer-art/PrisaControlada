# Visión Escotópica (Scotopic Vision) — Efecto de visión nocturna para URP

Efecto de visión nocturna realista para **Unity URP**. Simula la visión de
bastones del ojo humano: con la linterna apagada ves el entorno apenas
distinguible (Eigengrau + ruido neural + monocromo + blur), y al prender la
linterna se apaga el efecto y la luz revela todo con color. Al apagarla de
nuevo, el ojo "parte de ciegas" y se va adaptando a la oscuridad.

Inspirado en el enfoque de render target + shader propio (equivalente al
material "NightWrite" del video de referencia).

---

## Requisitos

- **Unity** (probado en 6000.0.x, funciona en 2022 LTS+).
- **URP** (Universal Render Pipeline). El efecto es un *Scriptable Renderer Feature*.
- Paquete **Input System** (`com.unity.input.system`) — lo usa la linterna (tecla F).

> Nota: funciona con *Compatibility Mode* de URP (Render Graph deshabilitado).

## Archivos incluidos

```
Scripts/
  ScotopicVisionFeature.cs    Render Feature + pass que aplica el shader.
  ScotopicVisionController.cs Traduce la adaptación a parámetros del shader (todos públicos).
  DarkAdaptation.cs           Mide la luz alrededor y calcula la adaptación 0-1.
  FlashlightController.cs     Linterna (tecla F), con fundido suave.
Shaders/
  ScotopicVision.shader       El shader del efecto (Hidden/PCHZ/ScotopicVision).
```

Cada archivo trae su `.meta` (GUIDs estables). Cópialos dentro de tu carpeta
`Assets/` (puedes mantener la estructura `Assets/Scripts/` y `Assets/Shaders/`).

---

## Instalación paso a paso

### 1. Copiar los archivos
Copia las carpetas `Scripts/` y `Shaders/` dentro de tu `Assets/`.
Deja que Unity importe (generará/atrapará los `.meta`).

### 2. Agregar el Render Feature a tu URP Renderer
1. Ve a **Edit > Project Settings > Graphics** y anota cuál es tu *Scriptable Render Pipeline Settings* (asset de URP).
2. Abre ese **URP Asset** y, en *Rendering > Renderer List*, abre tu **Renderer** (ej. `PC_Renderer`).
3. En la lista de *Renderer Features* haz clic en **Add Renderer Feature > Scotopic Vision Feature**.
4. Asigna el **Shader** `Hidden/PCHZ/ScotopicVision` al campo `Shader` del feature.
   (Si lo dejas vacío, el feature lo busca por nombre solo, pero es mejor asignarlo.)

### 3. Agregar los componentes al jugador
En tu GameObject del jugador (o donde prefieras):

- **`DarkAdaptation`** — mide la luz y calcula la adaptación. Asigna la
  *Target Camera* (la cámara del jugador) y el *Volume* global si quieres.
- **`ScotopicVisionController`** — requiere `DarkAdaptation` (se auto-agrega).
  Busca la linterna automáticamente (está en una cámara hija) o asígnala a mano.
- **`FlashlightController`** — en la **cámara** del jugador (hijo), junto a una
  luz **Spot**. Requiere un `Light` en el mismo objeto.

### 4. Iluminación base de la sala (importante)
Para que con la linterna **apagada** el entorno sea apenas distinguible (y no
negro puro), usa una luz ambiental muy tenue y una Directional suave:

- **Ambient**: modo *Flat*, color ~`(0.032, 0.032, 0.04)`.
- **Directional**: intensidad ~`0.18`, **sin sombras** (en interiores cerrados
  las sombras direccionales no aportan y encarecen).

`ScotopicVisionController` puede encargarse de esto solo si activas
**Dark Ambient With Flashlight** (ver controles).

---

## Controles (todos públicos, ajustables en vivo desde el Inspector)

Selecciona el jugador con `ScotopicVisionController`:

### Controles maestros
- **Filter Enabled** — apaga/enciende todo el filtro (off = escena cruda).
- **Filter Strength** — mezcla global 0-1 del efecto.
- **Filter Gamma** — curva tonal del look escotópico (>1 = más contraste).
- **Min Darkness Floor** — piso de gris (evita negro puro ya adaptado).

### Linterna
- **Turn Off With Flashlight** — si está ON, el efecto se apaga con la linterna
  (visión de conos). Recomendado ON.
- **Darken Ambient With Flashlight** — si está ON, al prender la linterna se
  apagan las luces generales para que **solo ilumine la linterna** (fuera del
  cono = negro absoluto). Recomendado ON.
- **Dark Ambient Color / Dark Directional Intensity** — valores base de las
  luces generales cuando la linterna está apagada.
- **Flashlight Fade Time** — segundos del fundido de esas luces.

### Eigengrau / ganancia / ruido
- **Eigengrau Color / Amount** — el gris intrínseco de la retina.
- **Scotopic Gain** — cuánto amplifica las formas tenues (revela objetos).
- **Mono Amount** — cuánto desatura (bastones = monocromo).
- **Local Lit Sensitivity** — qué tan pronto un pixel iluminado queda nítido.

### Ruido neural
- **Noise Mode** — dropdown para elegir el estilo en vivo:
  - `DirectionalScroll` — scroll diagonal (sensación de viento).
  - `StaticGrain` — estática de TV / grano (parpadeo random, recomendado).
  - `CellParticles` — partículas discretas que destellan.
- **Noise Flicker Rate** — cuántas veces por segundo se re-randomiza (modos 1-2).
- **Noise Intensity / Scales** — amplitud y escala del grano.

### Visión periférica / blur
- **Peripheral Power / Center Weight** — cuánto crece el efecto hacia los bordes.
- **Blur Radius** — blur de bastones (baja agudeza espacial).

---

## Cómo funciona (resumen)

1. `DarkAdaptation` mide la luz alrededor (con raycasts de oclusión para
   interiores) y produce una adaptación 0-1 (`Adaptation01`). Ir a oscuridad es
   lento (~7s); recibir luz es rápido (~0.9s).
2. `ScotopicVisionController` empuja esa adaptación y los parámetros públicos al
   material del shader cada frame.
3. `ScotopicVisionFeature` inyecta el shader como un blit a pantalla completa
   *después* del post-procesado.
4. El shader, por píxel: si es zona oscura responde escotópico (Eigengrau +
   ganancia + ruido, todo escalado con la adaptación → negro al inicio, emerge al
   adaptarse); si es zona iluminada por la linterna, la deja cruda y nítida.

---

## Troubleshooting

- **Pantalla negra / sin efecto**: verifica que el Render Feature esté agregado
  y **activo** en el Renderer, y que el shader esté asignado. Recompila.
- **Se ve todo gris plano con la linterna apagada**: baja la iluminación base
  (ambiental/Directional) — la escena debe ser oscura para que la ganancia
  amplifique contrastes en vez de aplanar todo.
- **Color visible donde no debería**: sube *Mono Amount* y revisa que la escena
  base no esté demasiado iluminada.
