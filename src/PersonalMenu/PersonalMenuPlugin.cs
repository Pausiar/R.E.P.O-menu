using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using PersonalMenu.Core;

namespace PersonalMenu
{
    /// <summary>
    /// Menú Personal para R.E.P.O. — plugin BepInEx 5 + HarmonyX, solo cliente.
    /// Expone el contrato §8 de ui-spec.md (NoRecibirDanio, ResistenciaInfinita,
    /// SonidoAvisos, EsHost, MenuVisible, Hotkey, AnadirDinero).
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("R.E.P.O..exe")]
    public class PersonalMenuPlugin : BaseUnityPlugin
    {
        public const string Guid = "pausiar.personalmenu";
        public const string Name = "Menú Personal";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static PersonalMenuPlugin Instance;

        // ---- Config ----
        internal static ConfigEntry<KeyCode> Hotkey;
        internal static ConfigEntry<bool> CfgNoRecibirDanio;
        internal static ConfigEntry<bool> CfgResistenciaInfinita;
        internal static ConfigEntry<bool> CfgSonidoAvisos;
        internal static ConfigEntry<float> CfgMenuX;
        internal static ConfigEntry<float> CfgMenuY;
        internal static ConfigEntry<string> DamageMethodNames;
        internal static ConfigEntry<string> StaminaMethodNames;
        internal static ConfigEntry<string> MoneyFieldNames;

        // ---- Contrato §8 (estado que lee la UI) ----
        public static bool NoRecibirDanio
        {
            get => CfgNoRecibirDanio.Value;
            set { CfgNoRecibirDanio.Value = value; NotifyToggle(); }
        }

        public static bool ResistenciaInfinita
        {
            get => CfgResistenciaInfinita.Value;
            set { CfgResistenciaInfinita.Value = value; NotifyToggle(); }
        }

        public static bool SonidoAvisos
        {
            get => CfgSonidoAvisos.Value;
            set { CfgSonidoAvisos.Value = value; }
        }

        /// <summary>Se evalúa en CADA lectura: un cambio host→cliente se refleja sin reinicio.</summary>
        public static bool EsHost => GameRefs.IsHost();

        public static bool MenuVisible { get; set; }

        public static KeyCode HotkeyKey => Hotkey.Value;

        // Resultado de la última operación de dinero para el toast de la UI (§4.4).
        public static bool UltimoDineroOk { get; private set; }
        public static string UltimoAvisoDinero { get; private set; }
        public static float UltimoDineroUtc { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            Hotkey = Config.Bind("Menu", "hotkey", KeyCode.F5,
                "Atajo para abrir/cerrar el menú.");
            CfgNoRecibirDanio = Config.Bind("Funciones", "no-recibir-danio", false,
                "Estado persistido del toggle 'No recibir daño' (jugador local).");
            CfgResistenciaInfinita = Config.Bind("Funciones", "resistencia-infinita", false,
                "Estado persistido del toggle 'Resistencia infinita'.");
            CfgSonidoAvisos = Config.Bind("Funciones", "sonido-avisos", false,
                "Beep corto al alternar funciones (feedback sin mirar). No afecta al juego.");
            CfgMenuX = Config.Bind("Menu", "menuX", 20f, "Última posición X del panel (px a 1080p).");
            CfgMenuY = Config.Bind("Menu", "menuY", 20f, "Última posición Y del panel (px a 1080p).");
            DamageMethodNames = Config.Bind("Compatibilidad", "damage-methods", "",
                "Nombres exactos de métodos de daño separados por ';' (vacío = autodetección por nombre *Damage* en Player).");
            StaminaMethodNames = Config.Bind("Compatibilidad", "stamina-methods", "",
                "Nombres exactos de métodos de consumo de stamina separados por ';' (vacío = autodetección *Stamina*).");
            MoneyFieldNames = Config.Bind("Compatibilidad", "money-fields", "",
                "Nombre exacto del campo de dinero (vacío = autodetección *money* en Player/playerInfo).");

            Log.LogInfo($"{Name} v{Version} cargado. Resolviendo firmas del juego...");

            // Fase 6 del plan: los parches fallan en silencio con log claro.
            try
            {
                _harmony = new Harmony(Guid);
                CorePatches.ApplyAll(_harmony, DamageMethodNames.Value, StaminaMethodNames.Value);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Menú Personal: no se pudo aplicar algún parche (firma cambiada o no encontrada). " +
                               $"El juego sigue funcionando con las funciones disponibles. Detalle: {ex.GetBaseException().Message}");
            }

            MenuGUI.BuildStyles();
            Log.LogInfo("Listo. Pulsa " + Hotkey.Value + " en juego para abrir el menú.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(Hotkey.Value))
                MenuVisible = !MenuVisible;

            // Fallback de resistencia: clamp por frame si no se encontró método de consumo que parchear.
            if (ResistenciaInfinita && CorePatches.StaminaClampNeeded)
                GameRefs.ClampLocalStamina();
        }

        private void OnGUI()
        {
            if (MenuVisible)
                MenuGUI.Draw(Instance);
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { /* silencioso */ }
        }

        /// <summary>Acción §8: añadir dinero. No-op con aviso si no eres host.</summary>
        public static bool AnadirDinero(int cantidad)
        {
            if (!EsHost)
            {
                UltimoDineroOk = false;
                UltimoAvisoDinero = "AVISO: dinero bloqueado: solo disponible cuando eres el host de la sesión";
                UltimoDineroUtc = Time.realtimeSinceStartup;
                Log.LogWarning("AnadirDinero ignorado: el jugador local no es el host.");
                return false;
            }

            bool ok = GameRefs.TryAddMoney(cantidad, MoneyFieldNames.Value, out string msg);
            UltimoDineroOk = ok;
            UltimoAvisoDinero = ok ? $"+{cantidad} añadido ({msg})" : "Error: no se pudo aplicar (" + msg + ")";
            UltimoDineroUtc = Time.realtimeSinceStartup;
            NotifyToggle();
            return ok;
        }

        private static void NotifyToggle()
        {
            if (!SonidoAvisos) return;
            try { Console.Beep(880, 120); } catch { /* sin sonido si el entorno no lo permite */ }
        }
    }
}
