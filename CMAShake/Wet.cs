using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAShake
{
    // The hero's penis looks wet while she rides him: the glossiness of its material is raised a little more the longer she
    // sits on him, and goes back slowly once she is off. Nothing of the game's own is changed: the look is a per-renderer
    // override (a MaterialPropertyBlock for that one material slot) that is built on top of whatever the renderer already
    // has, and taken off again when it has dried.
    public sealed partial class ShakeController
    {
        private sealed class WetPart
        {
            public Renderer renderer;
            public int slot;
            public string name;
            public bool hadBlock;
            public readonly List<int> floatIds = new List<int>();
            public readonly List<float> floatOrig = new List<float>();
            public readonly List<float> floatTarget = new List<float>();
            public int colorId = -1;
            public Color colorOrig;
        }

        // Smoothness (glossiness) properties of the shaders the game is likely to use, with how glossy a wet surface is on each.
        private static readonly string[] WetFloats = { "_Smoothness", "_Glossiness", "_GlossMapScale", "_SmoothnessRemapMax", "_SmoothnessRemapMin", "_SmoothnessMax", "_SmoothnessMin" };
        private static readonly float[] WetFloatTo = { 0.92f, 0.92f, 1f, 1f, 0.6f, 1f, 0.6f };
        private static readonly Regex WetMaterialName = new Regex("penis|genit|dick|glans|shaft|scrot|testic", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly List<WetPart> m_wetParts = new List<WetPart>();
        private float m_wet;
        private float m_wetApplied = -1f;
        private MaterialPropertyBlock m_wetBlock;

        // Finds the materials to make wet, on the hero as he is taken up. Called once for each ride.
        private void WetCapture(Character hero)
        {
            WetRestore();
            if (!Plugin.RideWet.Value)
            {
                return;
            }
            try
            {
                var seen = new HashSet<int>();
                var names = new StringBuilder();
                int renderers = 0;
                Component[] places = { hero, hero.bodyAnimator };
                foreach (Component place in places)
                {
                    if (place == null)
                    {
                        continue;
                    }
                    var all = place.GetComponentsInChildren<Renderer>(true);
                    if (all == null)
                    {
                        continue;
                    }
                    foreach (Renderer r in all)
                    {
                        if (r == null || !seen.Add(r.GetInstanceID()))
                        {
                            continue;
                        }
                        renderers++;
                        var mats = r.sharedMaterials;
                        if (mats == null)
                        {
                            continue;
                        }
                        bool byRenderer = WetMaterialName.IsMatch(r.name) && mats.Length == 1;
                        if (names.Length < 900)
                        {
                            names.Append('\'').Append(r.name).Append("'[");
                            for (int i = 0; i < mats.Length; i++)
                            {
                                names.Append(i > 0 ? "," : "").Append(mats[i] != null ? mats[i].name : "-");
                            }
                            names.Append("] ");
                        }
                        for (int i = 0; i < mats.Length; i++)
                        {
                            Material m = mats[i];
                            if (m != null && (byRenderer || WetMaterialName.IsMatch(m.name)))
                            {
                                WetPart part = WetPartFor(r, i, m);
                                if (part != null)
                                {
                                    m_wetParts.Add(part);
                                }
                            }
                        }
                    }
                }
                if (m_wetParts.Count == 0)
                {
                    Plugin.ModLog.LogInfo($"Ride: wet: no penis material among {renderers} renderer(s) of the hero, nothing is made wet. Renderers: {names}");
                }
            }
            catch (Exception ex)
            {
                WetRestore();
                Plugin.ModLog.LogWarning("Wet look could not be prepared: " + ex.Message);
            }
        }

        private WetPart WetPartFor(Renderer r, int slot, Material m)
        {
            var part = new WetPart { renderer = r, slot = slot, name = m.name };
            var found = new StringBuilder();
            for (int i = 0; i < WetFloats.Length; i++)
            {
                int id = Shader.PropertyToID(WetFloats[i]);
                if (m.HasProperty(id))
                {
                    part.floatIds.Add(id);
                    part.floatOrig.Add(m.GetFloat(id));
                    part.floatTarget.Add(WetFloatTo[i]);
                    found.Append(WetFloats[i]).Append('=').Append(m.GetFloat(id).ToString("F2")).Append(' ');
                }
            }
            if (m.IsKeywordEnabled("_MATERIAL_FEATURE_CLEAR_COAT") && m.HasProperty(Shader.PropertyToID("_CoatMask")))
            {
                int id = Shader.PropertyToID("_CoatMask");
                part.floatIds.Add(id);
                part.floatOrig.Add(m.GetFloat(id));
                part.floatTarget.Add(0.7f);
                found.Append("_CoatMask ");
            }
            foreach (string c in new[] { "_BaseColor", "_Color" })
            {
                int id = Shader.PropertyToID(c);
                if (m.HasProperty(id))
                {
                    part.colorId = id;
                    part.colorOrig = m.GetColor(id);
                    found.Append(c).Append(' ');
                    break;
                }
            }
            part.hadBlock = r.HasPropertyBlock();
            string shader = m.shader != null ? m.shader.name : "?";
            if (part.floatIds.Count == 0)
            {
                // None of the usual ones: what the shader has that could be about gloss, to be added to the list.
                var other = new StringBuilder();
                try
                {
                    var sh = m.shader;
                    int n = sh != null ? sh.GetPropertyCount() : 0;
                    var rx = new Regex("smooth|gloss|rough|metal|spec|coat|sss|shin|wet|bright|fresnel", RegexOptions.IgnoreCase);
                    for (int i = 0; i < n && other.Length < 700; i++)
                    {
                        string pn = sh.GetPropertyName(i);
                        if (rx.IsMatch(pn))
                        {
                            other.Append(pn).Append(' ');
                        }
                    }
                }
                catch (Exception)
                {
                }
                Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}' (renderer '{r.name}', slot {slot}, shader '{shader}') has none of the known gloss properties. Its own: {other}");
                return null;
            }
            Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}' (renderer '{r.name}', slot {slot}, shader '{shader}') will be made wet through: {found}");
            return part;
        }

        // Writes the look for a wetness between 0 (as it was) and 1.
        private void WetApply(float level)
        {
            if (m_wetBlock == null)
            {
                m_wetBlock = new MaterialPropertyBlock();
            }
            foreach (WetPart p in m_wetParts)
            {
                if (p.renderer == null)
                {
                    continue;
                }
                p.renderer.GetPropertyBlock(m_wetBlock, p.slot);
                for (int i = 0; i < p.floatIds.Count; i++)
                {
                    float orig = p.floatOrig[i];
                    m_wetBlock.SetFloat(p.floatIds[i], Mathf.Lerp(orig, Mathf.Max(orig, p.floatTarget[i]), level));
                }
                if (p.colorId >= 0)
                {
                    // A wet surface is a little darker.
                    Color c = p.colorOrig;
                    float f = Mathf.Lerp(1f, 0.9f, level);
                    m_wetBlock.SetColor(p.colorId, new Color(c.r * f, c.g * f, c.b * f, c.a));
                }
                p.renderer.SetPropertyBlock(m_wetBlock, p.slot);
            }
        }

        // Takes the wet look off: what the renderer had before is put back, and nothing is left on it if it had nothing.
        private void WetRestore()
        {
            try
            {
                if (m_wetBlock == null)
                {
                    m_wetBlock = new MaterialPropertyBlock();
                }
                foreach (WetPart p in m_wetParts)
                {
                    if (p.renderer == null || m_wetApplied < 0f)
                    {
                        continue;
                    }
                    if (!p.hadBlock)
                    {
                        p.renderer.SetPropertyBlock(null, p.slot);
                        continue;
                    }
                    p.renderer.GetPropertyBlock(m_wetBlock, p.slot);
                    for (int i = 0; i < p.floatIds.Count; i++)
                    {
                        m_wetBlock.SetFloat(p.floatIds[i], p.floatOrig[i]);
                    }
                    if (p.colorId >= 0)
                    {
                        m_wetBlock.SetColor(p.colorId, p.colorOrig);
                    }
                    p.renderer.SetPropertyBlock(m_wetBlock, p.slot);
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Wet look could not be taken off: " + ex.Message);
            }
            m_wetParts.Clear();
            m_wetApplied = -1f;
        }

        // Runs every frame: she sits on him and he gets wetter over a few seconds; she is off and he dries over a while.
        private void UpdateWet(float dt)
        {
            try
            {
                bool want = Plugin.RideWet.Value && m_heroReady && m_ride && m_toggled && m_heroW > 0.95f;
                if (!Plugin.RideWet.Value)
                {
                    m_wet = Mathf.MoveTowards(m_wet, 0f, dt / 2f);
                }
                else
                {
                    m_wet = Mathf.Clamp01(m_wet + dt * (want ? 1f / 5f : -1f / 40f));
                }
                if (m_wetParts.Count == 0)
                {
                    return;
                }
                float level = Mathf.SmoothStep(0f, 1f, m_wet) * Plugin.RideWetShine.Value;
                if (m_wet <= 0.001f)
                {
                    WetRestore();
                    return;
                }
                if (Mathf.Abs(level - m_wetApplied) > 0.004f)
                {
                    WetApply(level);
                    m_wetApplied = level;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Wet look failed: " + ex.Message);
                m_wet = 0f;
                WetRestore();
            }
        }
    }
}
