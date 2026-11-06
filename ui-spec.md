# Especificación de UI — "Menú Personal" (R.E.P.O.)

Autor: Ana UI Designer · Fecha: 2026-11-06 · Versión: 1.0
Destino: integración en el plugin BepInEx (Gabriel BackEnd). Implementación con Unity `OnGUI`/IMGUI, **sin dependencias de assets** (ni sprites, ni fuentes externas, ni Canvas). Todo se dibuja con `GUI.Box`, `GUI.Toggle`, `GUI.Button`, `GUI.Label` y `GUIStyle` en tiempo de ejecución.

---

## 1. Comportamiento general

| Aspecto | Definición |
|---|---|
| Apertura/cierre | Atajo configurable, por defecto **F5**. Se lee de la config BepInEx (`hotkey`, tipo `KeyCode`). F5 funciona también mientras el menú está abierto (toggle). |
| Modalidad | **No modal**: el menú es una ventana flotante; el juego sigue corriendo detrás. No captura el cursor permanentemente (solo mientras se arrastra, ver §5). |
| Posición inicial | Arriba-izquierda, margen `(20, 20)` desde el borde. Recordar última posición en config (`menuX`, `menuY`) al cerrar. |
| Visibilidad en partida | Siempre disponible en menú principal y en partida (es solo cliente/local). |
| Idioma | Español (ES), codificado en las cadenas, sin Localization framework. |
| Render | `GUI.matrix` con escala de densidad: `scale = Screen.height / 1080f` (mín. 0.75, máx. 1.5) para legibilidad en 720p–4K. Todos los valores de abajo son a 1080p (escala 1.0). |

## 2. Layout del panel

```
┌─────────────────────────────────────────────┐
│  MENÚ PERSONAL                        [—]    │  ← barra título 34px
├─────────────────────────────────────────────┤
│                                             │
│  ▣  No recibir daño                 ON      │  ← fila toggle 40px
│  ─────────────────────────────────────────  │
│  ▣  Resistencia infinita            ON      │
│  ─────────────────────────────────────────  │
│  ▣  Sonido de avisos                OFF     │
│  ─────────────────────────────────────────  │
│                                             │
│  [ ▲ Añadir 100 ]   [ ▲ Añadir 1000 ]       │  ← fila botones dinero 44px
│  Dinero: bloqueado — solo disponible        │
│  cuando eres el host de la sesión           │  ← línea de estado 2×18px
│                                             │
│  F5 cerrar · host: sí/no                   │  ← pie de página 24px
└─────────────────────────────────────────────┘
```

### Medidas (px a 1080p, todo multiplica por `scale`)

| Elemento | Valor |
|---|---|
| Ancho del panel | 420 |
| Alto total | ≈ 330 (se recalcula: 34 + 3×(40+8) + 12 + 44 + 40 + 28 + padding) |
| Padding interior | 12 |
| Barra de título | alto 34, texto 16px negrita |
| Fila de toggle | alto 40, ancho 396; checkbox 20×20 a la izquierda; etiqueta 15px; interruptor de estado (ON/OFF) alineado a la derecha |
| Separador entre filas | 1px, `#2E2E2E`, margen horizontal 12 |
| Botón dinero | cada uno 180×36, separación 12, dos en fila |
| Texto de estado dinero | 13px, hasta 2 líneas, bajo los botones |
| Pie de página | 12px, texto secundario `#9E9E9E`: `F5 cerrar` + estado de host |

## 3. Colores y tipografía (estilo sobrio, tema oscuro)

Fuente: `GUI.skin` por defecto de Unity (no se carga fuente externa). Tamños vía `fontSize` en `GUIStyle`.

| Rol | Color HEX | Uso |
|---|---|---|
| Fondo panel | `#1B1B1F` (alpha 0.92) | `GUI.Box` principal |
| Barra título | `#26262C` | fondo cabecera |
| Borde | `#3A3A42` (1px) | contorno panel y controles |
| Texto primario | `#F2F2F2` | etiquetas de toggles |
| Texto secundario | `#9E9E9E` | pie, hints |
| Acento ON | `#4CAF7D` | toggle activado, texto "ON" |
| Acento OFF | `#6E6E76` | toggle desactivado, texto "OFF" |
| Botón normal | `#2F3438`, hover `#3A4046`, presionado `#25292D` | botones de dinero |
| Botón deshabilitado | fondo `#26282B`, texto `#5A5D61` | dinero sin host |
| Aviso bloqueado | `#E0A458` (ámbar) | mensaje "solo host" |
| Error/peligro | `#D36B6B` | reserved: log en pantalla si un parche falló |

Contraste: primario sobre fondo ≥ 12:1; secundario ≥ 5:1; ámbar sobre fondo ≥ 5.5:1. Cumple legibilidad exigente en 1080p y en escena oscura del juego (fondo con alpha 0.92 garantiza separación del juego).

## 4. Estados de cada control

### 4.1 Toggle "No recibir daño"
- **OFF (inicial)**: checkbox vacío, etiqueta "OFF" gris. Sin parche activo.
- **ON**: check relleno verde, "ON". Patch de daño activo para el jugador local.
- Hover: borde de la fila a `#4A4A54`. Click con ratón o teclado (Tab/Espacio, ver §6).
- Al alternar: se persiste en config BepInEx inmediatamente.

### 4.2 Toggle "Resistencia infinita"
- Igual que 4.1; controla el multiplicador de consumo de stamina (0 = infinito).

### 4.3 Toggle "Sonido de avisos" (third toggle)
- ON: reproduce un beep corto (`System.Console.Beep` o `AudioSource` procedural de 200 ms) al activar/desactivar cualquier función, para feedback sin mirar.
- OFF: silencio. Este toggle nunca afecta al juego.

### 4.4 Botones de dinero "Añadir 100" / "Añadir 1000"
Cuatro estados visibles:

| Estado | Cuándo | Apariencia | Texto de estado |
|---|---|---|---|
| **Disponible** | jugador local es host | botones normales, texto primario | (ninguno) o `Partida propia: dinero disponible` en verde |
| **Bloqueado (no host)** | es cliente | botones deshabilitados (gris), cursor sin hover | `⚠ Dinero bloqueado: solo disponible cuando eres el host de la sesión` en ámbar |
| **Busy** | tras pulsar, hasta que el plugin aplica la operación | spinner textual `…` en el botón, ambos deshabilitados brevemente | `Aplicando…` |
| **Resultado** | 1.5 s tras aplicar | toast interno bajo los botones: `+100 añadido` (verde) o `Error: no se pudo aplicar` (rojo) | — |

Reglas:
- La comprobación de host se hace **en cada draw o al abrir el menú** (cachear `isHost`, refrescar al cambiar de sesión); el botón no debe parecer disponible y luego fallar al click.
- Si el jugador pasa de host a cliente (se une a otra sala), la UI debe pasar a "Bloqueado" sin reapertura.
- El texto de estado ámbar es accesible: se redacta completo (no solo icono).

## 5. Interacción

- **Click**: toggles alternan al instante; botones disparan la acción.
- **Arrastrar**: la barra de título es zona de drag (`GUI` hotzone de 34px). Sin botones de redimensionado.
- **Ocultar panel con X**: botón `[—]` en la cabecera = mismo efecto que F5 (cierra).
- **El ratón solo reacciona sobre el panel**; fuera, los clicks pasan al juego. No secuestrar input del juego mientras el menú está visible pero sin hover.
- No usar `Time.timeScale = 0` (el juego es en red; congelar local desincroniza).

## 6. Accesibilidad y teclado

- **Foco teclado**: orden Tab = Toggle1 → Toggle2 → Toggle3 → Botón100 → Botón1000 → cerrar. Implementar con `GUI.SetNextControlName` + manejo de eventos de teclado si IMGUI nativo no basta; fallback aceptable: atajos directos (documentados en pie: `F5 cerrar`).
- Cada toggle announces estado con texto literal "ON"/"OFF" (no depende solo del color del check — daltónicos).
- Área táctil mínima equivalente: filas de 40px ≥ 44px a escala 1.0 en 1440p; nunca bajar `scale` de 0.75.
- Todos los textos en español con acentos correctos (fuente Unity los soporta).

## 7. Responsividad

| Resolución | Comportamiento |
|---|---|
| 1080p | diseño base (escala 1.0) |
| 720p | escala 0.75 → panel 315×~248, legible |
| 1440p/4K | escala 1.25/1.5 máx. — el panel no ocupa más del 20 % del ancho |
| Ultrawide | posición anclada a izquierda, nunca centrado sobre el viewport de juego |

Si `Screen.width < 1280`: reducir ancho del panel a 340 y apilar los botones de dinero en dos filas de 180×36.

## 8. Contrato con el backend (para Gabriel)

La UI solo necesita de núcleo del plugin estas operaciones/propiedades, sin importar la implementación:

```csharp
// Estado que la UI lee:
bool NoRecibirDanio { get; set; }        // toggle 1
bool ResistenciaInfinita { get; set; }   // toggle 2
bool SonidoAvisos { get; set; }          // toggle 3
bool EsHost { get; }                     // condición dinero
bool MenuVisible { get; set; }           // F5 la alterna
KeyCode Hotkey { get; }                  // config, default F5

// Acciones que la UI invoca:
void AnadirDinero(int cantidad);         // 100 | 1000; solo válida si EsHost
```

- El plugin debe exponer lo anterior (estático o singleton accesible desde la clase OnGUI).
- `AnadirDinero` puede fallar: la UI muestra el toast rojo de §4.4 usando el `bool`/evento de resultado que devuelva.
- Los parches deben fallar en silencio con log (Fase 6 del plan): la UI **no** muestra errores de firma; eso queda en BepInEx Log.

## 9. Fuera de alcance

- Sin skin custom, sin animaciones, sin imágenes ni iconos externos (el "⚠" es carácter Unicode del set estándar de Unity; si no renderiza, sustituir por la palabra `AVISO:`).
- Sin gestión de otros jugadores, sin lista de jugadores, sin config avanzada en pantalla (hotkey se edita en `BepInEx/config/*.cfg`).
- El dinero no tiene input libre de cantidad: solo presets 100/1000 (decisiones de producto → Pausiar si quiere más).

## 10. Criterios de aceptado QA

1. Menú abre/cierra con F5 y con `[—]`; posición recuerda última sesión.
2. Tres toggles visibles con estado literal ON/OFF legible sobre fondo oscuro del juego.
3. Como cliente: botones de dinero gris + mensaje ámbar explicativo; como host: habilitados y funcionales (verificado por Mary Jane).
4. Cambio host→cliente en vivo actualiza el estado del botón sin reproceso.
5. Texto completo en español; ningún control depende solo del color; contraste ≥ 4.5:1 en todos los textos.
6. Sin assets externos: el .dll único + BepInEx funciona en 720p, 1080p y 1440p.
