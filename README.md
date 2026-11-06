# Menú Personal — plugin BepInEx para R.E.P.O.

Plugin **solo cliente** (sin inyectores) con tres funciones que solo afectan al jugador local:

| Función | Comportamiento |
|---|---|
| No recibir daño | Parche Harmony (prefix) a los métodos de daño de `Player`: se cancela el daño **solo si la víctima es el jugador local**. Los demás jugadores reciben daño normal. |
| Resistencia infinita | Prefix a los métodos de consumo de stamina (equivalente a multiplicador 0). Si la firma no existe, fallback: clamp del campo `stamina` al máximo cada frame. |
| Dinero (+100 / +1000) | **Solo operativo cuando el jugador local es el host** de la sesión. Como cliente: botones bloqueados + aviso ámbar; `AnadirDinero` es no-op con log. El estado de host se re-evalúa en cada dibujo de la UI: un cambio host→cliente se refleja sin reiniciar. |

Menú: OnGUI puro, no modal, atajo configurable (defecto **F5**), sin `Time.timeScale = 0`, sin assets externos. Diseño según `ui-spec.md` v1.0 (Ana UI Designer).

## 1. Estructura

```
repo-personal-menu/
├── README.md                  ← este archivo
├── ui-spec.md                 ← especificación de UI (Ana)
└── src/PersonalMenu/
    ├── PersonalMenu.csproj    ← net472, BepInEx 5.4 + HarmonyX 2.10
    ├── PersonalMenuPlugin.cs  ← entrada, config BepInEx, contrato §8 de la spec
    ├── GameRefs.cs            ← resolución por reflexión de tipos/miembros del juego
    ├── Patches.cs             ← parches Harmony dinámicos (daño, stamina)
    └── MenuGUI.cs             ← menú OnGUI según spec
```

## 2. Compilación

Requisitos: .NET SDK 6+ (o MSBuild 17+) con acceso a nuget.org. **No** hacen falta los DLL del juego:
el plugin resuelve todas las firmas por reflexión en tiempo de ejecución, así que en tiempo de
compilación solo depende de `UnityEngine.Modules` (reference-only, no se distribuye), BepInEx y HarmonyX.

```bash
cd src/PersonalMenu
dotnet build -c Release
# Salida: bin/Release/net472/PersonalMenu.dll
```

Si el juego usara otra versión de Unity que rompiera la referencia de compilación, ajusta la
versión de `UnityEngine.Modules` en el `.csproj` a la que reporta el log de BepInEx al arrancar.

## 3. Instalación

### Opción A — r2modman (recomendada)
1. Instala [r2modman](https://github.com/ebkr/r2modman) → nuevo perfil para R.E.P.O. (Thunderstore).
2. En la pestaña **Modding Library / "Otros mods"** de r2modman, busca **BepInEx** e instala `BepInExPack` para R.E.P.O.
   (Thunderstore: <https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/>).
3. Botón derecho sobre el perfil → **Open in explorer** → carpeta `BepInEx/plugins/`.
4. Crea `BepInEx/plugins/PersonalMenu/` y copia ahí `PersonalMenu.dll`.
5. Arranca R.E.P.O. → aparece el log `[Menú Personal] ... cargado` y el menú con **F5**.

### Opción B — manual con BepInExPack
1. Descarga `BepInExPack` para R.E.P.O. y extráelo en la raíz del juego
   (`.../steamapps/common/R.E.P.O./`, acepta reubicar el `GameAssembly.dll`/`_Data` si el instalador lo pide).
2. Ejecuta el juego una vez para generar `BepInEx/`.
3. Copia `PersonalMenu.dll` a `BepInEx/plugins/`.
4. Reversible: borra el `.dll` (y opcionalmente la carpeta `BepInEx`).

## 4. Verificación de firmas con ILSpy (importante)

No tenemos los assemblies del juego en este entorno: los nombres objetivo son los **públicos
conocidos de R.E.P.O.** y el plugin los busca por patrón + candidatos configurables. Tras cada
actualización del juego, verifica con [ILSpy](https://github.com/icsharpcode/ILSpy) (o dnSpy):

1. Abre `R.E.P.O./R.E.P.O._Data/Managed/Assembly-CSharp.dll`.
2. Busca el tipo `Player` y comprueba:
   - **Daño**: métodos con `Damage` en el nombre que devuelvan `void` (p. ej. `TakeDamage(...)`).
   - **Stamina**: campo `stamina`/`maxStamina` y métodos de consumo (`UseStamina`, `ConsumeStamina`, `CostStamina`...).
   - **Dinero**: campo numérico con `money` en el nombre, en `Player` o en su `playerInfo`.
   - **Jugador local**: campo/propiedad estática (`localPlayer`, `CurrentLocalPlayer`...) o flag de red (`IsOwner` de FishNet).
3. En el menú principal del juego, ¿usas FishNet `NetworkManager`? Busca también los tipos
   `NetworkManager`, `LobbyManager`, `GameSceneManager` y sus flags `IsHost`/`isHost`.

Si algún nombre cambió, **no hay que recompilar**: edita
`BepInEx/config/pausiar.personalmenu.cfg` (se crea solo al arrancar):

| Clave | Uso |
|---|---|
| `damage-methods` | Nombres exactos separados por `;` (vacío = patrón `*Damage*` en `Player`) |
| `stamina-methods` | Nombres exactos de consumo (vacío = patrón `*Stamina*`) |
| `money-fields` | **Override recomendado**: nombre exacto del campo de dinero verificado con ILSpy (vacío = autodetección `*money*`/`*cash*`/`*balance*`, excluyendo `*max*`/`*timer*`/`*regen*`/`*rate*`) |
| `hotkey` | Atajo del menú (defecto `F5`) |
| `menuX`/`menuY` | Última posición del panel |
| `no-recibir-danio` / `resistencia-infinita` / `sonido-avisos` | Estado persistido de los toggles |

## 5. Comportamiento ante fallos de firma (Fase 6 del plan)

- Cada parche se aplica en `try/catch`. Si una firma no existe o cambió: **warning claro en el
  log de BepInEx** (`.../BepInEx/LogOutput.log`) y la función queda inactiva; el resto del
  plugin y el juego siguen funcionando. Ningún error de firma se muestra en pantalla (spec §8).
- Resistencia tiene fallback de clamp por frame si no hay método de consumo parcheable.
- `AnadirDinero` como cliente es no-op con aviso en la UI + warning en log (restricción host-only confirmada por Pausiar).

## 6. Notas de seguridad/red

- El dinero solo toca el estado autoritativo del host → sin desync esperado. Aun así, usarlo
  en partidas con otras personas puede afectar a su experiencia: decisión del usuario.
- Godmode/resistencia son locales por diseño (comparación de instancia contra jugador local).

## 7. Checklist QA (para Mary Jane)

1. Compilación `dotnet build -c Release` sin errores.
2. Arranque con BepInExPack: log de carga + warnings si faltan firmas (no excepciones).
3. F5 abre/cierra; `[—]` cierra y recuerda posición; sin congelación del juego (no `timeScale=0`).
4. Como host: `+100`/`+1000` incrementan el saldo mostrado y toast verde; como cliente: botones
   grises + aviso ámbar y no-op.
5. Cambio host→cliente en vivo (dejar la partida propia y unirse a otra) → estado del botón
   dinero actualizado sin reiniciar.
6. Toggle "No recibir daño" ON: el jugador local no pierde salud ante enemigos/trampas; otro
   jugador sigue recibiendo daño normal (partida 2 jugadores en red local).
7. Toggle "Resistencia infinita" ON: correr >60 s sin agotarse; OFF vuelve a consumir normal.
8. Toggles persisten tras reiniciar el juego.
9. Legibilidad 720p/1080p/1440p y tema oscuro (contraste de la spec §3/§7).
