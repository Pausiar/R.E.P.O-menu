# Informe QA — Plugin "Menú Personal" (R.E.P.O.)

Autor: Mary Jane Quality Engineer · Fecha: 2026-11-06 · Entrada: commit de trabajo `shared\repo-personal-menu\` (README.md, ui-spec.md v1.0, src\PersonalMenu\ 5 archivos)
Método: revisión estática de código completa. **Limitación de entorno documentada**: no hay .NET SDK ni assemblies del juego en este entorno (`which dotnet` → no encontrado; sin `msbuild`/`csc`/`mono`), por lo que la compilación real y las pruebas in-game (checklist 2–9) quedan **pendientes para verificación local de Pausiar** y no se dan por aprobadas.

## Veredicto global: **CONDICIONAL — 2 bloqueantes antes de la entrega final**

---

## 1. Resultados por punto del encargo

### 1.1 Revisión estática / build (checklist #1) — **RECHAZADO (bloqueante B1)**
- `MenuGUI.cs` usa el identificador `MoneyGap` en 5 sitios (líneas 130, 198, 199, 203, 205) y **no está declarado en ninguna parte del proyecto**. `dotnet build` fallará con `CS0103: The name 'MoneyGap' does not exist in the current context`.
- Evidencia: grep `MoneyGap` → 5 usos, 0 declaraciones (const/private/searched in full file).
- Corrección: declarar p. ej. `private const float MoneyGap = 12f;` (spec §2: separación 12 px) en MenuGUI.cs.
- Limitación: sin compilador aquí; tras el fix, Pausiar debe ejecutar `dotnet build -c Release` localmente (checklist #1 sigue pendiente de evidencia real de compilación).

### 1.2 Contrato §8 (PersonalMenuPlugin.cs) — **CONDICIONAL (aprobado funcional, 1 desviación menor)**
| Miembro exigido | Implementado | Estado |
|---|---|---|
| `NoRecibirDanio { get; set; }` | sí, persistido en config | ✓ |
| `ResistenciaInfinita { get; set; }` | sí, persistido | ✓ |
| `SonidoAvisos { get; set; }` | sí, persistido | ✓ (nota M3) |
| `EsHost { get; }` | `=> GameRefs.IsHost()`, sin caché: evaluado en CADA lectura | ✓ |
| `MenuVisible { get; set; }` | sí | ✓ |
| `Hotkey { get; }` (KeyCode) | expuesto como `HotkeyKey`; `Hotkey` es `ConfigEntry<KeyCode>` interno | ⚠ desviación de nombre; sin impacto real (UI y plugin en el mismo assembly usan HotkeyKey) |
| `AnadirDinero(int)` | sí (devuelve bool + `UltimoAvisoDinero`, admitido por §8) | ✓ |

### 1.3 Restricción de alcance acordada con el owner — **RECHAZADO (bloqueante B2)**
- **Dinero host-only**: ✓ correcto. `AnadirDinero` comprueba `EsHost` y es no-op con warning en log + aviso UI; la UI deshabilita los botones con `GUI.enabled = isHost` (no solo apariencia) y re-evalúa host en cada draw → cambio host→cliente en vivo reflejado sin reinicio.
- **Daño solo jugador local**: ✓ `DamagePrefix` cancela solo si `GameRefs.IsLocalPlayer(__instance)`.
- **Resistencia — BLOQUEANTE B2**: `StaminaPrefix` (Patches.cs:135-138) cancela el consumo **en TODAS las instancias de Player**, sin comprobar jugador local. Cuando Pausiar es host (escenario autorizado para dinero y el caso normal de partida propia), el host es autoritativo sobre los jugadores remotos: el toggle ON cancelaría también la resistencia de **otros jugadores**, violando el alcance acordado ("los parches aplican solo al jugador local, nunca a otros"). El fallback `ClampLocalStamina` sí es local-only.
  - Corrección propuesta (para Gabriel, no la aplico yo): `public static bool StaminaPrefix(object __instance) { if (!PersonalMenuPlugin.ResistenciaInfinita) return true; return !GameRefs.IsLocalPlayer(__instance); }`
- **Sin `Time.timeScale=0`**: ✓ verificado por grep — la única aparición es un comentario ("sin Time.timeScale=0").

### 1.4 Fallo silencioso con warning en log — **APROBADO con defecto menor (M1)**
- Cada parche en try/catch individual; firma ausente/cambiada → `LogWarning` descriptivo en LogOutput.log + función inactiva + sin errores en pantalla (la UI nunca pinta errores de firma, conforme a §8). ✓
- Sin Player type → warning y funciones inactivas, sin excepción. ✓
- **M1 (bajo)**: `PersonalMenuPlugin.cs:103-104` — el string del catch no tiene prefijo `$` en la segunda parte: el detalle de la excepción se registra literalmente como `{ex.GetBaseException().Message}` (se pierde el mensaje real). Fix: añadir `$`.

### 1.5 README para usuario no técnico — **CONDICIONAL (aprobado con 1 enlace defectuoso M2)**
- Opciones A (r2modman) y B (manual BepInExPack) paso a paso, reversibles, con ruta de salida del build correcta (`bin/Release/net472/PersonalMenu.dll`), tabla de claves de config coherente con el código, procedimiento ILSpy claro y explicado (§4). Buena calidad general. ✓
- **M2 (bajo)**: README §3 link roto/incompleto `https://github.com/ThunderstoreModManager?` (URL con `?`, no es la de r2modman). Sustituir por la oficial (r2modman.net / repo `tbhuttonr/r2modman`... verificar) antes de entregar a Pausiar.

## 2. Checklist README §7 — estado tras esta QA

| # | Punto | Estado |
|---|---|---|
| 1 | dotnet build sin errores | **Rechazado estáticamente (B1)**; re-verificar compilación real tras fix |
| 2 | Arranque con log de carga + warnings sin excepciones | Pendiente in-game (estáticamente conforme) |
| 3 | F5 abre/cierra, [—] cierra, sin congelación | Pendiente in-game (código conforme; ver nota M4: F5 no persiste posición, [—] sí) |
| 4 | Dinero host/cliente con toast/ámbar | Pendiente in-game (lógica verificada ✓) |
| 5 | Cambio host→cliente en vivo | Pendiente in-game (sin caché de host verificado ✓) |
| 6 | Daño solo jugador local, otros reciben daño normal | Pendiente in-game (prefix ✓); **pero resistencia NO es local-only (B2)** |
| 7 | Resistencia >60 s / OFF normal | Pendiente in-game (requiere fix B2 primero) |
| 8 | Toggles persisten tras reinicio | Pendiente in-game (persistencia en config verificada ✓) |
| 9 | Legibilidad 720p/1080p/1440p y tema oscuro | Pendiente in-game (escala clamp [0.75,1.5], paleta y estados literales ON/OFF conformes a §3/§6/§7 ✓) |

## 3. Defectos menores / observaciones no bloqueantes
- **M3**: el setter de `SonidoAvisos` no emite beep al alternar su propio toggle (spec §4.3 "al activar/desactivar cualquier función"). Ambiguo, cosmético.
- **M4**: cerrar con F5 no guarda `menuX/menuY` (solo `[—]` los guarda). Spec §1 pide recordar al cerrar en general. Bajo.
- **M5 (riesgo medio, no verificable sin DLL del juego)**: `TryAddMoney` escribe en el **primer** campo numérico que case `*money*/*cash*/*balance*`, sin excluir contadores tipo `maxMoney`/`moneyTimer`. Si el juego tiene esos campos y aparecen primero, el +100 iría al campo equivocado. Recomendado: excluir nombres con `max` (igual que en stamina) y/o priorizar `int`. Mitigación existente: clave `money-fields` permite fijar el nombre exacto tras ILSpy — reflejarlo en el README como paso recomendado, no solo fallback.
- `EsHost` recorre todos los types cargados en cada draw de la UI: coste de reflexión por frame mientras el menú está abierto; aceptable (menú no modal, uso breve), anotar como riesgo de perf menor.

## 4. Riesgo residual
- Nombres de tipos/métodos por autodetección sin assemblies del juego: el comportamiento real (qué firma hace daño a quién, si `__instance` del método de daño es la víctima) **no es verificable hasta ejecutar contra el juego instalado** — guía ILSpy en README §4 cubre el ajuste.
- Pruebas de red con 2 jugadores, legibilidad real y persistencia: pendientes de ejecución local por Pausiar (checklist 2–9).
- Godmode/dinero en partidas con otras personas: riesgo social/documentado, decisión del owner (fuera del alcance QA).

## 5. Acción solicitada al coordinador
Bloqueantes para devolver a Gabriel BackEnd: **B1** (`MoneyGap` no declarado → build falla) y **B2** (`StaminaPrefix` sin comprobación de jugador local → viola alcance host/otros jugadores). Menores: M1 (`$` en log), M2 (link r2modman). Tras fixes: re-ejecutar esta revisión estática + `dotnet build -c Release` local de Pausiar antes de la entrega final.

*Este informe no incluye cambios de código: QA solo reporta (evidencia = rutas y números de línea sobre los archivos entregados).*

---

## 6. Re-revisión estática tras fixes de Gabriel (2da pasada, mismo día)

Evidencia verificada sobre los ficheros finales en `shared\repo-personal-menu\`:

| Hallazgo | Fix verificado | Estado |
|---|---|---|
| B1 | `MenuGUI.cs:31` — `private const float MoneyGap = 12f;` declarado; los 5 usos (131, 199-200, 204, 206) resueltos. CS0103 eliminado. | **APROBADO** |
| B2 | `Patches.cs:139-144` — `StaminaPrefix(object __instance)`: cancela solo si toggle ON **e** `GameRefs.IsLocalPlayer(__instance)`; resto de instancias devuelve true (consumo normal). Misma semántica que DamagePrefix; alcance "solo jugador local" cumplido también siendo host. `nameof(StaminaPrefix)` sigue apuntando correctamente. | **APROBADO** |
| M1 | `PersonalMenuPlugin.cs:103-104` — `$` añadido en la segunda parte; el detalle de la excepción se interpola. | **APROBADO** |
| M2 | README §3:45 — enlace corregido a `https://github.com/ebkr/r2modman` (repo oficial). | **APROBADO** |
| M5 | `GameRefs.cs:271-277` — autodetección excluye `max/timer/regen/rate`; override `money-fields` con prioridad exacta sin exclusiones; README §4 documenta el override como recomendado. | **APROBADO** |

**Veredicto final de la revisión estática: APROBADA.** Sin bloqueantes conocidos en el código entregado.

Pendientes que esta QA no puede cerrar en este entorno (documentados, no aprobados):
- `dotnet build -c Release` real (aquí no hay .NET SDK) → verificación final de Pausiar.
- Checklist in-game §7 puntos 2-9 (arranque BepInEx, F5, host/cliente dinámico, daño/stamina con 2 jugadores, persistencia, legibilidad 720p-1440p) → ejecución local de Pausiar.
- Firmas reales de Assembly-CSharp/FishNet autodetectadas → pueden requerir ajuste por `damage-methods`/`stamina-methods`/`money-fields` según guía ILSpy del README.
