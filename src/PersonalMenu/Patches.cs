using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PersonalMenu.Core
{
    /// <summary>
    /// Parches Harmony dinámicos. Se aplican UNA vez al arrancar sobre los métodos que se
    /// resuelvan por nombre; los toggles se comprueban en cada llamada. Si una actualización
    /// del juego renombra o cambia una firma, el parche simplemente no se aplica y queda un
    /// warning claro en el log de BepInEx (el juego no se rompe).
    /// </summary>
    internal static class CorePatches
    {
        /// <summary>True si NO se encontró ningún método de consumo que parchear → usar clamp por frame.</summary>
        public static bool StaminaClampNeeded = true;

        public static int PatchedDamageMethods { get; private set; }
        public static int PatchedStaminaMethods { get; private set; }

        public static void ApplyAll(Harmony harmony, string damageNames, string staminaNames)
        {
            ApplyDamagePatches(harmony, damageNames);
            ApplyStaminaPatches(harmony, staminaNames);
        }

        // ---------------- No recibir daño ----------------

        private static void ApplyDamagePatches(Harmony harmony, string configuredNames)
        {
            List<MethodInfo> methods;
            try
            {
                methods = GameRefs.FindPlayerMethods(configuredNames, "Damage",
                    m => m.ReturnType == typeof(void) && m.GetParameters().Length >= 1);
            }
            catch (Exception ex)
            {
                GameRefs.Log("Warning", "Menú Personal: fallo resolviendo métodos de daño: " + ex.Message);
                return;
            }

            if (methods.Count == 0)
            {
                GameRefs.Log("Warning", "Menú Personal: no se encontró ningún método de daño en 'Player' " +
                    "(firma cambiada o renombrada). Toggle 'No recibir daño' inactivo. " +
                    "Verifica el nombre con ILSpy y ajústalo en config 'damage-methods'.");
                return;
            }

            var prefix = new HarmonyMethod(typeof(CorePatches), nameof(DamagePrefix));
            foreach (var m in methods)
            {
                try
                {
                    harmony.Patch(m, prefix: prefix);
                    PatchedDamageMethods++;
                    GameRefs.Log("Info", $"Parche 'no recibir daño' aplicado en: {Describe(m)}");
                }
                catch (Exception ex)
                {
                    GameRefs.Log("Warning", $"Menú Personal: no se pudo parchear {Describe(m)}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Prefix genérico: cancela el daño SOLO si la víctima es el jugador local y el toggle
        /// está activo. Otros jugadores reciben daño normal; en singleplayer esto solo te afecta a ti.
        /// </summary>
        public static bool DamagePrefix(object __instance)
        {
            if (!PersonalMenuPlugin.NoRecibirDanio) return true;
            if (GameRefs.IsLocalPlayer(__instance)) return false; // skip original
            return true;
        }

        // ---------------- Resistencia infinita ----------------

        private static void ApplyStaminaPatches(Harmony harmony, string configuredNames)
        {
            List<MethodInfo> methods;
            try
            {
                methods = GameRefs.FindPlayerMethods(configuredNames, "Stamina", m =>
                {
                    // Métodos de CONSUMO: nombre de acción (no getters/fields accesors),
                    // con parámetro numérico o void, y que no empiecen por "get_".
                    var ps = m.GetParameters();
                    bool hasNumeric = Array.Exists(ps, p =>
                        p.ParameterType == typeof(float) || p.ParameterType == typeof(int) || p.ParameterType == typeof(double));
                    return (hasNumeric || ps.Length == 0) &&
                           (m.Name.IndexOf("use", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("consume", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            m.Name.IndexOf("cost", StringComparison.OrdinalIgnoreCase) >= 0);
                });
            }
            catch (Exception ex)
            {
                GameRefs.Log("Warning", "Menú Personal: fallo resolviendo métodos de stamina: " + ex.Message);
                return;
            }

            if (methods.Count == 0)
            {
                StaminaClampNeeded = true;
                GameRefs.Log("Warning", "Menú Personal: no se encontró método de consumo de stamina. " +
                    "Se usará el fallback de clamp por frame sobre el campo 'stamina' del jugador local. " +
                    "Si no funciona, verifica el nombre con ILSpy y ajústalo en config 'stamina-methods'.");
                return;
            }

            var prefix = new HarmonyMethod(typeof(CorePatches), nameof(StaminaPrefix));
            foreach (var m in methods)
            {
                try
                {
                    harmony.Patch(m, prefix: prefix);
                    PatchedStaminaMethods++;
                    GameRefs.Log("Info", $"Parche 'resistencia infinita' (multiplicador 0) aplicado en: {Describe(m)}");
                }
                catch (Exception ex)
                {
                    GameRefs.Log("Warning", $"Menú Personal: no se pudo parchear {Describe(m)}: {ex.Message}");
                }
            }

            // Si además existe el campo, el clamp queda como red de seguridad barata.
            StaminaClampNeeded = PatchedStaminaMethods == 0;
        }

        /// <summary>
        /// Prefix: con el toggle ON se cancela el consumo SOLO en la instancia del jugador
        /// local (equivalente a multiplicador 0 para ti). Otros jugadores siguen consumiendo
        /// resistencia normal, también cuando Pausiar es el host.
        /// </summary>
        public static bool StaminaPrefix(object __instance)
        {
            if (!PersonalMenuPlugin.ResistenciaInfinita) return true;
            if (GameRefs.IsLocalPlayer(__instance)) return false; // skip original (solo self)
            return true;
        }

        private static string Describe(MethodInfo m) =>
            $"{m.DeclaringType?.Name}.{m.Name}({string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})";
    }
}
