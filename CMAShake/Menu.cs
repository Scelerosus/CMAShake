using System;
using System.Collections.Generic;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;
using BepInEx.Configuration;
using UnityEngine;

namespace CMAShake
{
    // The settings window: pick the bikini top and bottom from the installed clothing, change its colour while she
    // wears it, and tune the twerk and the ride. Everything it changes is the same setting as in the config file,
    // which is written when a slider is let go and when the window is closed.
    //
    // It is drawn with the game's immediate-mode GUI, so every look here (rounded panels, switches, sliders with a
    // filled track, round colour chips) is made from textures drawn in code and styles built from them.
    internal static class Menu
    {
        private sealed class Entry
        {
            public ClothingItemMap map;
            public string id;
            public string name;
            public string lower;
            public ClothingItemMap.Type type;
        }

        private sealed class Styles
        {
            public GUIStyle panel, inner, card, seg, title, badge, h2, label, dim, value, status, statusOn, keycap, placeholder;
            public GUIStyle button, primary, ghost, close, tab, tabOn, row, rowSel, field, switchText, chip, preview;
            public GUIStyle sliderTrack, sliderThumb, scrollTrack, scrollThumb;
        }

        private const float Width = 650f;
        private const float Pad = 18f;
        private const float ThumbSize = 22f;

        private static readonly string[] Tabs = { "Bikini", "Twerk", "Ride", "Profiles" };

        private static readonly Color Accent = Hex("#FF4D9D");
        private static readonly Color AccentDark = Hex("#D81B60");
        private static readonly Color TextColor = Hex("#ECECF5");
        private static readonly Color DimColor = Hex("#9494AA");
        private static readonly Color Good = Hex("#3DDC97");

        private static readonly Color[] Presets =
        {
            Hex("#D81B60"), Hex("#E53935"), Hex("#FB8C00"), Hex("#FDD835"), Hex("#9CCC24"), Hex("#43A047"),
            Hex("#00ACC1"), Hex("#29B6F6"), Hex("#1E88E5"), Hex("#8E24AA"), Hex("#FFFFFF"), Hex("#212121")
        };

        // Textures, drawn in code.
        private static Texture2D tPanel, tCard, tSeg, tBtn, tBtnHover, tBtnDown, tPrimary, tPrimaryHover, tPrimaryDown;
        private static Texture2D tGhostHover, tTabOn, tField, tFieldFocus, tRowHover, tRowSel, tKeycap, tBadge;
        private static Texture2D tSwitchOff, tSwitchOn, tCircle, tRing, tTrack, tThumb, tLine, tPreview;
        private static Texture2D tScrollTrack, tScrollThumb, tWhitePill;

        private static Styles s_st;
        private static GUISkin s_skin;

        private static bool s_open;
        private static bool s_typing;
        private static bool s_failed;
        private static bool s_dirty;
        private static float s_openedAt;
        private static Vector2 s_pos = new Vector2(24f, 24f);
        private static bool s_drag;
        private static Vector2 s_dragOffset;
        private static int s_tab;
        private static Vector2 s_scroll;
        private static Vector2 s_listScroll;

        private static List<Entry> s_entries = new List<Entry>();
        private static List<Entry> s_shown = new List<Entry>();
        private static string s_shownKey = "";
        private static bool s_pickBottom;
        private static bool s_showAll;
        private static string s_search = "";
        private static string s_hex = "";
        private static string s_topKey, s_bottomKey;
        private static Entry s_top, s_bottom;
        private static int s_version;

        // True while a text box of the window has the keyboard, so typing in it does not trigger the hotkeys.
        internal static bool TypingText => s_open && s_typing;

        internal static bool IsOpen => s_open;

        internal static void Toggle(ShakeController host)
        {
            SetOpen(host, !s_open);
        }

        // Opening shows the mouse pointer and holds the game's own input; closing gives both back.
        private static void SetOpen(ShakeController host, bool open)
        {
            if (open == s_open)
            {
                return;
            }
            s_open = open;
            s_typing = false;
            s_drag = false;
            if (open)
            {
                s_failed = false;
                s_openedAt = Time.unscaledTime;
                Refresh();
            }
            else
            {
                SaveIfDirty();
            }
            host.SetMenuCursor(open);
        }

        // ---- helpers ----

        private static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color c);
            return c;
        }

        private static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        private static Color CurrentColor()
        {
            return ColorUtility.TryParseHtmlString(Plugin.BikiniColor.Value, out Color c) ? c : Presets[0];
        }

        private static void MarkDirty()
        {
            s_dirty = true;
        }

        private static void SaveIfDirty()
        {
            if (!s_dirty)
            {
                return;
            }
            s_dirty = false;
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Could not save the settings: " + ex.Message);
            }
        }

        // ---- clothing list ----

        // Reads the clothing items the game has loaded. They are only there once a game is running.
        private static void Refresh()
        {
            var list = new List<Entry>();
            var seen = new HashSet<string>();
            foreach (ClothingItemMap m in Bikini.LoadMaps())
            {
                if (m == null || string.IsNullOrEmpty(m.id) || m.sex == ClothingItemMap.Sex.male || !seen.Add(m.id))
                {
                    continue;
                }
                string name = !string.IsNullOrWhiteSpace(m.displayId) ? m.displayId : m.name;
                list.Add(new Entry { map = m, id = m.id, name = name, lower = (name + " " + m.id + " " + m.name).ToLowerInvariant(), type = m.type });
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            s_entries = list;
            s_version++;
            s_shownKey = "";
            s_topKey = null;
            s_bottomKey = null;
            Plugin.ModLog.LogInfo($"Menu: {list.Count} clothing item(s) available for the bikini.");
        }

        // What belongs in a bikini list: swimwear and lingerie, found by the kind the game gives the item and by its
        // name, minus anything that is plainly another sort of clothing (a dress that is typed as swimwear, a shoe...).
        private static readonly string[] SwimWords = { "bikini", "bra", "bralette", "bandeau", "thong", "string", "tanga", "panty", "panties", "brief", "lingerie", "swim", "monokini", "microkini" };
        private static readonly string[] TopWords = { "bra", "top", "bandeau", "bikini", "breast", "bralette" };
        private static readonly string[] BottomWords = { "bot", "bottom", "panty", "panties", "brief", "thong", "string", "tanga", "gstring", "crotch" };
        private static readonly string[] OtherClothes =
        {
            "dress", "skirt", "gown", "shirt", "jacket", "coat", "pants", "jeans", "shorts", "hoodie", "sweater", "blouse", "romper", "jumpsuit",
            "catsuit", "shoe", "boot", "heel", "sock", "stocking", "glove", "hat", "glasses", "brace", "brand", "necklace", "earring", "jewel", "belt"
        };

        private static bool HasAny(string text, string[] words)
        {
            foreach (string w in words)
            {
                if (text.Contains(w))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSwimwear(Entry e)
        {
            if (HasAny(e.lower, OtherClothes))
            {
                return false;
            }
            return e.type == ClothingItemMap.Type.swimsuit || e.type == ClothingItemMap.Type.upperBodyUnderwear
                || e.type == ClothingItemMap.Type.lowerBodyUnderwear || HasAny(e.lower, SwimWords);
        }

        private static bool IsTop(Entry e)
        {
            if (e.type == ClothingItemMap.Type.upperBodyUnderwear || e.type == ClothingItemMap.Type.swimsuit)
            {
                return true;
            }
            bool bottom = HasAny(e.lower, BottomWords);
            return e.type != ClothingItemMap.Type.lowerBodyUnderwear && (!bottom || HasAny(e.lower, TopWords));
        }

        private static bool IsBottom(Entry e)
        {
            if (e.type == ClothingItemMap.Type.lowerBodyUnderwear || e.type == ClothingItemMap.Type.swimsuit)
            {
                return true;
            }
            bool top = HasAny(e.lower, TopWords);
            return e.type != ClothingItemMap.Type.upperBodyUnderwear && (!top || HasAny(e.lower, BottomWords));
        }

        private static void RebuildShown()
        {
            string key = $"{s_version}|{s_pickBottom}|{s_showAll}|{s_search}";
            if (key == s_shownKey)
            {
                return;
            }
            s_shownKey = key;
            s_shown = new List<Entry>();
            string q = (s_search ?? "").Trim().ToLowerInvariant();
            foreach (Entry e in s_entries)
            {
                if (!s_showAll && !(IsSwimwear(e) && (s_pickBottom ? IsBottom(e) : IsTop(e))))
                {
                    continue;
                }
                if (q.Length > 0 && !e.lower.Contains(q))
                {
                    continue;
                }
                s_shown.Add(e);
            }
        }

        // The items the Top and Bottom settings stand for right now, worked out again only when a setting changes.
        private static void ResolvePicks()
        {
            if (s_topKey != Plugin.BikiniTop.Value)
            {
                s_topKey = Plugin.BikiniTop.Value;
                s_top = FindEntry(s_topKey, false);
            }
            if (s_bottomKey != Plugin.BikiniBottom.Value)
            {
                s_bottomKey = Plugin.BikiniBottom.Value;
                s_bottom = FindEntry(s_bottomKey, true);
            }
        }

        private static Entry FindEntry(string setting, bool bottom)
        {
            var maps = new List<ClothingItemMap>();
            foreach (Entry e in s_entries)
            {
                maps.Add(e.map);
            }
            ClothingItemMap hit = Bikini.Resolve(maps, setting, bottom);
            foreach (Entry e in s_entries)
            {
                if (hit != null && e.map == hit)
                {
                    return e;
                }
            }
            return null;
        }

        // ---- textures ----

        private static Texture2D Make(int w, int h, Func<float, float, Color> shade)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    t.SetPixel(x, y, shade(x + 0.5f, y + 0.5f));
                }
            }
            t.Apply();
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // Distance from a point to the edge of a rounded rectangle (negative inside).
        private static float RoundDist(float px, float py, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(px) - (hw - r);
            float qy = Mathf.Abs(py) - (hh - r);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Texture2D Rounded(int w, int h, float r, Color fill, Color border, float borderWidth)
        {
            return Make(w, h, (x, y) =>
            {
                float d = RoundDist(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, r);
                float outer = Mathf.Clamp01(0.5f - d);
                float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                Color c = borderWidth > 0f ? Color.Lerp(border, fill, inner) : fill;
                c.a *= outer;
                return c;
            });
        }

        private static Texture2D Solid(Color c)
        {
            return Make(2, 2, (x, y) => c);
        }

        private static Texture2D Switch(bool on)
        {
            const int w = 48;
            const int h = 26;
            Color track = on ? Accent : Hex("#3B3B50");
            float knobX = on ? w - h * 0.5f : h * 0.5f;
            float knobR = h * 0.5f - 3.5f;
            return Make(w, h, (x, y) =>
            {
                float d = RoundDist(x - w * 0.5f, y - h * 0.5f, w * 0.5f, h * 0.5f, h * 0.5f);
                float kd = Mathf.Sqrt((x - knobX) * (x - knobX) + (y - h * 0.5f) * (y - h * 0.5f)) - knobR;
                Color c = Color.Lerp(track, Color.white, Mathf.Clamp01(0.5f - kd));
                c.a = Mathf.Clamp01(0.5f - d);
                return c;
            });
        }

        private static void MakeTextures()
        {
            if (tPanel != null)
            {
                return;
            }
            Color none = new Color(0f, 0f, 0f, 0f);
            Color line = Hex("#2A2A3E");
            tPanel = Rounded(48, 48, 16f, WithAlpha(Hex("#12121A"), 0.97f), WithAlpha(line, 1f), 1.5f);
            tCard = Rounded(32, 32, 11f, Hex("#1A1A26"), Hex("#25253A"), 1f);
            tSeg = Rounded(32, 32, 11f, Hex("#0F0F16"), Hex("#25253A"), 1f);
            tBtn = Rounded(32, 32, 9f, Hex("#2B2B3E"), none, 0f);
            tBtnHover = Rounded(32, 32, 9f, Hex("#37374F"), none, 0f);
            tBtnDown = Rounded(32, 32, 9f, Hex("#444462"), none, 0f);
            tPrimary = Rounded(32, 32, 9f, AccentDark, none, 0f);
            tPrimaryHover = Rounded(32, 32, 9f, Hex("#EC2A78"), none, 0f);
            tPrimaryDown = Rounded(32, 32, 9f, Hex("#B3134F"), none, 0f);
            tGhostHover = Rounded(32, 32, 9f, Hex("#26263A"), none, 0f);
            tTabOn = Rounded(32, 32, 9f, AccentDark, none, 0f);
            tField = Rounded(32, 32, 9f, Hex("#0E0E15"), Hex("#34344C"), 1.5f);
            tFieldFocus = Rounded(32, 32, 9f, Hex("#0E0E15"), Accent, 1.5f);
            tRowHover = Rounded(32, 32, 9f, Hex("#242436"), none, 0f);
            tRowSel = Rounded(32, 32, 9f, WithAlpha(Accent, 0.16f), WithAlpha(Accent, 0.75f), 1.5f);
            tKeycap = Rounded(32, 32, 7f, Hex("#26263A"), Hex("#3A3A54"), 1f);
            tBadge = Rounded(32, 32, 10f, WithAlpha(Accent, 0.18f), WithAlpha(Accent, 0.6f), 1f);
            tSwitchOff = Switch(false);
            tSwitchOn = Switch(true);
            tCircle = Rounded(32, 32, 16f, Color.white, none, 0f);
            tRing = Rounded(40, 40, 20f, none, Accent, 2.5f);
            tTrack = Rounded(16, 8, 4f, Hex("#2F2F46"), none, 0f);
            tWhitePill = Rounded(16, 8, 4f, Color.white, none, 0f);
            tThumb = Rounded(44, 44, 22f, Color.white, Accent, 5f);
            tLine = Solid(WithAlpha(line, 1f));
            tPreview = Rounded(32, 32, 9f, Color.white, WithAlpha(line, 1f), 1.5f);
            tScrollTrack = Rounded(12, 12, 6f, Hex("#1C1C28"), none, 0f);
            tScrollThumb = Rounded(12, 12, 6f, Hex("#4A4A66"), none, 0f);
        }

        // ---- styles ----

        private static void Tint(GUIStyle s, Color c)
        {
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.focused.textColor = c;
            s.onNormal.textColor = c;
            s.onHover.textColor = c;
            s.onActive.textColor = c;
            s.onFocused.textColor = c;
        }

        private static void Back(GUIStyle s, Texture2D normal, Texture2D hover, Texture2D active, int border)
        {
            s.normal.background = normal;
            s.hover.background = hover ?? normal;
            s.active.background = active ?? hover ?? normal;
            s.focused.background = normal;
            s.onNormal.background = normal;
            s.onHover.background = hover ?? normal;
            s.onActive.background = active ?? hover ?? normal;
            s.onFocused.background = normal;
            s.border = new RectOffset(border, border, border, border);
        }

        private static RectOffset Pad4(int l, int r, int t, int b)
        {
            return new RectOffset(l, r, t, b);
        }

        private static void MakeStyles()
        {
            MakeTextures();
            if (s_st != null)
            {
                return;
            }
            GUISkin skin = GUI.skin;
            var st = new Styles();

            st.panel = new GUIStyle();
            Back(st.panel, tPanel, null, null, 18);

            st.inner = new GUIStyle { padding = Pad4((int)Pad, (int)Pad, 16, 14) };

            st.card = new GUIStyle { padding = Pad4(14, 14, 12, 12), margin = Pad4(0, 0, 0, 10) };
            Back(st.card, tCard, null, null, 13);

            st.seg = new GUIStyle { padding = Pad4(4, 4, 4, 4), margin = Pad4(0, 0, 6, 10) };
            Back(st.seg, tSeg, null, null, 13);

            st.title = new GUIStyle(skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            Tint(st.title, Color.white);

            st.badge = new GUIStyle(skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = Pad4(9, 9, 2, 3) };
            Tint(st.badge, Accent);
            Back(st.badge, tBadge, null, null, 11);

            st.h2 = new GUIStyle(skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, margin = Pad4(0, 0, 0, 6) };
            Tint(st.h2, Accent);

            st.label = new GUIStyle(skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            Tint(st.label, TextColor);

            st.dim = new GUIStyle(st.label) { fontSize = 13, wordWrap = true };
            Tint(st.dim, DimColor);

            st.value = new GUIStyle(st.label) { fontSize = 14, alignment = TextAnchor.MiddleRight };
            Tint(st.value, TextColor);

            st.status = new GUIStyle(st.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            Tint(st.status, DimColor);
            st.statusOn = new GUIStyle(st.status);
            Tint(st.statusOn, Good);

            st.keycap = new GUIStyle(skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = Pad4(8, 8, 2, 3), margin = Pad4(0, 4, 3, 3) };
            Tint(st.keycap, TextColor);
            Back(st.keycap, tKeycap, null, null, 8);

            st.placeholder = new GUIStyle(st.label) { fontSize = 15 };
            Tint(st.placeholder, WithAlpha(DimColor, 0.8f));

            st.button = new GUIStyle(skin.button) { fontSize = 15, alignment = TextAnchor.MiddleCenter, padding = Pad4(14, 14, 7, 8), margin = Pad4(0, 0, 2, 2) };
            Tint(st.button, TextColor);
            Back(st.button, tBtn, tBtnHover, tBtnDown, 11);

            st.primary = new GUIStyle(st.button) { fontStyle = FontStyle.Bold };
            Tint(st.primary, Color.white);
            Back(st.primary, tPrimary, tPrimaryHover, tPrimaryDown, 11);

            st.ghost = new GUIStyle(st.button);
            Tint(st.ghost, DimColor);
            Back(st.ghost, null, tGhostHover, tBtnDown, 11);
            st.ghost.normal.background = null;
            st.ghost.focused.background = null;
            st.ghost.onNormal.background = null;
            st.ghost.onFocused.background = null;

            st.close = new GUIStyle(st.ghost) { fontSize = 16, fontStyle = FontStyle.Bold, padding = Pad4(0, 0, 0, 2) };

            st.tab = new GUIStyle(st.ghost) { fontSize = 16, fontStyle = FontStyle.Bold, margin = Pad4(0, 0, 0, 0) };
            st.tabOn = new GUIStyle(st.button) { fontSize = 16, fontStyle = FontStyle.Bold, margin = Pad4(0, 0, 0, 0) };
            Tint(st.tabOn, Color.white);
            Back(st.tabOn, tTabOn, tTabOn, tTabOn, 11);

            st.row = new GUIStyle(skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = false, clipping = TextClipping.Clip, padding = Pad4(14, 10, 4, 4), margin = Pad4(0, 0, 0, 3) };
            Tint(st.row, TextColor);
            Back(st.row, null, tRowHover, tRowHover, 11);
            st.row.normal.background = null;
            st.row.focused.background = null;
            st.row.onNormal.background = null;
            st.row.onFocused.background = null;
            st.rowSel = new GUIStyle(st.row);
            Back(st.rowSel, tRowSel, tRowSel, tRowSel, 11);

            st.field = new GUIStyle(skin.textField) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = Pad4(12, 12, 7, 8), margin = Pad4(0, 0, 2, 2), clipping = TextClipping.Clip };
            Tint(st.field, TextColor);
            Back(st.field, tField, tField, tFieldFocus, 11);
            st.field.focused.background = tFieldFocus;
            st.field.onFocused.background = tFieldFocus;
            st.field.onHover.background = tField;

            st.switchText = new GUIStyle(skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = Pad4(60, 0, 0, 1), margin = Pad4(0, 0, 3, 3) };
            Tint(st.switchText, TextColor);

            st.chip = new GUIStyle(skin.button) { margin = Pad4(0, 6, 2, 2), padding = Pad4(0, 0, 0, 0) };
            Back(st.chip, tCircle, tCircle, tCircle, 0);

            st.preview = new GUIStyle(skin.box) { margin = Pad4(8, 0, 2, 2) };
            Back(st.preview, tPreview, tPreview, tPreview, 11);

            st.sliderTrack = new GUIStyle(skin.horizontalSlider) { padding = Pad4(0, 0, 0, 0), margin = Pad4(0, 0, 0, 0), border = Pad4(0, 0, 0, 0), fixedHeight = 0f };
            st.sliderTrack.normal.background = null;
            st.sliderTrack.hover.background = null;
            st.sliderTrack.active.background = null;
            st.sliderTrack.focused.background = null;
            st.sliderThumb = new GUIStyle(skin.horizontalSliderThumb) { padding = Pad4(0, 0, 0, 0), margin = Pad4(0, 0, 0, 0), border = Pad4(0, 0, 0, 0), fixedWidth = ThumbSize, fixedHeight = ThumbSize, overflow = Pad4(0, 0, 0, 0) };
            Back(st.sliderThumb, tThumb, tThumb, tThumb, 0);

            st.scrollTrack = new GUIStyle(skin.verticalScrollbar) { fixedWidth = 8f, margin = Pad4(6, 0, 0, 0), padding = Pad4(0, 0, 0, 0) };
            Back(st.scrollTrack, tScrollTrack, tScrollTrack, tScrollTrack, 5);
            st.scrollThumb = new GUIStyle(skin.verticalScrollbarThumb) { fixedWidth = 8f, padding = Pad4(0, 0, 0, 0), margin = Pad4(0, 0, 0, 0) };
            Back(st.scrollThumb, tScrollThumb, tScrollThumb, tScrollThumb, 5);

            s_st = st;

            // A copy of the skin with the scroll bars restyled, used only while this window is drawn.
            try
            {
                s_skin = UnityEngine.Object.Instantiate(skin);
                s_skin.verticalScrollbar = st.scrollTrack;
                s_skin.verticalScrollbarThumb = st.scrollThumb;
                s_skin.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception ex)
            {
                s_skin = null;
                Plugin.ModLog.LogWarning("Settings window: could not restyle the scroll bars: " + ex.Message);
            }
        }

        // ---- window ----

        internal static void Draw(ShakeController host)
        {
            if (!s_open)
            {
                s_typing = false;
                return;
            }
            Matrix4x4 keepMatrix = GUI.matrix;
            Color keepColor = GUI.color;
            GUISkin keepSkin = GUI.skin;
            try
            {
                MakeStyles();
                if (s_skin != null)
                {
                    GUI.skin = s_skin;
                }
                DrawWindow(host);
            }
            catch (Exception ex)
            {
                if (!s_failed)
                {
                    s_failed = true;
                    Plugin.ModLog.LogWarning("Settings window failed and was closed: " + ex);
                }
                s_typing = false;
                SetOpen(host, false);
            }
            finally
            {
                GUI.skin = keepSkin;
                GUI.color = keepColor;
                GUI.matrix = keepMatrix;
            }
        }

        private static void DrawWindow(ShakeController host)
        {
            Styles st = s_st;
            Event ev = Event.current;

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            float h = Mathf.Min(sh - 40f, 920f);
            s_pos.x = Mathf.Clamp(s_pos.x, 0f, Mathf.Max(0f, sw - 140f));
            s_pos.y = Mathf.Clamp(s_pos.y, 0f, Mathf.Max(0f, sh - 90f));

            // Fades and slides in when opened.
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - s_openedAt) / 0.18f));
            GUI.color = new Color(1f, 1f, 1f, appear);
            Rect r = new Rect(s_pos.x, s_pos.y + (1f - appear) * 14f, Width, h);

            // Drag the window by its header.
            Rect header = new Rect(r.x, r.y, r.width - 64f, 66f);
            if (ev != null)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && header.Contains(ev.mousePosition))
                {
                    s_drag = true;
                    s_dragOffset = ev.mousePosition - s_pos;
                }
                else if (s_drag && ev.type == EventType.MouseDrag)
                {
                    s_pos = ev.mousePosition - s_dragOffset;
                }
                else if (ev.rawType == EventType.MouseUp)
                {
                    s_drag = false;
                }
            }

            GUI.Box(r, "", st.panel);
            GUILayout.BeginArea(r);
            GUILayout.BeginVertical(st.inner);

            GUILayout.BeginHorizontal();
            GUILayout.Label("CMA Shake", st.title);
            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            GUILayout.Space(11f);
            GUILayout.Label("v" + Plugin.PluginVersion, st.badge);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", st.close, GUILayout.Width(32f), GUILayout.Height(32f)))
            {
                SetOpen(host, false);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Bikini, twerk and ride settings", st.dim);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal(st.seg);
            float tabWidth = (Width - 2f * Pad - 8f) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                if (GUILayout.Button(Tabs[i], i == s_tab ? st.tabOn : st.tab, GUILayout.Width(tabWidth - 0.5f), GUILayout.Height(34f)))
                {
                    s_tab = i;
                }
            }
            GUILayout.EndHorizontal();

            s_scroll = GUILayout.BeginScrollView(s_scroll, GUILayout.ExpandHeight(true));
            if (s_tab == 0)
            {
                DrawBikini(host, st);
            }
            else if (s_tab == 1)
            {
                DrawTwerk(host, st);
            }
            else if (s_tab == 2)
            {
                DrawRide(host, st);
            }
            else
            {
                DrawProfiles(host, st);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            Hint(st, Plugin.MenuKey.Value.ToString(), "menu");
            Hint(st, Plugin.ToggleKey.Value.ToString(), "twerk");
            Hint(st, Plugin.RideKey.Value.ToString(), "ride");
            Hint(st, Plugin.BikiniKey.Value.ToString(), "bikini");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();

            string focus = GUI.GetNameOfFocusedControl();
            s_typing = focus == "search" || focus == "hex" || focus == "profile";

            if (ev != null && ev.rawType == EventType.MouseUp)
            {
                SaveIfDirty();
            }
        }

        private static void Hint(Styles st, string key, string what)
        {
            GUILayout.Label(key, st.keycap);
            GUILayout.Label(what, st.dim, GUILayout.Width(52f));
        }

        // ---- widgets ----

        private static void CardBegin(Styles st, string title)
        {
            GUILayout.BeginVertical(st.card);
            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(title.ToUpperInvariant(), st.h2);
            }
        }

        private static void CardEnd()
        {
            GUILayout.EndVertical();
        }

        private static bool Repainting()
        {
            Event e = Event.current;
            return e != null && e.type == EventType.Repaint;
        }

        // A slider with a filled track and a round handle. Returns the new value.
        private static float SliderRow(Styles st, string label, float value, float min, float max, string format, Color fill)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, st.label, GUILayout.Width(236f), GUILayout.Height(30f));
            GUILayout.BeginVertical();
            GUILayout.Space(4f);
            Rect r = GUILayoutUtility.GetRect(60f, ThumbSize, GUILayout.ExpandWidth(true), GUILayout.Height(ThumbSize));
            GUILayout.EndVertical();

            float t = Mathf.InverseLerp(min, max, value);
            if (Repainting())
            {
                Color keep = GUI.color;
                float cy = r.y + r.height * 0.5f;
                GUI.DrawTexture(new Rect(r.x, cy - 4f, r.width, 8f), tTrack);
                float centre = r.x + ThumbSize * 0.5f + t * (r.width - ThumbSize);
                GUI.color = new Color(fill.r, fill.g, fill.b, keep.a);
                GUI.DrawTexture(new Rect(r.x, cy - 4f, Mathf.Max(8f, centre - r.x), 8f), tWhitePill);
                GUI.color = keep;
            }
            float nv = GUI.HorizontalSlider(r, value, min, max, st.sliderTrack, st.sliderThumb);
            GUILayout.Label(nv.ToString(format), st.value, GUILayout.Width(72f), GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            return nv;
        }

        private static void Slider(Styles st, string label, ConfigEntry<float> e, float min, float max, string format)
        {
            float v = e.Value;
            float nv = SliderRow(st, label, v, min, max, format, Accent);
            if (Mathf.Abs(nv - v) > 1e-6f)
            {
                e.Value = nv;
                MarkDirty();
            }
        }

        // An on/off switch with its label. Returns the new value.
        private static bool SwitchRow(Styles st, string label, bool value)
        {
            Rect r = GUILayoutUtility.GetRect(60f, 30f, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
            bool nv = GUI.Toggle(r, value, label, st.switchText);
            if (Repainting())
            {
                GUI.DrawTexture(new Rect(r.x, r.y + 2f, 48f, 26f), value ? tSwitchOn : tSwitchOff);
            }
            return nv;
        }

        private static void Check(Styles st, string label, ConfigEntry<bool> e)
        {
            bool nv = SwitchRow(st, label, e.Value);
            if (nv != e.Value)
            {
                e.Value = nv;
                MarkDirty();
            }
        }

        // A row of choices, one of them selected. Returns the index chosen.
        private static int Choice(Styles st, string[] names, int selected, float itemWidth)
        {
            int result = selected;
            GUILayout.BeginHorizontal(st.seg, GUILayout.ExpandWidth(false));
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button(names[i], i == selected ? st.tabOn : st.tab, GUILayout.Width(itemWidth), GUILayout.Height(30f)))
                {
                    result = i;
                }
            }
            GUILayout.EndHorizontal();
            return result;
        }

        private static void Gap(float height)
        {
            GUILayout.Space(height);
        }

        private static void ResetButton(Styles st, string text, Action reset)
        {
            if (GUILayout.Button(text, st.ghost, GUILayout.Height(32f)))
            {
                reset();
            }
        }

        private static void ResetAll(params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase e in entries)
            {
                e.BoxedValue = e.DefaultValue;
            }
            MarkDirty();
        }

        // ---- Bikini ----

        private static void DrawBikini(ShakeController host, Styles st)
        {
            ResolvePicks();

            bool on = Bikini.AnyOn();
            CardBegin(st, null);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Bikini", st.dim);
            GUILayout.Label(on ? "Worn" : "Not worn", on ? st.statusOn : st.status);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(on ? "Take it off" : "Put it on", on ? st.button : st.primary, GUILayout.Width(150f), GUILayout.Height(40f)))
            {
                host.ToggleBikini();
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, "Pieces");
            PickRow(host, st, "Top", s_top, Plugin.BikiniTop);
            PickRow(host, st, "Bottom", s_bottom, Plugin.BikiniBottom);
            if (s_entries.Count > 0)
            {
                bool noTop = s_top == null;
                bool noBottom = s_bottom == null;
                if (noTop && noBottom)
                {
                    GUILayout.Label("Pick a top and a bottom: only the clothes the bikini replaces are taken off.", st.dim);
                }
                else if (noTop || noBottom)
                {
                    GUILayout.Label(noTop ? "No top picked: what she wears on top stays on." : "No bottom picked: what she wears below stays on.", st.dim);
                }
            }
            Gap(6f);

            GUILayout.BeginHorizontal();
            int pick = Choice(st, new[] { "Top", "Bottom" }, s_pickBottom ? 1 : 0, 84f);
            s_pickBottom = pick == 1;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh list", st.ghost, GUILayout.Height(32f)))
            {
                Refresh();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("search");
            s_search = GUILayout.TextField(s_search, st.field);
            if (s_search.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "search")
            {
                Rect fr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(fr.x + 12f, fr.y, fr.width - 12f, fr.height), "Search clothing...", st.placeholder);
            }
            GUILayout.Space(10f);
            s_showAll = SwitchWide(st, "Other clothes", s_showAll, 150f);
            GUILayout.EndHorizontal();

            RebuildShown();
            if (s_entries.Count == 0)
            {
                GUILayout.Label("No clothing is loaded yet. Start or load a game, then press Refresh list.", st.dim);
            }
            else if (s_shown.Count == 0)
            {
                GUILayout.Label(s_showAll || s_search.Length > 0 ? "Nothing matches." : "No swimwear or lingerie of this kind is installed. Turn on Other clothes to see the rest.", st.dim);
            }

            s_listScroll = GUILayout.BeginScrollView(s_listScroll, GUILayout.Height(236f));
            Entry chosen = s_pickBottom ? s_bottom : s_top;
            int shown = 0;
            foreach (Entry e in s_shown)
            {
                if (shown++ >= 250)
                {
                    GUILayout.Label("More items: type in the search box to narrow the list.", st.dim);
                    break;
                }
                bool sel = chosen != null && chosen.id == e.id;
                string text = e.name + "\n<size=12><color=#8D8DA3>" + e.id + "</color></size>";
                if (GUILayout.Button(text, sel ? st.rowSel : st.row, GUILayout.Height(48f)))
                {
                    Pick(host, e);
                }
            }
            GUILayout.EndScrollView();
            CardEnd();

            CardBegin(st, "Colour");
            Color cur = CurrentColor();
            GUILayout.BeginHorizontal();
            foreach (Color p in Presets)
            {
                Color bg = GUI.backgroundColor;
                GUI.backgroundColor = p;
                bool clicked = GUILayout.Button("", st.chip, GUILayout.Width(30f), GUILayout.Height(30f));
                GUI.backgroundColor = bg;
                if (Repainting() && SameColor(p, cur))
                {
                    Rect cr = GUILayoutUtility.GetLastRect();
                    GUI.DrawTexture(new Rect(cr.x - 5f, cr.y - 5f, cr.width + 10f, cr.height + 10f), tRing);
                }
                if (clicked)
                {
                    SetColor(p);
                    cur = p;
                }
            }
            GUILayout.EndHorizontal();
            Gap(6f);

            float r = SliderRow(st, "Red", cur.r, 0f, 1f, "0.00", Hex("#FF5A5A"));
            float g = SliderRow(st, "Green", cur.g, 0f, 1f, "0.00", Hex("#4CD97B"));
            float b = SliderRow(st, "Blue", cur.b, 0f, 1f, "0.00", Hex("#5AA9FF"));
            if (Mathf.Abs(r - cur.r) > 0.0005f || Mathf.Abs(g - cur.g) > 0.0005f || Mathf.Abs(b - cur.b) > 0.0005f)
            {
                SetColor(new Color(r, g, b, 1f));
                cur = CurrentColor();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Hex", st.label, GUILayout.Width(236f), GUILayout.Height(32f));
            if (GUI.GetNameOfFocusedControl() != "hex")
            {
                s_hex = "#" + ColorUtility.ToHtmlStringRGB(cur);
            }
            GUI.SetNextControlName("hex");
            string typed = GUILayout.TextField(s_hex, 7, st.field, GUILayout.Width(120f));
            if (typed != s_hex)
            {
                s_hex = typed;
                if (typed.Length == 7 && ColorUtility.TryParseHtmlString(typed, out Color parsed))
                {
                    SetColor(parsed);
                    cur = parsed;
                }
            }
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = cur;
            GUILayout.Box("", st.preview, GUILayout.Width(70f), GUILayout.Height(32f));
            GUI.backgroundColor = old;
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, "Options");
            bool solid = SwitchRow(st, "Plain colour (hide the item's own pattern)", Plugin.BikiniSolid.Value);
            if (solid != Plugin.BikiniSolid.Value)
            {
                Plugin.BikiniSolid.Value = solid;
                MarkDirty();
                host.ReapplyBikini();
            }
            Check(st, "Put it on automatically while twerking", Plugin.BikiniWithTwerk);
            CardEnd();

            ResetButton(st, "Reset the bikini settings", () =>
            {
                ResetAll(Plugin.BikiniTop, Plugin.BikiniBottom, Plugin.BikiniColor, Plugin.BikiniSolid, Plugin.BikiniWithTwerk);
                Bikini.Retint();
                host.ReapplyBikini();
            });
        }

        private static bool SameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        // A switch that is a fixed width, for sitting in a row next to other controls.
        private static bool SwitchWide(Styles st, string label, bool value, float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 32f, GUILayout.Width(width), GUILayout.Height(32f));
            bool nv = GUI.Toggle(r, value, label, st.switchText);
            if (Repainting())
            {
                GUI.DrawTexture(new Rect(r.x, r.y + 3f, 48f, 26f), value ? tSwitchOn : tSwitchOff);
            }
            return nv;
        }

        private static void PickRow(ShakeController host, Styles st, string name, Entry current, ConfigEntry<string> setting)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, st.dim, GUILayout.Width(64f), GUILayout.Height(30f));
            string text;
            if (string.IsNullOrWhiteSpace(setting.Value))
            {
                text = "None";
            }
            else if (current != null)
            {
                text = current.name;
            }
            else
            {
                text = "Not found: " + setting.Value;
            }
            GUILayout.Label(text, st.label, GUILayout.Height(30f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", st.ghost, GUILayout.Width(70f), GUILayout.Height(30f)))
            {
                setting.Value = "";
                MarkDirty();
                host.ReapplyBikini();
            }
            GUILayout.EndHorizontal();
        }

        private static void Pick(ShakeController host, Entry e)
        {
            (s_pickBottom ? Plugin.BikiniBottom : Plugin.BikiniTop).Value = e.id;
            MarkDirty();
            host.ReapplyBikini();
        }

        private static void SetColor(Color c)
        {
            Plugin.BikiniColor.Value = "#" + ColorUtility.ToHtmlStringRGB(c);
            MarkDirty();
            Bikini.Retint();
        }

        // ---- Twerk and Ride ----

        // Kinds of pose: choosing one sets a bundle of the settings below it. What was set before is kept as the profile "_last".
        private static void PoseRow(Styles st, Poses.Pose[] set, ConfigEntry<string> label)
        {
            GUILayout.Label("Kind of pose (sets the values below)", st.dim);
            int cur = Poses.IndexOf(set, label.Value);
            int next = Choice(st, Poses.Names(set), cur, 134f);
            if (next != cur && next >= 0)
            {
                Poses.Apply(set[next], label);
            }
            GUILayout.Space(4f);
        }

        // ---- Profiles ----

        private static string s_profileName = "";
        private static string s_profileSelected = "";
        private static string s_profileStatus = "";
        private static List<string> s_profiles = new List<string>();
        private static Vector2 s_profileScroll;
        private static int s_profilesLoadedFor = -1;

        private static string ProfileLabel(string name)
        {
            return name == "_last" ? "(settings before the last change)" : name;
        }

        private static void RefreshProfiles()
        {
            s_profiles = Profiles.List();
            s_profilesLoadedFor = s_version;
        }

        private static void DrawProfiles(ShakeController host, Styles st)
        {
            if (s_profilesLoadedFor < 0)
            {
                RefreshProfiles();
            }

            CardBegin(st, "Active profile");
            GUILayout.Label(string.IsNullOrEmpty(Plugin.ActiveProfile.Value) ? "None: the settings are as you left them." : Plugin.ActiveProfile.Value, st.status);
            if (!string.IsNullOrEmpty(s_profileStatus))
            {
                GUILayout.Label(s_profileStatus, st.dim);
            }
            CardEnd();

            CardBegin(st, "Save the current settings");
            GUILayout.Label("A profile keeps the motion, pose, rhythm, ride and bikini settings under a name, in a file of its own. Hotkeys are not part of it.", st.dim);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("profile");
            s_profileName = GUILayout.TextField(s_profileName, 40, st.field);
            GUILayout.Space(8f);
            if (GUILayout.Button("Save", st.primary, GUILayout.Width(110f)))
            {
                string clean = Profiles.Clean(s_profileName);
                if (clean.Length == 0)
                {
                    s_profileStatus = "Type a name first.";
                }
                else if (Profiles.Save(clean))
                {
                    Plugin.ActiveProfile.Value = clean;
                    MarkDirty();
                    s_profileSelected = clean;
                    s_profileStatus = "Saved as '" + clean + "'.";
                    RefreshProfiles();
                }
                else
                {
                    s_profileStatus = "Could not save it.";
                }
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, "Saved profiles");
            if (s_profiles.Count == 0)
            {
                GUILayout.Label("None yet. Save the current settings above.", st.dim);
            }
            s_profileScroll = GUILayout.BeginScrollView(s_profileScroll, GUILayout.Height(Mathf.Min(220f, 12f + 44f * Mathf.Max(1, s_profiles.Count))));
            foreach (string name in s_profiles)
            {
                bool sel = string.Equals(name, s_profileSelected, StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button(ProfileLabel(name), sel ? st.rowSel : st.row, GUILayout.Height(40f)))
                {
                    s_profileSelected = name;
                    if (name != "_last")
                    {
                        s_profileName = name;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            bool any = !string.IsNullOrEmpty(s_profileSelected);
            if (GUILayout.Button("Load", st.primary, GUILayout.Height(36f)) && any)
            {
                int applied = Profiles.Load(s_profileSelected);
                if (applied < 0)
                {
                    s_profileStatus = "That profile is gone.";
                }
                else
                {
                    if (s_profileSelected != "_last")
                    {
                        Plugin.ActiveProfile.Value = s_profileSelected;
                        MarkDirty();
                    }
                    s_profileStatus = "Loaded '" + ProfileLabel(s_profileSelected) + "' (" + applied + " settings).";
                    host.ReapplyBikini();
                    RefreshProfiles();
                }
            }
            GUILayout.Space(6f);
            if (GUILayout.Button("Overwrite with current", st.button, GUILayout.Height(36f)) && any && s_profileSelected != "_last")
            {
                s_profileStatus = Profiles.Save(s_profileSelected) ? "Overwrote '" + s_profileSelected + "' with the current settings." : "Could not save it.";
                RefreshProfiles();
            }
            GUILayout.Space(6f);
            if (GUILayout.Button("Delete", st.ghost, GUILayout.Height(36f)) && any)
            {
                s_profileStatus = Profiles.Delete(s_profileSelected) ? "Deleted '" + ProfileLabel(s_profileSelected) + "'." : "Nothing to delete.";
                s_profileSelected = "";
                RefreshProfiles();
            }
            GUILayout.EndHorizontal();
            CardEnd();
        }

        // The preset and the rhythm, shared by the twerk and the ride.
        private static void DrawStyle(Styles st)
        {
            CardBegin(st, "Style");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Preset", st.label, GUILayout.Width(236f), GUILayout.Height(34f));
            string[] presets = { "Soft", "Fast", "Mine" };
            int presetCur = Array.FindIndex(presets, p => string.Equals(p, Plugin.PresetName.Value, StringComparison.OrdinalIgnoreCase));
            int presetNext = Choice(st, presets, Mathf.Max(0, presetCur), 90f);
            if (presetNext != presetCur)
            {
                Plugin.PresetName.Value = presets[presetNext];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rhythm", st.label, GUILayout.Width(236f), GUILayout.Height(34f));
            string[] rhythms = { "Steady", "Random", "Scenario" };
            int rhythmCur = Array.FindIndex(rhythms, r => string.Equals(r, Plugin.Rhythm.Value, StringComparison.OrdinalIgnoreCase));
            int rhythmNext = Choice(st, rhythms, Mathf.Max(0, rhythmCur), 90f);
            if (rhythmNext != rhythmCur)
            {
                Plugin.Rhythm.Value = rhythms[rhythmNext];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"{Plugin.PresetKey.Value} switches the preset. Soft is slower and gentler, Fast quicker, Mine keeps your own settings as they are. Scenario runs a slow circle, bounces, a pause, a faster stretch and a side-to-side, round and round.", st.dim);
            CardEnd();
        }

        private static void DrawTwerk(ShakeController host, Styles st)
        {
            CardBegin(st, null);
            if (GUILayout.Button(host.Twerking ? "Stop the twerk" : "Start the twerk", host.Twerking ? st.button : st.primary, GUILayout.Height(40f)))
            {
                host.ToggleMode(false);
            }
            CardEnd();

            DrawStyle(st);

            CardBegin(st, "Motion");
            Slider(st, "Smoothness", Plugin.Smoothness, 0f, 1f, "0.00");
            Slider(st, "Speed (bounces/s)", Plugin.Frequency, 0.5f, 5f, "0.0");
            Slider(st, "Bounce height (m)", Plugin.BounceHeight, 0f, 0.1f, "0.000");
            Slider(st, "Pelvis rock (deg)", Plugin.TiltDegrees, 0f, 20f, "0.0");
            Slider(st, "Glute bounce (m)", Plugin.ButtAmplitude, 0f, 0.08f, "0.000");
            Check(st, "Bounce the chest too", Plugin.ShakeChest);
            Slider(st, "Chest bounce (m)", Plugin.ChestAmplitude, 0f, 0.05f, "0.000");
            CardEnd();

            CardBegin(st, "Pose");
            PoseRow(st, Poses.Twerk, Plugin.TwerkPoseType);
            Slider(st, "Lean forward (deg)", Plugin.LeanDegrees, 0f, 85f, "0");
            Slider(st, "Back arch (deg)", Plugin.Arch, 0f, 30f, "0");
            Slider(st, "Squat depth (m)", Plugin.SquatDepth, 0f, 0.5f, "0.00");
            Slider(st, "Hips back (m)", Plugin.HipBack, 0f, 0.3f, "0.00");
            Slider(st, "Feet apart (m)", Plugin.Stance, 0f, 0.5f, "0.00");
            Slider(st, "Toes out (deg)", Plugin.ToesOut, 0f, 45f, "0");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hands on", st.label, GUILayout.Width(236f), GUILayout.Height(34f));
            string[] hands = { "Hips", "Knees", "None" };
            int cur = Array.FindIndex(hands, h => string.Equals(h, Plugin.HandsOn.Value, StringComparison.OrdinalIgnoreCase));
            int next = Choice(st, hands, Mathf.Max(0, cur), 80f);
            if (next != cur)
            {
                Plugin.HandsOn.Value = hands[next];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, "Behaviour");
            Slider(st, "Seconds per move (6 = normal)", Plugin.MoveSeconds, 2f, 20f, "0");
            Check(st, "Show the on-screen notice", Plugin.ShowNotice);
            CardEnd();

            ResetButton(st, "Reset the twerk settings", () =>
                ResetAll(Plugin.Smoothness, Plugin.Frequency, Plugin.BounceHeight, Plugin.TiltDegrees, Plugin.ButtAmplitude,
                    Plugin.ShakeChest, Plugin.ChestAmplitude, Plugin.LeanDegrees, Plugin.Arch, Plugin.SquatDepth, Plugin.HipBack, Plugin.Stance, Plugin.ToesOut, Plugin.HandsOn,
                    Plugin.Rhythm, Plugin.PresetName, Plugin.MoveSeconds, Plugin.ShowNotice));
        }

        private static void DrawRide(ShakeController host, Styles st)
        {
            CardBegin(st, null);
            if (GUILayout.Button(host.Riding ? "Stop the ride" : "Start the ride", host.Riding ? st.button : st.primary, GUILayout.Height(40f)))
            {
                host.ToggleMode(true);
            }
            CardEnd();

            DrawStyle(st);

            CardBegin(st, "Motion");
            Slider(st, "Smoothness", Plugin.Smoothness, 0f, 1f, "0.00");
            Slider(st, "Speed (strokes/s)", Plugin.RideFrequency, 0.5f, 4f, "0.0");
            Slider(st, "Hips up and down (m)", Plugin.RideBounce, 0f, 0.3f, "0.00");
            Slider(st, "Hips forward-back (m)", Plugin.RideGrind, 0f, 0.3f, "0.00");
            Slider(st, "Pelvis rock (deg)", Plugin.RideTilt, 0f, 25f, "0.0");
            Slider(st, "Springiness of the landing", Plugin.RideSpring, 0f, 1f, "0.00");
            CardEnd();

            CardBegin(st, "Pose");
            PoseRow(st, Poses.Ride, Plugin.RidePoseType);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Facing", st.label, GUILayout.Width(236f), GUILayout.Height(34f));
            string[] facings = { "Face", "Away" };
            int facingCur = Array.FindIndex(facings, f => string.Equals(f, Plugin.RideFacing.Value, StringComparison.OrdinalIgnoreCase));
            int facingNext = Choice(st, new[] { "Toward him", "Away (reverse)" }, Mathf.Max(0, facingCur), 130f);
            if (facingNext != facingCur)
            {
                Plugin.RideFacing.Value = facings[facingNext];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hands on", st.label, GUILayout.Width(236f), GUILayout.Height(34f));
            string[] rideHandsShown = { "His chest", "His thighs", "Her thighs" };
            string[] rideHands = { "Hero", "HeroThighs", "Thighs" };
            int rideCur = Array.FindIndex(rideHands, h => string.Equals(h, Plugin.RideHandsOn.Value, StringComparison.OrdinalIgnoreCase));
            int rideNext = Choice(st, rideHandsShown, Mathf.Max(0, rideCur), 100f);
            if (rideNext != rideCur)
            {
                Plugin.RideHandsOn.Value = rideHands[rideNext];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            Slider(st, "Squat (m)", Plugin.RideSquat, 0f, 1f, "0.00");
            Slider(st, "Sink into him (m)", Plugin.RideSink, 0f, 0.15f, "0.00");
            Slider(st, "Lean forward (deg)", Plugin.RideLean, 0f, 80f, "0");
            Slider(st, "Lean back, hands on his thighs (deg)", Plugin.RideLeanBack, 0f, 40f, "0");
            Slider(st, "Feet apart (m)", Plugin.RideStance, 0f, 0.6f, "0.00");
            CardEnd();

            CardBegin(st, "Hero");
            Check(st, "Carry the hero under her", Plugin.RideHero);
            Check(st, "Match his crotch to hers automatically", Plugin.RideAutoMatch);
            Slider(st, "Fine-tune the contact (m)", Plugin.RideHeroShift, -0.5f, 0.5f, "0.00");
            Slider(st, "Height above floor (m)", Plugin.RideHeroClearance, -0.2f, 0.4f, "0.00");
            Check(st, "Carry his physical body with him", Plugin.RideHeroSolid);
            Check(st, "Switch off his controllers and idle sway", Plugin.RideHeroControllers);
            Check(st, "He gets wet while she rides him", Plugin.RideWet);
            Slider(st, "Wet shine", Plugin.RideWetShine, 0f, 1f, "0.00");
            GUILayout.Label("He lies on his back under her and goes back where he stood when the ride ends. His crotch is matched to hers by itself, in any pose; the fine-tune moves him toward his head (+) or his feet (-). Raise Height above floor if he sinks into it.", st.dim);
            CardEnd();

            ResetButton(st, "Reset the ride settings", () =>
                ResetAll(Plugin.RideFrequency, Plugin.RideBounce, Plugin.RideGrind, Plugin.RideTilt, Plugin.RideSpring, Plugin.RideSquat, Plugin.RideSink, Plugin.RideHandsOn,
                    Plugin.RideLeanBack, Plugin.RideStance, Plugin.RideLean, Plugin.RideFacing, Plugin.RideHero, Plugin.RideAutoMatch, Plugin.RideHeroShift, Plugin.RideHeroClearance,
                    Plugin.RideHeroSolid, Plugin.RideHeroControllers, Plugin.RideWet, Plugin.RideWetShine));
        }
    }
}
