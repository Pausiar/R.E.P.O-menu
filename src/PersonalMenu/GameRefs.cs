using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PersonalMenu.Core
{
    /// <summary>
    /// Resolución por reflexión de los tipos/miembros de R.E.P.O. (Assembly-CSharp) y de
    /// FishNet (red). Todo es name-based y con candidatos configurables: si una actualización
    /// del juego renombra una firma, el plugin falla en silencio con log y se puede reajustar
    /// editando BepInEx/config/pausiar.personalmenu.cfg sin recompilar.
    /// </summary>
    internal static class GameRefs
    {
        private const BindingFlags AllStatic =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Nombres públicos conocidos de R.E.P.O. (verificar con ILSpy; ver README §4).
        private static readonly string[] PlayerTypeNames = { "Player" };
        private static readonly string[] LocalPlayerMembers =
            { "localPlayer", "LocalPlayer", "currentLocalPlayer", "CurrentLocalPlayer" };
        private static readonly string[] NetworkOwnerMembers =
            { "IsOwner", "IsLocalPlayer", "isLocalPlayer", "hasAuthority", "HasAuthority" };
        private static readonly string[] HostManagerTypeNames =
            { "NetworkManager", "LobbyManager", "GameSceneManager", "GameManager" };
        private static readonly string[] HostFlagMembers =
            { "IsHost", "isHost", "IsListenHost", "isListenHost", "hosting", "IsHosting" };
        private static readonly string[] MoneyHints = { "money", "cash", "balance" };
        private static readonly string[] MoneyHolderHints = { "playerInfo", "info", "stats" };
        private static readonly string[] StaminaFieldHints = { "stamina" };

        private static Type _playerType;
        private static bool _playerTypeSearched;
        private static Component _localPlayerCache;
        private static float _localPlayerCacheTime = -10f;

        public static void Log(string level, string msg) =>
            PersonalMenuPlugin.Log?.Log((BepInEx.Logging.LogLevel)Enum.Parse(typeof(BepInEx.Logging.LogLevel), level), msg);

        // ---------------- Tipos ----------------

        public static Type PlayerType
        {
            get
            {
                if (_playerTypeSearched) return _playerType;
                _playerTypeSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var name in PlayerTypeNames)
                    {
                        var t = asm.GetType(name) ?? asm.GetTypes()
                            .FirstOrDefault(x => x.Name == name && typeof(Component).IsAssignableFrom(x));
                        if (t == null) continue;
                        _playerType = t;
                        Log("Info", $"Tipo de jugador resuelto: {t.FullName} ({asm.GetName().Name})");
                        return _playerType;
                    }
                }
                Log("Warning", "Menú Personal: no se encontró el tipo 'Player' en los assemblies del juego. " +
                               "Funciones de daño/resistencia/dinero quedarán inactivas hasta ajustar la config.");
                return null;
            }
        }

        private static IEnumerable<Type> AllLoadedTypes()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types) yield return t;
            }
        }

        // ---------------- Jugador local ----------------

        /// <summary>Instancia del jugador local, con caché corto (0.5 s) por seguridad de rendimiento.</summary>
        public static Component GetLocalPlayer()
        {
            if (_localPlayerCache != null && !_localPlayerCache.IsDestroyedOrNull()
                && Time.realtimeSinceStartup - _localPlayerCacheTime < 0.5f)
                return _localPlayerCache;

            _localPlayerCache = ResolveLocalPlayer();
            _localPlayerCacheTime = Time.realtimeSinceStartup;
            return _localPlayerCache;
        }

        private static Component ResolveLocalPlayer()
        {
            var pt = PlayerType;
            if (pt == null) return null;

            // Estrategia 1: miembro estático conocido (Player.localPlayer etc.).
            foreach (var name in LocalPlayerMembers)
            {
                var f = pt.GetField(name, AllStatic);
                if (f != null && f.FieldType.IsAssignableFrom(pt) && f.GetValue(null) is Component c1) return c1;
                var p = pt.GetProperty(name, AllStatic);
                if (p != null && p.PropertyType.IsAssignableFrom(pt) && p.GetValue(null) is Component c2) return c2;
            }

            // Estrategia 2: buscar entre todos los Player vivos cuál es el nuestro por red
            // (FishNet NetworkBehaviour.IsOwner / NetworkObject.IsOwned).
            var players = FindActivePlayers();
            Component ownerMatch = null, fallbackSingle = null;
            foreach (var pl in players)
            {
                fallbackSingle ??= pl;
                foreach (var m in NetworkOwnerMembers)
                {
                    if (ReadBoolMember(pl, m, AllInstance)) { ownerMatch = pl; break; }
                }
                if (ownerMatch != null) break;
            }

            if (ownerMatch == null && players.Count == 1) ownerMatch = fallbackSingle;
            return ownerMatch;
        }

        public static List<Component> FindActivePlayers()
        {
            var result = new List<Component>();
            var pt = PlayerType;
            if (pt == null) return result;
            foreach (var o in Object.FindObjectsOfType(pt))
                if (o is Component c && c.gameObject.activeInHierarchy)
                    result.Add(c);
            return result;
        }

        public static bool IsLocalPlayer(object maybePlayer)
        {
            if (maybePlayer == null) return false;
            var local = GetLocalPlayer();
            if (local != null && ReferenceEquals(local, maybePlayer)) return true;
            if (maybePlayer is Component c && local != null && c.gameObject == local.gameObject) return true;
            return false;
        }

        // ---------------- Host ----------------

        /// <summary>
        /// Se evalúa en vivo en cada llamada (sin caché entre sesiones): un cambio
        /// host→cliente debe reflejarse inmediatamente (§8 de la spec).
        /// </summary>
        public static bool IsHost()
        {
            // Estrategia 1: NetworkManager de FishNet (singleton estático Instance/Singleton + IsHost/IsServer).
            foreach (var t in AllLoadedTypes())
            {
                if (!HostManagerTypeNames.Contains(t.Name)) continue;

                foreach (var flagName in HostFlagMembers)
                {
                    var sf = t.GetField(flagName, AllStatic);
                    if (sf != null && sf.FieldType == typeof(bool)) return (bool)sf.GetValue(null);
                    var sp = t.GetProperty(flagName, AllStatic);
                    if (sp != null && sp.PropertyType == typeof(bool))
                    {
                        try { if (sp.GetGetMethod(true) != null && !sp.GetIndexParameters().Any()) return (bool)sp.GetValue(null); }
                        catch { /* instancia requerida: se intenta más abajo */ }
                    }
                }

                // Singleton de red: NetworkManager.Instance.IsHost
                foreach (var instName in new[] { "Instance", "Singleton", "instance" })
                {
                    object inst = null;
                    var inf = t.GetField(instName, AllStatic);
                    if (inf != null) inst = inf.GetValue(null);
                    else
                    {
                        var inp = t.GetProperty(instName, AllStatic);
                        if (inp != null) inst = inp.GetValue(null);
                    }
                    if (inst == null) continue;
                    foreach (var flagName in HostFlagMembers.Concat(new[] { "IsServer", "isServer" }))
                    {
                        if (inst is Component comp && ReadBoolMember(comp, flagName, AllInstance)) return true;
                        var f = t.GetField(flagName, AllInstance);
                        if (f != null && f.FieldType == typeof(bool)) { if ((bool)f.GetValue(inst)) return true; }
                        var p = t.GetProperty(flagName, AllInstance);
                        if (p != null && p.PropertyType == typeof(bool)) { try { if ((bool)p.GetValue(inst)) return true; } catch { } }
                    }
                }
            }

            // Estrategia 2: componentes de escena (Lobby/GameManager) con flag de host.
            foreach (var t in AllLoadedTypes())
            {
                if (!HostManagerTypeNames.Contains(t.Name) || !typeof(Component).IsAssignableFrom(t)) continue;
                foreach (var o in Object.FindObjectsOfType(t))
                    foreach (var flagName in HostFlagMembers)
                        if (o is Component c && ReadBoolMember(c, flagName, AllInstance))
                            return true;
            }

            return false;
        }

        // ---------------- Stamina ----------------

        /// <summary>Escribe la stamina local en su máximo (fallback sin Harmony).</summary>
        public static void ClampLocalStamina()
        {
            var pl = GetLocalPlayer();
            if (pl == null) return;
            var t = pl.GetType();
            foreach (var f in t.GetFields(AllInstance))
            {
                if (f.FieldType != typeof(float) && f.FieldType != typeof(int)) continue;
                var lname = f.Name.ToLowerInvariant();
                if (!StaminaFieldHints.Any(h => lname.Contains(h)) || lname.Contains("max") || lname.Contains("regen")) continue;

                float max = float.MaxValue;
                foreach (var mf in t.GetFields(AllInstance))
                {
                    if (mf.Name.ToLowerInvariant().Contains("maxstamina") && (mf.FieldType == typeof(float) || mf.FieldType == typeof(int)))
                    {
                        max = Convert.ToSingle(mf.GetValue(pl));
                        break;
                    }
                }
                if (Convert.ToSingle(f.GetValue(pl)) < max)
                    f.SetValue(pl, f.FieldType == typeof(int) ? (object)(int)max : (object)max);
                return; // primer campo de stamina encontrado
            }
        }

        // ---------------- Dinero ----------------

        public static bool TryAddMoney(int cantidad, string configuredFieldName, out string mensaje)
        {
            mensaje = "";
            var pl = GetLocalPlayer();
            if (pl == null) { mensaje = "jugador local no resuelto"; return false; }

            var holders = new List<object> { pl };
            foreach (var f in pl.GetType().GetFields(AllInstance))
            {
                if (f.FieldType.IsPrimitive || f.FieldType == typeof(string)) continue;
                if (!typeof(object).IsAssignableFrom(f.FieldType)) continue;
                var lname = f.Name.ToLowerInvariant();
                if (MoneyHolderHints.Any(h => lname.Contains(h)))
                {
                    var val = f.GetValue(pl);
                    if (val != null) holders.Add(val);
                }
            }

            foreach (var holder in holders)
            {
                var ht = holder.GetType();
                foreach (var f in ht.GetFields(AllInstance))
                {
                    if (f.FieldType != typeof(float) && f.FieldType != typeof(int) && f.FieldType != typeof(double)) continue;
                    var lname = f.Name.ToLowerInvariant();
                    bool nameOk;
                    if (!string.IsNullOrWhiteSpace(configuredFieldName))
                    {
                        // Override explícito (recomendado tras verificar con ILSpy): sin exclusiones.
                        nameOk = lname == configuredFieldName.Trim().ToLowerInvariant();
                    }
                    else
                    {
                        // Autodetección: excluir contadores derivados (maxMoney, moneyTimer, ...).
                        nameOk = MoneyHints.Any(h => lname.Contains(h)) &&
                                 !lname.Contains("max") && !lname.Contains("timer") &&
                                 !lname.Contains("regen") && !lname.Contains("rate");
                    }
                    if (!nameOk) continue;

                    var current = Convert.ToDouble(f.GetValue(holder));
                    var newValue = current + cantidad;
                    f.SetValue(holder, Convert.ChangeType(newValue, f.FieldType));
                    mensaje = $"{ht.Name}.{f.Name} = {newValue:0}";
                    Log("Info", $"Dinero aplicado (host): {mensaje}");
                    return true;
                }
            }

            mensaje = "campo de dinero no encontrado; ajusta 'money-fields' en la config";
            Log("Warning", $"Menú Personal: {mensaje}");
            return false;
        }

        // ---------------- Utilidades ----------------

        public static bool ReadBoolMember(object target, string name, BindingFlags flags)
        {
            var t = target.GetType();
            var f = t.GetField(name, flags);
            if (f != null && f.FieldType == typeof(bool))
            {
                try { return (bool)f.GetValue(target); } catch { return false; }
            }
            var p = t.GetProperty(name, flags);
            if (p != null && p.PropertyType == typeof(bool) && p.GetIndexParameters().Length == 0)
            {
                try { return (bool)p.GetValue(target); } catch { return false; }
            }
            return false;
        }

        /// <summary>Métodos de Player que casan con los nombres configurados o con el patrón por defecto.</summary>
        public static List<MethodInfo> FindPlayerMethods(string configuredNames, string defaultNamePattern, Func<MethodInfo, bool> extraFilter)
        {
            var found = new List<MethodInfo>();
            var pt = PlayerType;
            if (pt == null) return found;

            var explicitNames = (configuredNames ?? "")
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet();

            foreach (var m in pt.GetMethods(AllInstance))
            {
                if (m.IsSpecialName) continue;
                bool nameOk = explicitNames.Count > 0
                    ? explicitNames.Contains(m.Name.ToLowerInvariant())
                    : m.Name.IndexOf(defaultNamePattern, StringComparison.OrdinalIgnoreCase) >= 0;
                if (nameOk && (extraFilter == null || extraFilter(m)))
                    found.Add(m);
            }
            return found;
        }
    }

    internal static class ObjectExtensions
    {
        /// <summary>IsDestroyed sin depender de UnityEngine.Object en el receptor genérico.</summary>
        public static bool IsDestroyedOrNull(this Object o)
        {
            if (o == null) return true; // Unity "null" de objeto destruido
            try { return o == null; } catch { return true; }
        }
    }
}
