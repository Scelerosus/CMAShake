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
            public GUIStyle fold, big, bigOn, tip, warn, opt, optOn;
            public GUIStyle sliderTrack, sliderThumb, scrollTrack, scrollThumb;
        }

        private const float Width = 650f;
        private const float Pad = 18f;
        private const float ThumbSize = 22f;

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
        private static bool s_resize;
        private static float s_height;
        private static bool s_layoutLoaded;
        private static float s_uiScale;
        private const float MinHeight = 380f;
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
        internal static bool TypingText => s_open && (s_typing || s_capture != null);

        // The hotkey that is waiting for its new key, the help line under the pointer, and which cards are folded open.
        private static ConfigEntry<KeyCode> s_capture;
        private static string s_tipShown = "";
        private static readonly Dictionary<string, GUIContent> s_tips = new Dictionary<string, GUIContent>();
        private static readonly Dictionary<string, bool> s_folds = new Dictionary<string, bool>();

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
            s_capture = null;
            s_resize = false;
            if (open)
            {
                LoadLayout();
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

        // Where the window was left, how tall and how big it is: kept in the config file between games.
        private static void LoadLayout()
        {
            if (s_layoutLoaded)
            {
                return;
            }
            s_layoutLoaded = true;
            s_pos = new Vector2(Plugin.WindowX.Value, Plugin.WindowY.Value);
            s_height = Plugin.WindowHeight.Value;
        }

        private static void StoreLayout()
        {
            Plugin.WindowX.Value = Mathf.Round(s_pos.x);
            Plugin.WindowY.Value = Mathf.Round(s_pos.y);
            Plugin.WindowHeight.Value = Mathf.Round(s_height);
            MarkDirty();
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

        private static Font s_font;
        private static bool s_fontTried;

        // A font of the system's that has the letters of both languages. The game's own is kept if none is found.
        internal static Font UiFont
        {
            get
            {
                if (!s_fontTried)
                {
                    s_fontTried = true;
                    try
                    {
                        s_font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma", "Verdana" }, 15);
                        if (s_font != null)
                        {
                            s_font.hideFlags = HideFlags.HideAndDontSave;
                        }
                    }
                    catch (Exception ex)
                    {
                        s_font = null;
                        Plugin.ModLog.LogWarning("Settings window: no system font could be made, the game's own is used: " + ex.Message);
                    }
                }
                return s_font;
            }
        }

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
            // Narrow padding: the names of the pages and of the choices are longer in Russian.
            st.tab.fontSize = 15;
            st.tab.padding = Pad4(4, 4, 7, 8);
            st.tabOn.fontSize = 15;
            st.tabOn.padding = Pad4(4, 4, 7, 8);
            st.opt = new GUIStyle(st.tab) { fontSize = 14, padding = Pad4(4, 4, 5, 6) };
            st.optOn = new GUIStyle(st.tabOn) { fontSize = 14, padding = Pad4(4, 4, 5, 6) };

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

            st.fold = new GUIStyle(st.ghost) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = true, padding = Pad4(4, 4, 2, 2), margin = Pad4(0, 0, 0, 0) };
            Tint(st.fold, Accent);

            st.big = new GUIStyle(st.button) { fontSize = 16, richText = true, padding = Pad4(8, 8, 6, 6), margin = Pad4(0, 0, 2, 2) };
            st.bigOn = new GUIStyle(st.big);
            Tint(st.bigOn, Color.white);
            Back(st.bigOn, tPrimary, tPrimaryHover, tPrimaryDown, 11);

            st.tip = new GUIStyle(st.dim) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, clipping = TextClipping.Clip };
            st.warn = new GUIStyle(st.dim);
            Tint(st.warn, Hex("#FFB454"));

            // One font for the whole window, taken from the system, so that both languages have all their letters.
            Font font = UiFont;
            if (font != null)
            {
                foreach (GUIStyle s in new[]
                {
                    st.title, st.badge, st.h2, st.label, st.dim, st.value, st.status, st.statusOn, st.keycap, st.placeholder, st.button, st.primary, st.ghost,
                    st.close, st.tab, st.tabOn, st.row, st.rowSel, st.field, st.switchText, st.fold, st.big, st.bigOn, st.tip, st.warn, st.opt, st.optOn
                })
                {
                    s.font = font;
                }
            }

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

        private static string T(string en, string ru)
        {
            return Loc.T(en, ru);
        }

        private const int TabHome = 0, TabTwerk = 1, TabRide = 2, TabBikini = 3, TabProfiles = 4, TabSettings = 5;

        private static void DrawWindow(ShakeController host)
        {
            Styles st = s_st;
            Event ev = Event.current;

            // The size slider takes effect once it is let go: changing the size under the pointer while it drags would make it jump.
            if (s_uiScale <= 0f || GUIUtility.hotControl == 0)
            {
                s_uiScale = Mathf.Clamp(Plugin.UiScale.Value, 0.75f, 1.5f);
            }
            float scale = Mathf.Max(1f, Screen.height / 1080f) * s_uiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            bool collapsed = Plugin.WindowCollapsed.Value;
            float maxH = Mathf.Max(MinHeight, sh - 20f);
            s_pos.x = Mathf.Clamp(s_pos.x, 0f, Mathf.Max(0f, sw - 140f));
            s_pos.y = Mathf.Clamp(s_pos.y, 0f, Mathf.Max(0f, sh - 90f));
            // Never taller than the room below the window, so its bottom line and the corner that resizes it stay on screen.
            maxH = Mathf.Clamp(sh - 10f - s_pos.y, MinHeight, maxH);
            float h = collapsed ? 128f : s_height > 1f ? Mathf.Clamp(s_height, MinHeight, maxH) : Mathf.Min(sh - 40f, 920f, maxH);
            if (!collapsed && s_pos.y + h > sh - 10f)
            {
                // Not even the shortest window fits below: lift it.
                s_pos.y = Mathf.Max(0f, sh - 10f - h);
            }

            // A key is being chosen for a hotkey: the next key pressed is it. Esc leaves it as it was, Backspace sets none.
            if (s_capture != null && ev != null && ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None)
            {
                if (ev.keyCode == KeyCode.Backspace || ev.keyCode == KeyCode.Delete)
                {
                    s_capture.Value = KeyCode.None;
                    MarkDirty();
                }
                else if (ev.keyCode != KeyCode.Escape)
                {
                    s_capture.Value = ev.keyCode;
                    MarkDirty();
                }
                s_capture = null;
                ev.Use();
                SaveIfDirty();
            }

            // Esc: first lets go of a text box, then closes the window. (While a hotkey waits for its key, Esc cancels that above.)
            if (s_capture == null && ev != null && ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ev.Use();
                if (s_typing)
                {
                    GUI.FocusControl(null);
                    s_typing = false;
                }
                else
                {
                    SetOpen(host, false);
                    return;
                }
            }

            // Fades and slides in when opened.
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - s_openedAt) / 0.18f));
            GUI.color = new Color(1f, 1f, 1f, appear);
            Rect r = new Rect(s_pos.x, s_pos.y + (1f - appear) * 14f, Width, h);

            // Drag the window by its header (the part of it that has no buttons); change its height by the bottom-right corner.
            Rect header = new Rect(r.x, r.y, r.width - 230f, 66f);
            Rect grip = new Rect(r.xMax - 26f, r.yMax - 26f, 26f, 26f);
            if (ev != null)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && !collapsed && grip.Contains(ev.mousePosition))
                {
                    s_resize = true;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && header.Contains(ev.mousePosition))
                {
                    s_drag = true;
                    s_dragOffset = ev.mousePosition - s_pos;
                }
                else if (s_resize && ev.type == EventType.MouseDrag)
                {
                    s_height = Mathf.Clamp(ev.mousePosition.y - r.y + 8f, MinHeight, maxH);
                    ev.Use();
                }
                else if (s_drag && ev.type == EventType.MouseDrag)
                {
                    s_pos = ev.mousePosition - s_dragOffset;
                }
                else if (ev.rawType == EventType.MouseUp)
                {
                    if (s_drag || s_resize)
                    {
                        StoreLayout();
                    }
                    s_drag = false;
                    s_resize = false;
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
            int lang = Loc.Ru ? 0 : 1;
            int langNext = Choice(st, new[] { "RU", "EN" }, lang, 46f, 0f);
            if (langNext != lang)
            {
                Plugin.Language.Value = langNext == 0 ? "ru" : "en";
                MarkDirty();
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(collapsed ? "+" : "–", st.close, GUILayout.Width(32f), GUILayout.Height(32f)))
            {
                Plugin.WindowCollapsed.Value = !collapsed;
                s_capture = null;
                s_resize = false;
                MarkDirty();
                SaveIfDirty();
            }
            Tip(collapsed ? T("Unfold the window.", "Развернуть окно.") : T("Fold the window to a small bar with the start buttons.", "Свернуть окно в маленькую панель с кнопками запуска."));
            if (GUILayout.Button("X", st.close, GUILayout.Width(32f), GUILayout.Height(32f)))
            {
                SetOpen(host, false);
            }
            GUILayout.EndHorizontal();

            if (collapsed)
            {
                DrawQuickBar(host, st);
                GUILayout.EndVertical();
                GUILayout.EndArea();
                FinishFrame(ev);
                return;
            }

            GUILayout.Space(4f);

            string[] tabs = { T("Home", "Главная"), T("Twerk", "Тверк"), T("Ride", "Райд"), T("Bikini", "Бикини"), T("Profiles", "Профили"), T("Settings", "Настройки") };
            GUILayout.BeginHorizontal(st.seg);
            float tabWidth = (Width - 2f * Pad - 8f) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUILayout.Button(tabs[i], i == s_tab ? st.tabOn : st.tab, GUILayout.Width(tabWidth - 0.5f), GUILayout.Height(34f)))
                {
                    s_tab = i;
                    s_capture = null;
                    s_scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();

            s_scroll = GUILayout.BeginScrollView(s_scroll, GUILayout.ExpandHeight(true));
            switch (s_tab)
            {
                case TabTwerk: DrawTwerk(host, st); break;
                case TabRide: DrawRide(host, st); break;
                case TabBikini: DrawBikini(host, st); break;
                case TabProfiles: DrawProfiles(host, st); break;
                case TabSettings: DrawSettings(host, st); break;
                default: DrawHome(host, st); break;
            }
            GUILayout.EndScrollView();

            // One line at the bottom: what the setting under the pointer does, or the keys when the pointer is on nothing.
            GUILayout.Space(6f);
            if (!string.IsNullOrEmpty(s_tipShown))
            {
                GUILayout.Label(s_tipShown, st.tip, GUILayout.Height(40f));
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Height(40f));
                Hint(st, Plugin.MenuKey.Value, T("menu", "меню"));
                Hint(st, Plugin.ToggleKey.Value, T("twerk", "тверк"));
                Hint(st, Plugin.RideKey.Value, T("ride", "райд"));
                Hint(st, Plugin.BikiniKey.Value, T("bikini", "бикини"));
                Hint(st, Plugin.PresetKey.Value, T("preset", "пресет"));
                GUILayout.Label("Esc", st.keycap);
                GUILayout.Label(T("close", "закрыть"), st.dim, GUILayout.Width(58f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // The corner that changes the height: three short diagonal strokes.
            if (Repainting())
            {
                Color keep = GUI.color;
                bool hot = s_resize || (ev != null && grip.Contains(ev.mousePosition));
                GUI.color = new Color(1f, 1f, 1f, (hot ? 0.75f : 0.3f) * keep.a);
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (i + j >= 2)
                        {
                            GUI.DrawTexture(new Rect(grip.x + 6f + i * 5f, grip.y + 6f + j * 5f, 3f, 3f), tWhitePill);
                        }
                    }
                }
                GUI.color = keep;
            }

            FinishFrame(ev);
        }

        // The end of every frame of the window, folded or not.
        private static void FinishFrame(Event ev)
        {
            if (Repainting())
            {
                s_tipShown = GUI.tooltip;
            }

            string focus = GUI.GetNameOfFocusedControl();
            s_typing = focus == "search" || focus == "hex" || focus == "profile";

            if (ev != null && ev.rawType == EventType.MouseUp)
            {
                SaveIfDirty();
            }
        }

        // The folded window: the three start buttons and nothing else.
        private static void DrawQuickBar(ShakeController host, Styles st)
        {
            bool twerk = host.Twerking;
            bool ride = host.Riding;
            bool bikini = Bikini.AnyOn();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Twerk", "Тверк"), twerk ? st.bigOn : st.big, GUILayout.Height(40f)))
            {
                host.ToggleMode(false);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(T("Ride", "Райд"), ride ? st.bigOn : st.big, GUILayout.Height(40f)))
            {
                host.ToggleMode(true);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(T("Bikini", "Бикини"), bikini ? st.bigOn : st.big, GUILayout.Height(40f)))
            {
                host.ToggleBikini();
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(Loc.Preset(Plugin.PresetName.Value), st.big, GUILayout.Width(120f), GUILayout.Height(40f)))
            {
                host.CyclePreset();
            }
            Tip(T("The preset: click for the next one.", "Пресет: щелчок — следующий."));
            GUILayout.EndHorizontal();
        }

        private static void Hint(Styles st, KeyCode key, string what)
        {
            if (key == KeyCode.None)
            {
                return;
            }
            GUILayout.Label(key.ToString(), st.keycap);
            GUILayout.Label(what, st.dim, GUILayout.Width(58f));
        }

        // ---- widgets ----

        private const float LabelWidth = 236f;

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

        // A card that folds: its heading is a button that opens and closes it. Returns whether what is in it is to be drawn.
        // Always followed by CardEnd, open or not.
        private static bool FoldBegin(Styles st, string key, string title, string closedNote, bool openAtFirst)
        {
            GUILayout.BeginVertical(st.card);
            if (!s_folds.TryGetValue(key, out bool open))
            {
                open = openAtFirst;
            }
            string text = (open ? "–   " : "+   ") + title.ToUpperInvariant();
            if (!open && !string.IsNullOrEmpty(closedNote))
            {
                text += "   <size=12><color=#9494AA>" + closedNote + "</color></size>";
            }
            if (GUILayout.Button(text, st.fold, GUILayout.Height(26f)))
            {
                open = !open;
            }
            s_folds[key] = open;
            if (open)
            {
                GUILayout.Space(6f);
            }
            return open;
        }

        private static bool Repainting()
        {
            Event e = Event.current;
            return e != null && e.type == EventType.Repaint;
        }

        // Gives the control (or row) just drawn a line of help, shown at the bottom of the window while the pointer is on it.
        private static void Tip(string tip)
        {
            if (string.IsNullOrEmpty(tip) || !Repainting())
            {
                return;
            }
            if (!s_tips.TryGetValue(tip, out GUIContent content))
            {
                content = new GUIContent("", tip);
                s_tips[tip] = content;
            }
            GUI.Label(GUILayoutUtility.GetLastRect(), content, GUIStyle.none);
        }

        // A slider with a filled track, a round handle and a small mark where its default is. Returns the new value;
        // a right click on the row asks for the default.
        private static float SliderRow(Styles st, string label, float value, float min, float max, string format, string unit, float scale, Color fill, float mark, out bool reset)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, st.label, GUILayout.Width(LabelWidth), GUILayout.Height(30f));
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
                if (!float.IsNaN(mark) && mark >= min && mark <= max)
                {
                    float mx = r.x + ThumbSize * 0.5f + Mathf.InverseLerp(min, max, mark) * (r.width - ThumbSize);
                    GUI.color = new Color(1f, 1f, 1f, 0.55f * keep.a);
                    GUI.DrawTexture(new Rect(mx - 1f, cy + 6f, 2f, 5f), tWhitePill);
                }
                GUI.color = keep;
            }
            float nv = GUI.HorizontalSlider(r, value, min, max, st.sliderTrack, st.sliderThumb);
            GUILayout.Label((nv * scale).ToString(format) + unit, st.value, GUILayout.Width(72f), GUILayout.Height(30f));
            GUILayout.EndHorizontal();

            reset = false;
            Event ev = Event.current;
            if (ev != null && ev.type == EventType.MouseDown && ev.button == 1 && GUILayoutUtility.GetLastRect().Contains(ev.mousePosition))
            {
                reset = true;
                ev.Use();
            }
            return nv;
        }

        private static void Slider(Styles st, string label, ConfigEntry<float> e, float min, float max, string format, string unit, float scale, string tip)
        {
            float v = e.Value;
            float def = e.DefaultValue is float f ? f : float.NaN;
            float nv = SliderRow(st, label, v, min, max, format, unit, scale, Accent, def, out bool reset);
            Tip(tip);
            if (reset && !float.IsNaN(def))
            {
                nv = def;
            }
            if (Mathf.Abs(nv - v) > 1e-6f)
            {
                e.Value = nv;
                MarkDirty();
                if (reset)
                {
                    SaveIfDirty();
                }
            }
        }

        // The units values are shown in: what is kept in metres is shown in centimetres.
        private static string Cm => T(" cm", " см");
        private static string PerSec => T(" /s", " /с");
        private static string Sec => T(" s", " с");

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

        private static void Check(Styles st, string label, ConfigEntry<bool> e, string tip)
        {
            bool nv = SwitchRow(st, label, e.Value);
            Tip(tip);
            if (nv != e.Value)
            {
                e.Value = nv;
                MarkDirty();
            }
        }

        // A row of choices, one of them selected. Returns the index chosen.
        private static int Choice(Styles st, string[] names, int selected, float itemWidth, float height = 30f)
        {
            int result = selected;
            GUILayout.BeginHorizontal(st.seg, GUILayout.ExpandWidth(false));
            for (int i = 0; i < names.Length; i++)
            {
                if (GUILayout.Button(names[i], i == selected ? st.optOn : st.opt, GUILayout.Width(itemWidth), GUILayout.Height(height > 0f ? height : 26f)))
                {
                    result = i;
                }
            }
            GUILayout.EndHorizontal();
            return result;
        }

        // A named row of choices that sets a setting kept as text.
        private static void ChoiceRow(Styles st, string label, string[] shown, string[] values, ConfigEntry<string> e, float itemWidth, string tip)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, st.label, GUILayout.Width(LabelWidth), GUILayout.Height(34f));
            int cur = Array.FindIndex(values, v => string.Equals(v, e.Value, StringComparison.OrdinalIgnoreCase));
            int next = Choice(st, shown, cur, itemWidth);
            if (next != cur && next >= 0)
            {
                e.Value = values[next];
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            Tip(tip);
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
            Tip(T("Puts every setting of this page back to what it was when the mod was installed.", "Возвращает все настройки этой страницы к тем, что были при установке мода."));
        }

        private static void ResetAll(params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase e in entries)
            {
                e.BoxedValue = e.DefaultValue;
            }
            MarkDirty();
        }

        // ---- shared rows: the same setting can be shown on more than one page ----

        private static readonly string[] PresetValues = { "Slow", "Soft", "Fast", "Mine" };
        private static readonly string[] RhythmValues = { "Steady", "Random", "Scenario" };

        private static void PresetRow(Styles st)
        {
            ChoiceRow(st, T("Preset", "Пресет"), new[] { Loc.Preset("Slow"), Loc.Preset("Soft"), Loc.Preset("Fast"), Loc.Preset("Mine") }, PresetValues, Plugin.PresetName, 82f,
                T($"How fast and deep she moves, on top of your own settings. Slow: unhurried at full depth. Soft: slower and gentler. Fast: quicker. Mine: exactly as set. Key {Loc.Key(Plugin.PresetKey.Value)} switches it.",
                  $"Темп и размах поверх ваших настроек. Медленно: неторопливо, на всю глубину. Мягко: медленнее и слабее. Быстро: быстрее. Своё: ровно как настроено. Клавиша {Loc.Key(Plugin.PresetKey.Value)} переключает."));
        }

        private static void RhythmRow(Styles st)
        {
            ChoiceRow(st, T("Rhythm", "Ритм"), new[] { Loc.Rhythm("Steady"), Loc.Rhythm("Random"), Loc.Rhythm("Scenario") }, RhythmValues, Plugin.Rhythm, 100f,
                T("Steady: one even movement. Random: she changes moves and pauses by herself. Scenario: a set sequence that loops (slow circle, bounces, pause, faster, side to side).",
                  "Ровно: одно равномерное движение. Случайно: сама меняет движения и делает паузы. Сценарий: заданная последовательность по кругу (медленный круг, такты, пауза, быстрее, из стороны в сторону)."));
        }

        private static void SmoothRow(Styles st)
        {
            Slider(st, T("Smoothness", "Плавность"), Plugin.Smoothness, 0f, 1f, "0", " %", 100f,
                T("Rounds off every movement. 100 % is the softest; lower is sharper, with a harder drop.", "Сглаживает все движения. 100 % — мягче всего, меньше — резче, она опускается жёстче."));
        }

        private static bool IsSteady()
        {
            return string.Equals(Plugin.Rhythm.Value, "Steady", StringComparison.OrdinalIgnoreCase);
        }

        private static void MoveSecondsRow(Styles st)
        {
            Slider(st, T("Change moves every", "Менять движение каждые"), Plugin.MoveSeconds, 2f, 20f, "0", Sec, 1f,
                T("How long she keeps one move before going on to the next. 6 is normal.", "Сколько она делает одно движение, прежде чем перейти к следующему. Обычно 6."));
        }

        private static void TransitionRow(Styles st)
        {
            Slider(st, T("Transition time", "Время перехода"), Plugin.PoseRamp, 0.3f, 4f, "0.0", Sec, 1f,
                T("How long she takes to get into and out of a pose, to change from the twerk to the ride, and to move to a new pose or put her hands elsewhere.",
                  "За сколько секунд она встаёт в позу и выходит из неё, переходит от тверка к райду, меняет позу или кладёт руки в другое место."));
        }

        // Kinds of pose: choosing one sets a bundle of the settings. What was set before is kept as the profile "_last".
        private static void PoseRow(Styles st, string label, Poses.Pose[] set, ConfigEntry<string> current)
        {
            GUILayout.Label(label, st.dim);
            string[] names = new string[set.Length];
            for (int i = 0; i < set.Length; i++)
            {
                names[i] = Loc.Pose(set[i].name);
            }
            int cur = Poses.IndexOf(set, current.Value);
            int next = Choice(st, names, cur, 134f);
            if (next != cur && next >= 0)
            {
                Poses.Apply(set[next], current);
            }
            Tip(T("A ready-made pose: it sets several of the pose settings at once, and she moves into it smoothly. You can still adjust each of them after.",
                  "Готовая поза: задаёт сразу несколько настроек позы, и она плавно в неё переходит. После этого каждую можно подправить отдельно."));
            GUILayout.Space(4f);
        }

        // ---- Home ----

        // A big button with a name, what pressing it does, and its key.
        private static bool BigButton(Styles st, string name, string action, KeyCode key, bool on)
        {
            string text = "<b>" + name + "</b>\n<size=12>" + action + (key != KeyCode.None ? "   [" + key + "]" : "") + "</size>";
            return GUILayout.Button(text, on ? st.bigOn : st.big, GUILayout.Width(181f), GUILayout.Height(58f));
        }

        private static void DrawHome(ShakeController host, Styles st)
        {
            bool twerk = host.Twerking;
            bool ride = host.Riding;
            bool bikini = Bikini.AnyOn();

            CardBegin(st, null);
            GUILayout.Label(T("Now", "Сейчас"), st.dim);
            GUILayout.Label(ride ? T("She is riding him", "Она сверху на нём") : twerk ? T("She is twerking", "Она тверкает") : T("Nothing is running", "Ничего не запущено"),
                ride || twerk ? st.statusOn : st.status);
            Gap(6f);
            GUILayout.BeginHorizontal();
            if (BigButton(st, T("Twerk", "Тверк"), twerk ? T("stop", "остановить") : T("start", "запустить"), Plugin.ToggleKey.Value, twerk))
            {
                host.ToggleMode(false);
            }
            Tip(T("She bends over and twerks where she stands. Pressing it during the ride changes over to the twerk smoothly.",
                  "Она наклоняется и тверкает там, где стоит. Если нажать во время райда, плавно перейдёт к тверку."));
            GUILayout.Space(8f);
            if (BigButton(st, T("Ride", "Райд"), ride ? T("stop", "остановить") : T("start", "запустить"), Plugin.RideKey.Value, ride))
            {
                host.ToggleMode(true);
            }
            Tip(T("The hero lies down under her and she rides him. The woman nearest to your view is the one. When it ends he gets back up where he stood.",
                  "Герой ложится под неё, и она садится сверху. Выбирается ближайшая к камере девушка. По окончании он встаёт на прежнее место."));
            GUILayout.Space(8f);
            if (BigButton(st, T("Bikini", "Бикини"), bikini ? T("take off", "снять") : T("put on", "надеть"), Plugin.BikiniKey.Value, bikini))
            {
                host.ToggleBikini();
            }
            Tip(T("Puts her in the bikini picked on the Bikini page, or back in her own clothes.", "Надевает бикини, выбранное на странице «Бикини», или возвращает её одежду."));
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("How she moves (twerk and ride)", "Как она двигается (тверк и райд)"));
            PresetRow(st);
            RhythmRow(st);
            if (!IsSteady())
            {
                MoveSecondsRow(st);
            }
            SmoothRow(st);
            TransitionRow(st);
            CardEnd();

            CardBegin(st, T("Poses", "Позы"));
            PoseRow(st, T("Twerk pose", "Поза в тверке"), Poses.Twerk, Plugin.TwerkPoseType);
            PoseRow(st, T("Ride pose", "Поза в райде"), Poses.Ride, Plugin.RidePoseType);
            CardEnd();

            GUILayout.Label(T("Speed and everything else are on the Twerk and Ride pages. Profiles keeps whole sets of settings under a name.",
                "Скорость и остальное — на страницах «Тверк» и «Райд». В «Профилях» можно сохранить все настройки под своим именем."), st.dim);
        }

        // ---- Twerk ----

        private static void DrawTwerk(ShakeController host, Styles st)
        {
            CardBegin(st, null);
            if (GUILayout.Button(host.Twerking ? T("Stop the twerk", "Остановить тверк") : T("Start the twerk", "Запустить тверк"), host.Twerking ? st.button : st.primary, GUILayout.Height(40f)))
            {
                host.ToggleMode(false);
            }
            CardEnd();

            CardBegin(st, T("Main", "Основное"));
            PoseRow(st, T("Pose", "Поза"), Poses.Twerk, Plugin.TwerkPoseType);
            Slider(st, T("Speed", "Скорость"), Plugin.Frequency, 0.1f, 5f, "0.00", PerSec, 1f,
                T("How many times a second she bounces her hips.", "Сколько раз в секунду она подбрасывает бёдра."));
            Slider(st, T("How high her hips go", "Как высоко подлетают бёдра"), Plugin.BounceHeight, 0f, 0.1f, "0.0", Cm, 100f,
                T("How far her hips go up and down with each bounce.", "На сколько бёдра поднимаются и опускаются за раз."));
            ChoiceRow(st, T("Hands", "Руки"), new[] { T("On hips", "На бёдрах"), T("On knees", "На коленях"), T("As in the game", "Как в игре") }, new[] { "Hips", "Knees", "None" }, Plugin.HandsOn, 100f,
                T("Where she puts her hands. As in the game: the mod leaves her arms alone.", "Куда она кладёт руки. «Как в игре» — мод не трогает руки."));
            CardEnd();

            if (FoldBegin(st, "twerk.motion", T("Body", "Тело"), T("hips, bottom, chest", "таз, попа, грудь"), false))
            {
                Slider(st, T("Hip tilt", "Наклон таза"), Plugin.TiltDegrees, 0f, 20f, "0.0", "°", 1f,
                    T("How much her pelvis tips with every bounce.", "Насколько таз наклоняется при каждом движении."));
                Slider(st, T("Bottom jiggle", "Колыхание попы"), Plugin.ButtAmplitude, 0f, 0.08f, "0.0", Cm, 100f,
                    T("How much her bottom jiggles after each bounce.", "Насколько попа колышется после каждого движения."));
                Check(st, T("Breasts jiggle too", "Грудь тоже колышется"), Plugin.ShakeChest,
                    T("Lets the breasts move with her. They stay on the chest and never go far.", "Грудь двигается вместе с ней, но остаётся на месте и далеко не уходит."));
                if (Plugin.ShakeChest.Value)
                {
                    Slider(st, T("Breast jiggle", "Колыхание груди"), Plugin.ChestAmplitude, 0f, 0.05f, "0.0", Cm, 100f,
                        T("The most the breasts may move.", "Насколько сильно может колыхаться грудь."));
                }
            }
            CardEnd();

            if (FoldBegin(st, "twerk.pose", T("Fine-tune the pose", "Подстроить позу"), T("lean, arch, squat, feet", "наклон, прогиб, присед, ноги"), false))
            {
                Slider(st, T("Bend forward", "Наклон вперёд"), Plugin.LeanDegrees, 0f, 85f, "0", "°", 1f,
                    T("How far she bends over.", "Насколько она наклоняется вперёд."));
                Slider(st, T("Back arch", "Прогиб в спине"), Plugin.Arch, 0f, 30f, "0", "°", 1f,
                    T("Arches her back: her bottom goes up and back.", "Прогибает спину: попа уходит вверх и назад."));
                Slider(st, T("How low she squats", "Как низко она приседает"), Plugin.SquatDepth, 0f, 0.5f, "0", Cm, 100f,
                    T("How far her hips drop.", "На сколько опускаются бёдра."));
                Slider(st, T("Bottom pushed back", "Попа назад"), Plugin.HipBack, 0f, 0.3f, "0", Cm, 100f,
                    T("How far her hips are pushed back.", "На сколько она отставляет попу назад."));
                Slider(st, T("Feet apart", "Ноги шире"), Plugin.Stance, 0f, 0.5f, "0", Cm, 100f,
                    T("How much wider each foot stands.", "На сколько шире она ставит каждую ногу."));
                Slider(st, T("Toes out", "Носки врозь"), Plugin.ToesOut, 0f, 45f, "0", "°", 1f,
                    T("How far the feet are turned out.", "Насколько носки развёрнуты наружу."));
            }
            CardEnd();

            ResetButton(st, T("Reset the twerk settings", "Сбросить настройки тверка"), () =>
                ResetAll(Plugin.Frequency, Plugin.BounceHeight, Plugin.TiltDegrees, Plugin.ButtAmplitude,
                    Plugin.ShakeChest, Plugin.ChestAmplitude, Plugin.LeanDegrees, Plugin.Arch, Plugin.SquatDepth, Plugin.HipBack, Plugin.Stance, Plugin.ToesOut, Plugin.HandsOn));
        }

        // ---- Ride ----

        private static void DrawRide(ShakeController host, Styles st)
        {
            CardBegin(st, null);
            if (GUILayout.Button(host.Riding ? T("Stop the ride", "Остановить райд") : T("Start the ride", "Запустить райд"), host.Riding ? st.button : st.primary, GUILayout.Height(40f)))
            {
                host.ToggleMode(true);
            }
            CardEnd();

            CardBegin(st, T("Main", "Основное"));
            PoseRow(st, T("Pose", "Поза"), Poses.Ride, Plugin.RidePoseType);
            ChoiceRow(st, T("How she sits", "Как она сидит"), new[] { T("Facing him", "Лицом к нему"), T("Back to him", "Спиной к нему") }, new[] { "Face", "Away" }, Plugin.RideFacing, 130f,
                T("Facing him, or with her back to him (reverse).", "Лицом к нему или спиной к нему (наоборот)."));
            bool away = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            RideHandsRow(st, away);
            Slider(st, T("Speed", "Скорость"), Plugin.RideFrequency, 0.05f, 4f, "0.00", PerSec, 1f,
                T("How many times a second she goes up and down on him. Go low for a slow ride.", "Сколько раз в секунду она поднимается и опускается на нём. Меньше — медленнее."));
            Slider(st, T("How high she rises", "Как высоко она поднимается"), Plugin.RideBounce, 0f, 0.3f, "0", Cm, 100f,
                T("How far she rises off him before coming back down.", "На сколько она приподнимается с него, прежде чем опуститься снова."));
            Slider(st, T("How low she sits", "Как низко она садится"), Plugin.RideSquat, 0f, 1f, "0", Cm, 100f,
                T("How low she goes. She never goes through him: set it deep and she sits right down on him.", "Насколько низко она опускается. Сквозь него не пройдёт: поставьте побольше, и она сядет на него до конца."));
            CardEnd();

            if (FoldBegin(st, "ride.motion", T("Movement", "Движение"), T("sliding, hip tilt, landing", "скольжение, наклон таза, посадка"), false))
            {
                Slider(st, T("Slide forward and back", "Скольжение вперёд-назад"), Plugin.RideGrind, 0f, 0.3f, "0", Cm, 100f,
                    T("How far her hips slide forward and back on him.", "На сколько она ёрзает бёдрами вперёд-назад на нём."));
                Slider(st, T("Hip tilt", "Наклон таза"), Plugin.RideTilt, 0f, 25f, "0.0", "°", 1f,
                    T("How much her pelvis tips with every stroke.", "Насколько таз наклоняется при каждом движении."));
                Slider(st, T("Soft landing", "Мягкость посадки"), Plugin.RideSpring, 0f, 1f, "0", " %", 100f,
                    T("At the bottom she sinks a little further, softly, with no bounce.", "Внизу она чуть глубже оседает на нём, мягко и без отскока."));
            }
            CardEnd();

            // Leaning back is only for her hands on his thighs behind her; any other way she leans forward.
            bool leansBack = !away && string.Equals(Plugin.RideHandsOn.Value, "HeroThighs", StringComparison.OrdinalIgnoreCase);
            if (FoldBegin(st, "ride.pose", T("Fine-tune the pose", "Подстроить позу"), T("lean, feet", "наклон, ноги"), false))
            {
                if (leansBack)
                {
                    Slider(st, T("Lean back", "Наклон назад"), Plugin.RideLeanBack, 0f, 40f, "0", "°", 1f,
                        T("How far she leans back to her hands on his thighs.", "Насколько она откидывается назад, опираясь на его бёдра."));
                }
                else
                {
                    Slider(st, T("Lean forward", "Наклон вперёд"), Plugin.RideLean, 0f, 80f, "0", "°", 1f,
                        T("How far she leans forward. With her hands on him she leans as far as it takes to reach him, never less than this.",
                          "Насколько она наклоняется вперёд. Если руки на нём, она наклонится столько, сколько нужно, чтобы дотянуться, но не меньше этого."));
                }
                Slider(st, T("Feet apart", "Ноги шире"), Plugin.RideStance, 0f, 0.6f, "0", Cm, 100f,
                    T("How much wider she puts her feet. They always stand outside his legs.", "На сколько шире она ставит ноги. Они всегда стоят по бокам от его ног."));
            }
            CardEnd();

            if (FoldBegin(st, "ride.hero", T("Him", "Он"), T("where he lies, getting wet", "где лежит, намокание"), false))
            {
                Slider(st, T("Move him up or down", "Сдвинуть его выше или ниже"), Plugin.RideHeroShift, -0.5f, 0.5f, "0", Cm, 100f,
                    T("He lies down under her by himself. If he is off, move him toward his head (+) or his feet (-).", "Он сам ложится точно под неё. Если сместился — подвиньте к голове (+) или к ногам (-)."));
                Slider(st, T("Raise or lower him", "Поднять или опустить его"), Plugin.RideHeroClearance, -0.2f, 0.4f, "0", Cm, 100f,
                    T("Raise him if he sinks into the floor, lower him if he floats above it.", "Поднимите, если он проваливается в пол; опустите, если висит над полом."));
                Check(st, T("His penis gets wet inside her", "Член намокает в ней"), Plugin.RideWet,
                    T("He gets wet from her only as far down as he has been in her: half in, and only the upper half is wet. The wetter she is, the faster. Out of her he dries slowly.",
                      "Член намокает от неё только на ту глубину, на которую вошёл: вошёл наполовину — мокрая только верхняя половина. Чем она влажнее, тем быстрее. Снаружи он медленно высыхает."));
                if (Plugin.RideWet.Value)
                {
                    Slider(st, T("How wet he looks", "Насколько мокрым выглядит"), Plugin.RideWetShine, 0f, 1f, "0", " %", 100f,
                        T("How glossy he gets at most.", "Насколько сильно он блестит, когда совсем мокрый."));
                }
            }
            CardEnd();

            ResetButton(st, T("Reset the ride settings", "Сбросить настройки райда"), () =>
                ResetAll(Plugin.RideFrequency, Plugin.RideBounce, Plugin.RideGrind, Plugin.RideTilt, Plugin.RideSpring, Plugin.RideSquat, Plugin.RideSink, Plugin.RideHandsOn,
                    Plugin.RideLeanBack, Plugin.RideStance, Plugin.RideLean, Plugin.RideFacing, Plugin.RideHeroShift, Plugin.RideHeroClearance,
                    Plugin.RideWet, Plugin.RideWetShine));
        }

        // Where her hands go in the ride. With her back to him his chest is behind her, so it is not offered: her hands
        // go on his thighs in front of her, or on her own.
        private static void RideHandsRow(Styles st, bool away)
        {
            if (!away)
            {
                ChoiceRow(st, T("Hands", "Руки"), new[] { T("On his chest", "На его груди"), T("On his thighs", "На его бёдрах"), T("On her thighs", "На своих бёдрах") }, new[] { "Hero", "HeroThighs", "Thighs" }, Plugin.RideHandsOn, 120f,
                    T("Where she puts her hands. On his chest she leans over him; on his thighs she leans back to them.", "Куда она кладёт руки. На его грудь — наклоняется к нему, на его бёдра — откидывается назад."));
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Hands", "Руки"), st.label, GUILayout.Width(LabelWidth), GUILayout.Height(34f));
            bool own = string.Equals(Plugin.RideHandsOn.Value, "Thighs", StringComparison.OrdinalIgnoreCase);
            int cur = own ? 1 : 0;
            int next = Choice(st, new[] { T("On his thighs", "На его бёдрах"), T("On her thighs", "На своих бёдрах") }, cur, 120f);
            if (next != cur && next >= 0)
            {
                Plugin.RideHandsOn.Value = next == 1 ? "Thighs" : "HeroThighs";
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            Tip(T("Where she puts her hands. His thighs are in front of her: she leans forward to them.", "Куда она кладёт руки. Его бёдра перед ней — она наклоняется к ним вперёд."));
        }

        // ---- Bikini ----

        private static void DrawBikini(ShakeController host, Styles st)
        {
            ResolvePicks();

            bool on = Bikini.AnyOn();
            CardBegin(st, null);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(T("Bikini", "Бикини"), st.dim);
            GUILayout.Label(on ? T("Worn", "Надето") : T("Not worn", "Не надето"), on ? st.statusOn : st.status);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(on ? T("Take it off", "Снять") : T("Put it on", "Надеть"), on ? st.button : st.primary, GUILayout.Width(150f), GUILayout.Height(40f)))
            {
                host.ToggleBikini();
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Pieces", "Что надеть"));
            PickRow(host, st, T("Top", "Верх"), s_top, Plugin.BikiniTop);
            PickRow(host, st, T("Bottom", "Низ"), s_bottom, Plugin.BikiniBottom);
            if (s_entries.Count > 0)
            {
                bool noTop = s_top == null;
                bool noBottom = s_bottom == null;
                if (noTop && noBottom)
                {
                    GUILayout.Label(T("Pick a top and a bottom from the list: only the clothes the bikini replaces are taken off.", "Выберите верх и низ в списке: снимается только та одежда, которую бикини заменяет."), st.dim);
                }
                else if (noTop || noBottom)
                {
                    GUILayout.Label(noTop ? T("No top picked: what she wears on top stays on.", "Верх не выбран: то, что на ней сверху, остаётся.")
                        : T("No bottom picked: what she wears below stays on.", "Низ не выбран: то, что на ней снизу, остаётся."), st.dim);
                }
            }
            Gap(6f);

            GUILayout.BeginHorizontal();
            int pick = Choice(st, new[] { T("Top", "Верх"), T("Bottom", "Низ") }, s_pickBottom ? 1 : 0, 84f);
            s_pickBottom = pick == 1;
            Tip(T("Which of the two the list below picks.", "Что из двух выбирается в списке ниже."));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Refresh list", "Обновить список"), st.ghost, GUILayout.Height(32f)))
            {
                Refresh();
            }
            Tip(T("Reads the installed clothing again. Clothing is only there once a game is loaded.", "Заново читает установленную одежду. Она появляется только после загрузки игры."));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("search");
            s_search = GUILayout.TextField(s_search, st.field);
            if (s_search.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "search")
            {
                Rect fr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(fr.x + 12f, fr.y, fr.width - 12f, fr.height), T("Search clothing...", "Поиск одежды..."), st.placeholder);
            }
            GUILayout.Space(10f);
            s_showAll = SwitchWide(st, T("All clothes", "Вся одежда"), s_showAll, 160f);
            Tip(T("Off: only swimwear and lingerie. On: every piece of clothing installed.", "Выключено: только купальники и бельё. Включено: вся установленная одежда."));
            GUILayout.EndHorizontal();

            RebuildShown();
            if (s_entries.Count == 0)
            {
                GUILayout.Label(T("No clothing is loaded yet. Start or load a game, then press Refresh list.", "Одежда ещё не загружена. Запустите или загрузите игру и нажмите «Обновить список»."), st.dim);
            }
            else if (s_shown.Count == 0)
            {
                GUILayout.Label(s_showAll || s_search.Length > 0 ? T("Nothing matches.", "Ничего не найдено.")
                    : T("No swimwear or lingerie of this kind is installed. Turn on All clothes to see the rest.", "Купальников и белья такого вида не установлено. Включите «Вся одежда», чтобы увидеть остальное."), st.dim);
            }

            s_listScroll = GUILayout.BeginScrollView(s_listScroll, GUILayout.Height(236f));
            Entry chosen = s_pickBottom ? s_bottom : s_top;
            int shown = 0;
            foreach (Entry e in s_shown)
            {
                if (shown++ >= 250)
                {
                    GUILayout.Label(T("More items: type in the search box to narrow the list.", "Есть ещё: введите что-нибудь в поиск, чтобы сузить список."), st.dim);
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

            CardBegin(st, T("Colour", "Цвет"));
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

            float r = SliderRow(st, T("Red", "Красный"), cur.r, 0f, 1f, "0", " %", 100f, Hex("#FF5A5A"), float.NaN, out bool _);
            float g = SliderRow(st, T("Green", "Зелёный"), cur.g, 0f, 1f, "0", " %", 100f, Hex("#4CD97B"), float.NaN, out bool _);
            float b = SliderRow(st, T("Blue", "Синий"), cur.b, 0f, 1f, "0", " %", 100f, Hex("#5AA9FF"), float.NaN, out bool _);
            if (Mathf.Abs(r - cur.r) > 0.0005f || Mathf.Abs(g - cur.g) > 0.0005f || Mathf.Abs(b - cur.b) > 0.0005f)
            {
                SetColor(new Color(r, g, b, 1f));
                cur = CurrentColor();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Colour code", "Код цвета"), st.label, GUILayout.Width(LabelWidth), GUILayout.Height(32f));
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
            Tip(T("The colour as a code, such as #D81B60. It changes at once while she wears the bikini.", "Цвет в виде кода, например #D81B60. Меняется сразу, пока бикини на ней."));
            CardEnd();

            CardBegin(st, T("Options", "Дополнительно"));
            bool solid = SwitchRow(st, T("Plain colour (hide the item's own pattern)", "Однотонный цвет (без рисунка вещи)"), Plugin.BikiniSolid.Value);
            Tip(T("On: the piece is one flat colour. Off: its own pattern shows through, tinted.", "Включено: вещь одного ровного цвета. Выключено: виден её рисунок, подкрашенный цветом."));
            if (solid != Plugin.BikiniSolid.Value)
            {
                Plugin.BikiniSolid.Value = solid;
                MarkDirty();
                host.ReapplyBikini();
            }
            Check(st, T("Put it on by itself for the twerk and the ride", "Надевать само на время тверка и райда"), Plugin.BikiniWithTwerk,
                T("The bikini goes on when the twerk or the ride starts and comes off when it stops.", "Бикини надевается при запуске тверка или райда и снимается при остановке."));
            CardEnd();

            ResetButton(st, T("Reset the bikini settings", "Сбросить настройки бикини"), () =>
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
                text = T("Nothing picked", "Не выбрано");
            }
            else if (current != null)
            {
                text = current.name;
            }
            else
            {
                text = T("Not found: ", "Не найдено: ") + setting.Value;
            }
            GUILayout.Label(text, st.label, GUILayout.Height(30f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Clear", "Убрать"), st.ghost, GUILayout.Width(90f), GUILayout.Height(30f)))
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

        // ---- Profiles ----

        private static string s_profileName = "";
        private static string s_profileSelected = "";
        private static string s_profileStatus = "";
        private static List<string> s_profiles = new List<string>();
        private static Vector2 s_profileScroll;
        private static int s_profilesLoadedFor = -1;

        private static string ProfileLabel(string name)
        {
            return name == "_last" ? T("(settings before the last change)", "(настройки до последнего изменения)") : name;
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

            CardBegin(st, T("Active profile", "Текущий профиль"));
            GUILayout.Label(string.IsNullOrEmpty(Plugin.ActiveProfile.Value) ? T("None: the settings are as you left them.", "Нет: настройки такие, какими вы их оставили.") : Plugin.ActiveProfile.Value, st.status);
            if (!string.IsNullOrEmpty(s_profileStatus))
            {
                GUILayout.Label(s_profileStatus, st.dim);
            }
            CardEnd();

            CardBegin(st, T("Save the current settings", "Сохранить текущие настройки"));
            GUILayout.Label(T("A profile keeps everything about the twerk, the ride and the bikini under a name. Hotkeys are not part of it.",
                "Профиль хранит под именем всё про тверк, райд и бикини. Клавиши в него не входят."), st.dim);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("profile");
            s_profileName = GUILayout.TextField(s_profileName, 40, st.field);
            if (s_profileName.Length == 0 && Repainting() && GUI.GetNameOfFocusedControl() != "profile")
            {
                Rect fr = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(fr.x + 12f, fr.y, fr.width - 12f, fr.height), T("Name of the profile...", "Имя профиля..."), st.placeholder);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button(T("Save", "Сохранить"), st.primary, GUILayout.Width(130f)))
            {
                string clean = Profiles.Clean(s_profileName);
                if (clean.Length == 0)
                {
                    s_profileStatus = T("Type a name first.", "Сначала введите имя.");
                }
                else if (Profiles.Save(clean))
                {
                    Plugin.ActiveProfile.Value = clean;
                    MarkDirty();
                    s_profileSelected = clean;
                    s_profileStatus = T("Saved as '", "Сохранено как «") + clean + T("'.", "».");
                    RefreshProfiles();
                }
                else
                {
                    s_profileStatus = T("Could not save it.", "Не удалось сохранить.");
                }
            }
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Saved profiles", "Сохранённые профили"));
            if (s_profiles.Count == 0)
            {
                GUILayout.Label(T("None yet. Save the current settings above.", "Пока нет. Сохраните текущие настройки выше."), st.dim);
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
            if (GUILayout.Button(T("Load", "Загрузить"), st.primary, GUILayout.Height(36f)) && any)
            {
                int applied = Profiles.Load(s_profileSelected);
                if (applied < 0)
                {
                    s_profileStatus = T("That profile is gone.", "Этого профиля больше нет.");
                }
                else
                {
                    if (s_profileSelected != "_last")
                    {
                        Plugin.ActiveProfile.Value = s_profileSelected;
                        MarkDirty();
                    }
                    s_profileStatus = T("Loaded '", "Загружен «") + ProfileLabel(s_profileSelected) + T("'.", "».");
                    host.ReapplyBikini();
                    RefreshProfiles();
                }
            }
            Tip(T("Puts the settings of the picked profile in place. What you had is kept as \"settings before the last change\".", "Применяет настройки выбранного профиля. Прежние сохраняются как «настройки до последнего изменения»."));
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Overwrite", "Перезаписать"), st.button, GUILayout.Height(36f)) && any && s_profileSelected != "_last")
            {
                s_profileStatus = Profiles.Save(s_profileSelected) ? T("Overwrote '", "Перезаписан «") + s_profileSelected + T("' with the current settings.", "» текущими настройками.") : T("Could not save it.", "Не удалось сохранить.");
                RefreshProfiles();
            }
            Tip(T("Saves the current settings over the picked profile.", "Сохраняет текущие настройки поверх выбранного профиля."));
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Delete", "Удалить"), st.ghost, GUILayout.Height(36f)) && any)
            {
                s_profileStatus = Profiles.Delete(s_profileSelected) ? T("Deleted '", "Удалён «") + ProfileLabel(s_profileSelected) + T("'.", "».") : T("Nothing to delete.", "Удалять нечего.");
                s_profileSelected = "";
                RefreshProfiles();
            }
            GUILayout.EndHorizontal();
            CardEnd();
        }

        // ---- Settings ----

        // A hotkey: its name, the key it has now as a button, and a button that takes it away. Pressing the key button
        // waits for the next key pressed.
        private static void KeyRow(Styles st, string label, ConfigEntry<KeyCode> e, string tip)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, st.label, GUILayout.Width(LabelWidth), GUILayout.Height(34f));
            bool waiting = s_capture == e;
            if (GUILayout.Button(waiting ? T("press a key...", "нажмите клавишу...") : Loc.Key(e.Value), waiting ? st.primary : st.button, GUILayout.Width(190f), GUILayout.Height(32f)))
            {
                s_capture = waiting ? null : e;
            }
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Default", "По умолчанию"), st.ghost, GUILayout.Height(32f)))
            {
                e.BoxedValue = e.DefaultValue;
                s_capture = null;
                MarkDirty();
            }
            GUILayout.EndHorizontal();
            Tip(tip);
        }

        private static void DrawSettings(ShakeController host, Styles st)
        {
            CardBegin(st, T("Language", "Язык"));
            ChoiceRow(st, T("Language of the window", "Язык окна"), new[] { "Русский", "English", T("Auto", "Авто") }, new[] { "ru", "en", "auto" }, Plugin.Language, 100f,
                T("Auto follows the language of your system.", "«Авто» берёт язык системы."));
            CardEnd();

            CardBegin(st, T("Window", "Окно"));
            Slider(st, T("Window size", "Размер окна"), Plugin.UiScale, 0.75f, 1.5f, "0", " %", 100f,
                T("Makes the whole window bigger or smaller. Applied when the slider is let go.", "Увеличивает или уменьшает всё окно целиком. Применяется, когда отпустите ползунок."));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Back to the corner", "Вернуть в угол"), st.button, GUILayout.Height(32f)))
            {
                s_pos = new Vector2(24f, 24f);
                StoreLayout();
            }
            Tip(T("Puts the window back at the top-left of the screen.", "Возвращает окно в левый верхний угол экрана."));
            GUILayout.Space(6f);
            if (GUILayout.Button(T("Full height", "Во всю высоту"), st.button, GUILayout.Height(32f)))
            {
                s_height = 0f;
                StoreLayout();
            }
            Tip(T("Makes the window as tall as the screen allows. Drag its bottom-right corner to make it shorter.", "Растягивает окно на всю доступную высоту. Потяните за правый нижний угол, чтобы сделать ниже."));
            GUILayout.EndHorizontal();
            CardEnd();

            CardBegin(st, T("Keys", "Клавиши"));
            GUILayout.Label(T("Press a key's button, then the key you want. Esc leaves it as it was, Backspace sets no key.",
                "Нажмите на кнопку с клавишей, затем нужную клавишу. Esc — оставить как было, Backspace — без клавиши."), st.dim);
            Gap(4f);
            KeyRow(st, T("This window", "Это окно"), Plugin.MenuKey, T("Opens and closes this window.", "Открывает и закрывает это окно."));
            KeyRow(st, T("Twerk", "Тверк"), Plugin.ToggleKey, T("Starts and stops the twerk.", "Запускает и останавливает тверк."));
            KeyRow(st, T("Ride", "Райд"), Plugin.RideKey, T("Starts and stops the ride.", "Запускает и останавливает райд."));
            KeyRow(st, T("Bikini", "Бикини"), Plugin.BikiniKey, T("Puts the bikini on and takes it off.", "Надевает и снимает бикини."));
            KeyRow(st, T("Next preset", "Следующий пресет"), Plugin.PresetKey, T("Switches to the next preset: Slow, Soft, Fast, Mine.", "Переключает пресет по кругу: Медленно, Мягко, Быстро, Своё."));
            if (Plugin.MenuKey.Value == KeyCode.None)
            {
                GUILayout.Label(T("This window has no key: once closed it can only be opened again by setting MenuKey in the config file.",
                    "У этого окна нет клавиши: после закрытия открыть его можно будет только через MenuKey в файле настроек."), st.warn);
            }
            CardEnd();

            CardBegin(st, T("On the screen", "На экране"));
            Check(st, T("Show a line at the top of the screen while she twerks or rides", "Показывать подсказку вверху экрана во время тверка и райда"), Plugin.ShowNotice,
                T("The line at the top of the screen that says what is running and which key stops it.", "Строка вверху экрана: что сейчас идёт и какой клавишей остановить."));
            CardEnd();

            ResetButton(st, T("Reset the keys and these settings", "Сбросить клавиши и эти настройки"), () =>
            {
                s_capture = null;
                ResetAll(Plugin.MenuKey, Plugin.ToggleKey, Plugin.RideKey, Plugin.BikiniKey, Plugin.PresetKey, Plugin.ShowNotice, Plugin.Language);
            });
        }
    }
}
