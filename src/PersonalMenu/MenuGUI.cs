using System;
using UnityEngine;

namespace PersonalMenu.Core
{
    /// <summary>
    /// OnGUI/IMGUI puro según ui-spec.md v1.0 (Ana UI Designer).
    /// Sin assets externos, sin Canvas, sin Time.timeScale=0. No modal.
    /// Lee y muta el estado exclusivamente a través del contrato §8 (PersonalMenuPlugin.*).
    /// Todas las medidas se declaran a 1080p y se escalan con GUI.matrix (§1).
    /// </summary>
    internal static class MenuGUI
    {
        // ---- Paleta (§3) ----
        private static Color PanelBg   => Hex(0x1B1B1F, 0.92f);
        private static Color TitleBg   => Hex(0x26262C);
        private static Color Border    => Hex(0x3A3A42);
        private static Color TextPri   => Hex(0xF2F2F2);
        private static Color TextSec   => Hex(0x9E9E9E);
        private static Color AccentOn  => Hex(0x4CAF7D);
        private static Color AccentOff => Hex(0x6E6E76);

        // ---- Medidas base a 1080p (§2) ----
        private const float PanelW = 420f;        // 340f si Screen.width < 1280 (§7)
        private const float TitleH = 34f;
        private const float ToggleRowH = 40f;
        private const float RowGap = 8f;
        private const float Pad = 12f;
        private const float MoneyBtnW = 180f;
        private const float MoneyBtnH = 36f;
        private const float MoneyGap = 12f; // separación 12px entre botones de dinero (spec §2)
        private static readonly Color32 SepColor = new Color32(0x2E, 0x2E, 0x2E, 0xFF);

        // ---- Estilos ----
        private static GUIStyle sPanel, sTitle, sTitleText, sRow, sStateOn, sStateOff,
                                sSec, sAmber, sOk, sError, sBtn, sBtnDisabled;
        private static Texture2D _white;
        private static bool _stylesReady;

        // ---- Estado de interacción (coordenadas lógicas a 1080p) ----
        private static Rect _panel;
        private static bool _dragPending;
        private static string _toastMsg;
        private static bool _toastOk;
        private static float _toastUntil;
        private static float _busyUntil; // estado Busy (§4.4) tras pulsar dinero

        private static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static void BuildStyles()
        {
            if (_stylesReady) return;

            _white = MakeTex(Color.white);

            Texture2D MakeTexCol(Color c) => MakeTex(c);

            sPanel = new GUIStyle(GUI.skin.box) { padding = RectOffset.zero };
            sPanel.normal.background = MakeTexCol(PanelBg);
            sPanel.normal.textColor = TextPri;

            sTitle = new GUIStyle(GUI.skin.box) { padding = RectOffset.zero };
            sTitle.normal.background = MakeTexCol(TitleBg);

            sTitleText = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sTitleText.normal.textColor = TextPri;

            sRow = new GUIStyle(GUI.skin.toggle) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(4, 0, 0, 0) };
            sRow.normal.textColor = TextPri;
            sRow.hover.textColor = TextPri;
            sRow.active.textColor = TextPri;
            sRow.onNormal.textColor = TextPri;
            sRow.onHover.textColor = TextPri;

            sStateOn = MakeStateStyle(AccentOn);
            sStateOff = MakeStateStyle(AccentOff);

            sSec = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            sSec.normal.textColor = TextSec;

            sAmber = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft };
            sAmber.normal.textColor = Hex(0xE0A458);
            sOk = new GUIStyle(sAmber); sOk.normal.textColor = AccentOn;
            sError = new GUIStyle(sAmber); sError.normal.textColor = Hex(0xD36B6B);

            sBtn = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            sBtn.normal.background = MakeTexCol(Hex(0x2F3438)); sBtn.normal.textColor = TextPri;
            sBtn.hover.background = MakeTexCol(Hex(0x3A4046)); sBtn.hover.textColor = TextPri;
            sBtn.active.background = MakeTexCol(Hex(0x25292D)); sBtn.active.textColor = TextPri;

            sBtnDisabled = new GUIStyle(sBtn);
            sBtnDisabled.normal.background = MakeTexCol(Hex(0x26282B));
            sBtnDisabled.normal.textColor = Hex(0x5A5D61);
            sBtnDisabled.hover.textColor = Hex(0x5A5D61);
            sBtnDisabled.active.textColor = Hex(0x5A5D61);

            _stylesReady = true;
        }

        private static GUIStyle MakeStateStyle(Color c)
        {
            var s = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            s.normal.textColor = c;
            return s;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static void Draw(MonoBehaviour owner)
        {
            BuildStyles();

            // Escala de densidad (§1): Screen.height/1080, clamp [0.75, 1.5].
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 1.5f);

            // §7: pantallas <1280 px → panel 340 y botones de dinero apilados.
            float baseW = Screen.width < 1280 ? 340f : PanelW;
            bool stacked = Screen.width < 1280;

            float totalH = TitleH + Pad
                           + 3 * (ToggleRowH + RowGap) + 12f
                           + (stacked ? 2 * MoneyBtnH + MoneyGap + 44f : MoneyBtnH + 44f)
                           + 28f + Pad;

            if (_panel.width <= 0)
                _panel = new Rect(PersonalMenuPlugin.CfgMenuX.Value, PersonalMenuPlugin.CfgMenuY.Value, baseW, totalH);
            _panel.width = baseW;
            _panel.height = totalH;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var logical = _panel;

            // Fondo + borde (§3)
            GUI.Box(logical, GUIContent.none, sPanel);
            DrawBorder(logical);

            // ---- Barra de título: zona de drag + botón [—] (§5) ----
            var titleRect = new Rect(logical.x, logical.y, logical.width, TitleH);
            GUI.Box(titleRect, GUIContent.none, sTitle);
            GUI.Label(new Rect(titleRect.x + 12, titleRect.y, titleRect.width - 70, TitleH), "MENÚ PERSONAL", sTitleText);
            if (GUI.Button(new Rect(logical.xMax - 42, logical.y + 5, 30, 24), "—", sBtn))
                Close();

            var ev = Event.current;
            if (ev != null && ev.type == EventType.MouseDown && titleRect.Contains(ev.mousePosition))
                _dragPending = true;
            if (ev != null && ev.type == EventType.MouseUp)
                _dragPending = false;
            if (_dragPending && ev != null && ev.type == EventType.MouseDrag)
            {
                // El delta viene en píxeles físicos; lo convertimos a espacio lógico.
                _panel.x += ev.delta.x / scale;
                _panel.y += ev.delta.y / scale;
                ev.Use();
            }

            float y = logical.y + TitleH + Pad;
            float rowX = logical.x + Pad;
            float rowW = logical.width - Pad * 2;

            // ---- Toggles (§4.1–§4.3) con estado literal ON/OFF (§6) ----
            y = ToggleRow(rowX, y, rowW, "No recibir daño", PersonalMenuPlugin.NoRecibirDanio, v => PersonalMenuPlugin.NoRecibirDanio = v);
            y = Separator(rowX, y, rowW);
            y = ToggleRow(rowX, y, rowW, "Resistencia infinita", PersonalMenuPlugin.ResistenciaInfinita, v => PersonalMenuPlugin.ResistenciaInfinita = v);
            y = Separator(rowX, y, rowW);
            y = ToggleRow(rowX, y, rowW, "Sonido de avisos", PersonalMenuPlugin.SonidoAvisos, v => PersonalMenuPlugin.SonidoAvisos = v);
            y += 12f;

            // ---- Dinero (§4.4): host leído EN CADA DRAW → cambio host→cliente sin reinicio ----
            bool isHost = PersonalMenuPlugin.EsHost;
            bool busy = Time.realtimeSinceStartup < _busyUntil;

            string status;
            GUIStyle statusStyle;
            if (busy) { status = "Aplicando…"; statusStyle = sSec; }
            else if (isHost) { status = "Partida propia: dinero disponible"; statusStyle = sOk; }
            else { status = "AVISO: Dinero bloqueado: solo disponible cuando eres el host de la sesión"; statusStyle = sAmber; }

            var btnStyle = (isHost && !busy) ? sBtn : sBtnDisabled;
            var l100 = busy ? "… 100" : "▲ Añadir 100";
            var l1000 = busy ? "… 1000" : "▲ Añadir 1000";

            // GUI.enabled desactiva de verdad los clicks: como cliente nunca disparan (spec §4.4).
            GUI.enabled = isHost && !busy;
            if (stacked)
            {
                float bw = MoneyBtnW + 40;
                if (GUI.Button(new Rect(rowX, y, bw, MoneyBtnH), l100, btnStyle)) TriggerMoney(100);
                if (GUI.Button(new Rect(rowX, y + MoneyBtnH + MoneyGap, bw, MoneyBtnH), l1000, btnStyle)) TriggerMoney(1000);
                y += 2 * MoneyBtnH + MoneyGap;
            }
            else
            {
                float bw = (rowW - MoneyGap) / 2f;
                if (GUI.Button(new Rect(rowX, y, bw, MoneyBtnH), l100, btnStyle)) TriggerMoney(100);
                if (GUI.Button(new Rect(rowX + bw + MoneyGap, y, bw, MoneyBtnH), l1000, btnStyle)) TriggerMoney(1000);
            }
            GUI.enabled = true;
            y += MoneyBtnH + 4f;

            GUI.Label(new Rect(rowX, y, rowW, 40f), status, statusStyle);
            y += 40f;

            // Toast de resultado (1.5 s, §4.4)
            if (!busy && Time.realtimeSinceStartup < _toastUntil && !string.IsNullOrEmpty(_toastMsg))
                GUI.Label(new Rect(rowX, y, rowW, 18f), _toastMsg, _toastOk ? sOk : sError);
            y += 28f;

            // Pie (§2): atajo + estado host.
            GUI.Label(new Rect(rowX, y, rowW, 18f),
                $"{PersonalMenuPlugin.HotkeyKey} cerrar · host: {(isHost ? "sí" : "no")}", sSec);

            GUI.matrix = Matrix4x4.identity;
        }

        private static float ToggleRow(float x, float y, float w, string label, bool value, Action<bool> setValue)
        {
            bool now = GUI.Toggle(new Rect(x, y, w - 70, ToggleRowH), value, "  " + label, sRow);
            if (now != value) setValue(now);
            GUI.Label(new Rect(x + w - 70, y, 60, ToggleRowH), value ? "ON" : "OFF", value ? sStateOn : sStateOff);
            return y + ToggleRowH + RowGap;
        }

        private static float Separator(float x, float y, float w)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = SepColor;
            GUI.DrawTexture(new Rect(x, y - 3f, w, 1f), _white);
            GUI.contentColor = prev;
            return y + 3f;
        }

        private static void TriggerMoney(int cantidad)
        {
            // Estado Busy breve: AnadirDinero es síncrona por reflexión; el Busy cubre el draw del botón.
            _busyUntil = Time.realtimeSinceStartup + 0.15f;
            bool ok = PersonalMenuPlugin.AnadirDinero(cantidad);
            _toastMsg = PersonalMenuPlugin.UltimoAvisoDinero;
            _toastOk = ok;
            _toastUntil = Time.realtimeSinceStartup + 1.5f;
        }

        private static void Close()
        {
            PersonalMenuPlugin.CfgMenuX.Value = _panel.x;   // recuerda última posición (§1)
            PersonalMenuPlugin.CfgMenuY.Value = _panel.y;
            PersonalMenuPlugin.MenuVisible = false;
        }

        private static void DrawBorder(Rect r)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = Border;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), _white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), _white);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height), _white);
            GUI.contentColor = prev;
        }
    }
}
