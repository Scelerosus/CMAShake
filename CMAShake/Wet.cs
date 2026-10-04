using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;
using HerShine = Assets._ReusableScripts.CuchiCuchi.AI.Controladores.ControlladorBrilloPorPlacer;

namespace CMAShake
{
    // The hero's penis gets wet from her while she rides him.
    //
    // Where the wetness comes from: inside her. The game keeps how wet her vagina is (ControlladorBrilloPorPlacer, which it
    // raises with her pleasure). While she rides she is asked, with the game's own switch (its "least wetness" value), to
    // get wet, and what she really has is read back. He gets wet only while he is in her, the more the deeper and the
    // wetter she is; her sweat plays no part. Out of her, he dries slowly.
    //
    // What it looks like: a smooth, glossy coat of her fluid of the plugin's own, laid over the penis: a second renderer on
    // the same mesh and bones with a transparent material drawn here (no texture of the game's is used). It is an even
    // slick sheen with no drops or beads on it, which would read as sweat. Under it the skin itself is made a little
    // glossier, as a per-renderer override that is taken off again.
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

        private sealed class WetFilm
        {
            public SkinnedMeshRenderer source;
            public SkinnedMeshRenderer film;
            public GameObject go;
            public int shapes;
            public float[] last;
        }

        // Smoothness (glossiness) properties of the shaders the game is likely to use, with how glossy a wet surface is on each.
        private static readonly string[] WetFloats = { "_Smoothness", "_Glossiness", "_GlossMapScale", "_SmoothnessRemapMax", "_SmoothnessRemapMin", "_SmoothnessMax", "_SmoothnessMin" };
        private static readonly float[] WetFloatTo = { 0.9f, 0.9f, 1f, 1f, 0.5f, 1f, 0.5f };
        private static readonly Regex WetMaterialName = new Regex("penis|(?<![a-z])pene|genit|(?<![a-z])dick|glans|(?<![a-z])shaft|scrot|testic", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WetNotSkin = new Regex("x-?ray|internal", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private const string HerWetId = "CMAShake.Wet";

        private readonly List<WetPart> m_wetParts = new List<WetPart>();
        private readonly List<WetFilm> m_wetFilms = new List<WetFilm>();
        private float m_wet;
        private float m_wetApplied = -1f;
        private bool m_wetCaptured;
        private MaterialPropertyBlock m_wetBlock;

        // The film's material and its drawn textures are made once and kept.
        private static Material s_filmMaterial;
        private static bool s_filmNormal, s_filmMask, s_filmPreserve, s_filmFailed;
        private static Texture2D s_filmCover, s_filmNormalTex, s_filmMaskTex;

        // Her side: the game's own record of how wet she is, and the request to her to get wet.
        private HerShine m_herShine;
        private float m_herTargetBefore, m_herCheckAt;
        private bool m_herAskOk;
        private float m_herAskW, m_herAskSet = -1f;
        private float m_herWet;
        private float m_herReadAt, m_herLogAt;

        // Finds what to make wet, on the hero as he is taken up, and her wetness, on her. Called once for each ride.
        private void WetCapture(Character hero)
        {
            WetRestore();
            m_wetCaptured = Plugin.RideWet.Value;
            if (!Plugin.RideWet.Value)
            {
                // Nothing is asked of the woman of the ride before either.
                ForgetHerAsk();
                m_herShine = null;
                m_herAskW = 0f;
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
                        // The whole of a renderer that is the penis, or any material named for it on another renderer.
                        // The game keeps see-through copies of it for its x-ray view (Pene.xRay, Pene.Internals): those are not skin.
                        if (WetNotSkin.IsMatch(r.name))
                        {
                            continue;
                        }
                        bool byRenderer = WetMaterialName.IsMatch(r.name);
                        if (names.Length < 900)
                        {
                            names.Append('\'').Append(r.name).Append("'[");
                            for (int i = 0; i < mats.Length; i++)
                            {
                                names.Append(i > 0 ? "," : "").Append(mats[i] != null ? mats[i].name : "-");
                            }
                            names.Append("] ");
                        }
                        bool any = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            Material m = mats[i];
                            if (m != null && (byRenderer || WetMaterialName.IsMatch(m.name)))
                            {
                                any = true;
                                WetPart part = WetPartFor(r, i, m);
                                if (part != null)
                                {
                                    m_wetParts.Add(part);
                                }
                            }
                        }
                        if (any && byRenderer)
                        {
                            AddWetFilm(r);
                        }
                    }
                }
                if (m_wetParts.Count == 0 && m_wetFilms.Count == 0)
                {
                    Plugin.ModLog.LogInfo($"Ride: wet: no penis material among {renderers} renderer(s) of the hero, nothing is made wet. Renderers: {names}");
                }
            }
            catch (Exception ex)
            {
                WetRestore();
                Plugin.ModLog.LogWarning("Wet look could not be prepared: " + ex.Message);
            }
            FindHerWetness();
        }

        private WetPart WetPartFor(Renderer r, int slot, Material m)
        {
            var part = new WetPart { renderer = r, slot = slot, name = m.name };
            var found = new StringBuilder();
            string shader = m.shader != null ? m.shader.name : "?";

            // What is already overridden: on this slot (kept, and built upon), or on the whole renderer (then a block of this
            // slot's own would hide the game's, so the skin under the film is left as it is).
            var own = new MaterialPropertyBlock();
            r.GetPropertyBlock(own, slot);
            part.hadBlock = !own.isEmpty;
            if (!part.hadBlock)
            {
                var whole = new MaterialPropertyBlock();
                r.GetPropertyBlock(whole);
                if (!whole.isEmpty)
                {
                    Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}' (renderer '{r.name}', slot {slot}) has overrides of the game's on the whole renderer; its skin is left alone, only the film is laid over it.");
                    return null;
                }
            }
            for (int i = 0; i < WetFloats.Length; i++)
            {
                int id = Shader.PropertyToID(WetFloats[i]);
                if (m.HasProperty(id))
                {
                    float orig = own.HasFloat(id) ? own.GetFloat(id) : m.GetFloat(id);
                    part.floatIds.Add(id);
                    part.floatOrig.Add(orig);
                    part.floatTarget.Add(WetFloatTo[i]);
                    found.Append(WetFloats[i]).Append('=').Append(orig.ToString("F2")).Append(' ');
                }
            }
            foreach (string c in new[] { "_BaseColor", "_Color" })
            {
                int id = Shader.PropertyToID(c);
                if (m.HasProperty(id))
                {
                    part.colorId = id;
                    part.colorOrig = own.HasColor(id) ? own.GetColor(id) : m.GetColor(id);
                    found.Append(c).Append(' ');
                    break;
                }
            }
            if (part.floatIds.Count == 0)
            {
                Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}' (renderer '{r.name}', slot {slot}, shader '{shader}') has none of the known gloss properties; only the film is laid over it.");
                return null;
            }
            Plugin.ModLog.LogInfo($"Ride: wet: '{m.name}' (renderer '{r.name}', slot {slot}, shader '{shader}') skin gloss through: {found}");
            return part;
        }

        // ---- the film: the plugin's own fluid, drawn over the penis ----

        // A second renderer on the same mesh and bones, with the film's material on every part of it.
        private void AddWetFilm(Renderer r)
        {
            try
            {
                var source = r.TryCast<SkinnedMeshRenderer>();
                if (source == null || source.sharedMesh == null)
                {
                    Plugin.ModLog.LogInfo($"Ride: wet film: '{r.name}' is not a skinned mesh, no film is laid over it.");
                    return;
                }
                Material material = FilmMaterial();
                if (material == null)
                {
                    return;
                }
                var go = new GameObject("CMAShake wet film");
                go.hideFlags = HideFlags.DontSave;
                go.layer = source.gameObject.layer;
                go.transform.SetParent(source.transform, false);
                var film = go.AddComponent<SkinnedMeshRenderer>();
                film.enabled = false;
                film.sharedMesh = source.sharedMesh;
                film.bones = source.bones;
                film.rootBone = source.rootBone;
                film.localBounds = source.localBounds;
                film.updateWhenOffscreen = source.updateWhenOffscreen;
                film.quality = source.quality;
                film.renderingLayerMask = source.renderingLayerMask;
                // Lit the way the skin under it is.
                film.probeAnchor = source.probeAnchor;
                film.lightProbeUsage = source.lightProbeUsage;
                film.reflectionProbeUsage = source.reflectionProbeUsage;
                film.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                int parts = Mathf.Max(1, source.sharedMesh.subMeshCount);
                var mats = new Material[parts];
                for (int i = 0; i < parts; i++)
                {
                    mats[i] = material;
                }
                film.sharedMaterials = mats;
                m_wetFilms.Add(new WetFilm { source = source, film = film, go = go, shapes = source.sharedMesh.blendShapeCount });
                Plugin.ModLog.LogInfo($"Ride: wet film: laid over '{r.name}' ({parts} part(s), {source.sharedMesh.blendShapeCount} blend shape(s), {source.bones.Length} bone(s)).");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Wet film could not be laid: " + ex.Message);
            }
        }

        // The film's material. It is the plugin's own, with its own drawn textures. The game was built with only the kinds of
        // its shader that its own materials use, so the kind to use (transparent, with or without a normal map) is taken
        // from a transparent material that is already loaded: that one is sure to be drawn. Only a plain one will do (blended
        // by its alpha, drawn with the other transparent things, nothing bent or shifted by it); with none there is no film,
        // because one set up from nothing could come out as a solid white skin.
        private static Material FilmMaterial()
        {
            if (s_filmMaterial != null)
            {
                return s_filmMaterial;
            }
            if (s_filmFailed)
            {
                return null;
            }
            try
            {
                Material like = null;
                int bestScore = int.MinValue;
                string bestWords = "";
                int surface = Shader.PropertyToID("_SurfaceType");
                int blend = Shader.PropertyToID("_BlendMode");
                int distortion = Shader.PropertyToID("_DistortionEnable");
                int seen = 0;
                var all = Resources.FindObjectsOfTypeAll<Material>();
                foreach (Material m in all)
                {
                    if (m == null || m.shader == null || m.shader.name != "HDRP/Lit" || !m.HasProperty(surface) || m.GetFloat(surface) < 0.5f)
                    {
                        continue;
                    }
                    seen++;
                    // Blend mode 0 is alpha: an additive one would glow, a premultiplied one would be a white sheath.
                    if ((m.HasProperty(blend) && m.GetFloat(blend) > 0.5f) || m.renderQueue < 2900 || m.renderQueue > 3100
                        || (m.HasProperty(distortion) && m.GetFloat(distortion) > 0.5f))
                    {
                        continue;
                    }
                    string words = string.Join(" ", (string[])m.shaderKeywords);
                    if (!words.Contains("_SURFACE_TYPE_TRANSPARENT") || words.Contains("_REFRACTION") || words.Contains("_MAPPING_")
                        || words.Contains("_DEPTHOFFSET_ON") || words.Contains("_ALPHATEST_ON"))
                    {
                        continue;
                    }
                    int score = 10;
                    score += words.Contains("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING") ? 6 : 0;
                    score += words.Contains("_NORMALMAP") ? 4 : 0;
                    score += words.Contains("_MASKMAP") ? 2 : 0;
                    score -= words.Contains("SUBSURFACE") || words.Contains("TRANSMISSION") ? 3 : 0;
                    score -= words.Contains("_DETAIL_MAP") ? 3 : 0;
                    score -= words.Contains("_EMISSIVE_COLOR_MAP") ? 3 : 0;
                    score -= words.Contains("DISPLACEMENT") || words.Contains("_HEIGHTMAP") ? 4 : 0;
                    score -= words.Contains("_THICKNESSMAP") || words.Contains("_ANISOTROPYMAP") || words.Contains("_IRIDESCENCE") ? 2 : 0;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        like = m;
                        bestWords = words;
                    }
                }
                if (like == null)
                {
                    // Not kept as a failure: another scene may have one.
                    Plugin.ModLog.LogInfo($"Ride: wet film: none of the {seen} transparent HDRP/Lit material(s) loaded is a plain one to go by, so no film is laid (the skin gloss is still used).");
                    return null;
                }

                Material film = new Material(like);
                film.renderQueue = 3000;
                s_filmNormal = bestWords.Contains("_NORMALMAP");
                s_filmMask = bestWords.Contains("_MASKMAP");
                s_filmPreserve = bestWords.Contains("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                Plugin.ModLog.LogInfo($"Ride: wet film: own material, drawn the way '{like.name}' is (score {bestScore}, of {seen} transparent; {bestWords}).");
                film.name = "CMAShake wet film";
                film.hideFlags = HideFlags.HideAndDontSave;

                MakeFilmTextures();
                film.SetTexture("_BaseColorMap", s_filmCover);
                film.SetTextureScale("_BaseColorMap", Vector2.one);
                film.SetTextureOffset("_BaseColorMap", Vector2.zero);
                film.SetTexture("_NormalMap", s_filmNormal ? s_filmNormalTex : null);
                film.SetTexture("_MaskMap", s_filmMask ? s_filmMaskTex : null);
                SetIfHas(film, "_Metallic", 0f);
                SetIfHas(film, "_AlphaCutoff", 0f);
                SetIfHas(film, "_ZWrite", 0f);
                SetIfHas(film, "_TransparentZWrite", 0f);
                SetIfHas(film, "_ZTestTransparent", 4f);
                SetIfHas(film, "_UVBase", 0f);
                SetIfHas(film, "_DetailAlbedoScale", 0f);
                SetIfHas(film, "_DetailNormalScale", 0f);
                SetIfHas(film, "_DetailSmoothnessScale", 0f);
                SetIfHas(film, "_AORemapMin", 1f);
                SetIfHas(film, "_AORemapMax", 1f);
                SetIfHas(film, "_CoatMask", 0f);
                SetIfHas(film, "_Anisotropy", 0f);
                SetIfHas(film, "_IridescenceMask", 0f);
                if (film.HasProperty("_EmissiveColor"))
                {
                    film.SetColor("_EmissiveColor", Color.black);
                }
                s_filmMaterial = film;
                FilmLook(0f);
                return film;
            }
            catch (Exception ex)
            {
                s_filmFailed = true;
                Plugin.ModLog.LogWarning("Wet film material could not be made: " + ex.Message);
                return null;
            }
        }

        private static void SetIfHas(Material m, string name, float value)
        {
            if (m.HasProperty(name))
            {
                m.SetFloat(name, value);
            }
        }

        // How the film looks at a wetness between 0 (nothing) and 1: a thicker and glossier coat.
        private static void FilmLook(float level)
        {
            Material m = s_filmMaterial;
            if (m == null)
            {
                return;
            }
            // Fluid has almost no colour of its own: it darkens the skin a little and shines. (A light colour here would lie
            // on the skin like lotion.) Where the highlights are not kept apart from the alpha, more of it is needed to show.
            m.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.035f, Mathf.Lerp(0f, s_filmPreserve ? 0.16f : 0.35f, level)));
            SetIfHas(m, "_Smoothness", Mathf.Lerp(0.2f, 0.95f, level));
            SetIfHas(m, "_SmoothnessRemapMin", Mathf.Lerp(0.1f, 0.6f, level));
            SetIfHas(m, "_SmoothnessRemapMax", Mathf.Lerp(0.25f, 0.98f, level));
            SetIfHas(m, "_NormalScale", Mathf.Lerp(0.05f, 0.25f, level));
        }

        // The film's textures, drawn here: an even coat of fluid, a little thicker in some places than in others, with only
        // broad, gentle ripples in it to catch the light. No drops or beads: on him they look like sweat, not like her.
        // From one field of thickness come the cover (how much fluid is where), the normal map and the gloss mask.
        private static void MakeFilmTextures()
        {
            if (s_filmCover != null && s_filmNormalTex != null && s_filmMaskTex != null)
            {
                return;
            }
            const int size = 256;
            var height = new float[size * size];
            var rnd = new System.Random(20261);
            // A few long, soft waves with whole periods across the texture, so it repeats without a seam.
            const int waves = 7;
            var fx = new int[waves];
            var fy = new int[waves];
            var amp = new float[waves];
            var shift = new float[waves];
            for (int k = 0; k < waves; k++)
            {
                fx[k] = rnd.Next(-3, 4);
                fy[k] = rnd.Next(1, 5);
                amp[k] = 1f / (1f + Mathf.Sqrt(fx[k] * fx[k] + fy[k] * fy[k]));
                shift[k] = (float)rnd.NextDouble() * Mathf.PI * 2f;
            }
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float h = 0f;
                    for (int k = 0; k < waves; k++)
                    {
                        h += amp[k] * Mathf.Sin(2f * Mathf.PI * (fx[k] * x + fy[k] * y) / size + shift[k]);
                    }
                    height[y * size + x] = h;
                    lowest = Mathf.Min(lowest, h);
                    highest = Mathf.Max(highest, h);
                }
            }
            float span = Mathf.Max(1e-4f, highest - lowest);

            var cover = new Color32[size * size];
            var normal = new Color32[size * size];
            var mask = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float hl = height[y * size + (x + size - 1) % size], hr = height[y * size + (x + 1) % size];
                    float hd = height[((y + size - 1) % size) * size + x], hu = height[((y + 1) % size) * size + x];
                    // Very gentle slopes: the coat is smooth, it only bends the highlights a little.
                    var n = new Vector3(-(hr - hl) * 4f, -(hu - hd) * 4f, 1f).normalized;
                    float thick = (height[i] - lowest) / span;
                    // Normal maps are read as x from red times alpha and y from green.
                    normal[i] = new Color32((byte)(Mathf.Clamp01(n.x * 0.5f + 0.5f) * 255f), (byte)(Mathf.Clamp01(n.y * 0.5f + 0.5f) * 255f), (byte)(Mathf.Clamp01(n.z * 0.5f + 0.5f) * 255f), 255);
                    cover[i] = new Color32(255, 255, 255, (byte)(Mathf.Lerp(0.75f, 1f, thick) * 255f));
                    // Mask: no metal, no occlusion, glossy all over and a touch more where the coat is thicker.
                    mask[i] = new Color32(0, 255, 255, (byte)(Mathf.Lerp(0.85f, 1f, thick) * 255f));
                }
            }
            s_filmCover = FilmTexture(size, cover, false);
            s_filmNormalTex = FilmTexture(size, normal, true);
            s_filmMaskTex = FilmTexture(size, mask, true);
        }

        private static Texture2D FilmTexture(int size, Color32[] pixels, bool linear)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear);
            t.SetPixels32(pixels);
            t.Apply(true, true);
            t.filterMode = FilterMode.Trilinear;
            t.wrapMode = TextureWrapMode.Repeat;
            t.anisoLevel = 4;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        // ---- her side ----

        private void FindHerWetness()
        {
            // A request left with the woman of the ride before is taken away first.
            ForgetHerAsk();
            m_herShine = null;
            m_herAskW = 0f;
            m_herAskOk = false;
            m_herCheckAt = 0f;
            m_herWet = 0f;
            m_herLogAt = 0f;
            try
            {
                if (m_rideWoman == null || m_rideWoman.character == null)
                {
                    return;
                }
                m_herShine = m_rideWoman.character.GetComponentInChildren<HerShine>(true);
                if (m_herShine == null)
                {
                    Plugin.ModLog.LogInfo("Ride: wet: the game's record of her wetness was not found on her; he gets wet at a steady pace instead.");
                    return;
                }
                var least = m_herShine.minVagWeight;
                // A "least value" whose requests start at 0 is one that takes the greatest of them: asking is safe.
                m_herAskOk = least != null && least.defaultValueDeModificadorAlInstanciar < 0.5f;
                var now = m_herShine.m_currentBrillos;
                Plugin.ModLog.LogInfo($"Ride: wet: her wetness is read from the game (now: vagina {now.vag:F2}, skin {now.main:F2}); asking her to get wet is {(m_herAskOk ? "possible" : "not possible, left alone")}.");
            }
            catch (Exception ex)
            {
                m_herShine = null;
                Plugin.ModLog.LogWarning("Her wetness could not be found: " + ex.Message);
            }
        }

        // While she sits on him she is asked to get wet, a little more with every second; off him the request fades and
        // is taken away. What she really has, by the game, is read a few times a second.
        private void HerWetStep(float dt, bool contact)
        {
            if (m_herShine == null)
            {
                // Nothing to read: taken as wet while she rides.
                m_herWet = contact ? 1f : 0f;
                m_herAskW = 0f;
                m_herAskSet = -1f;
                return;
            }
            try
            {
                m_herAskW = Mathf.MoveTowards(m_herAskW, contact ? 1f : 0f, dt / (contact ? 12f : 25f));
                bool read = Time.unscaledTime >= m_herReadAt;
                if (m_herAskW <= 0.005f)
                {
                    if (m_herAskSet >= 0f)
                    {
                        ForgetHerAsk();
                    }
                }
                else if (m_herAskOk && (read || Mathf.Abs(m_herAskW - m_herAskSet) > 0.01f))
                {
                    // The request is looked up again every time (the game may have thrown its requests away and made new
                    // ones), and written again a few times a second for the same reason.
                    var least = m_herShine.minVagWeight;
                    if (m_herAskSet < 0f)
                    {
                        // The first time: what she was aiming at before, to see a moment later what the request did.
                        m_herTargetBefore = m_herShine.m_targetBrillos.vag;
                        m_herCheckAt = Time.unscaledTime + 1.5f;
                        Plugin.ModLog.LogInfo($"Ride: wet: asking her to get wet ({least.Count} other request(s) on that value, her target before {m_herTargetBefore:F2}).");
                    }
                    var ask = least.ObtenerModificadorNotNull(HerWetId);
                    var value = ask.valor;
                    value.valor = m_herAskW;
                    ask.valor = value;
                    m_herAskSet = m_herAskW;
                }
                if (m_herCheckAt > 0f && Time.unscaledTime >= m_herCheckAt)
                {
                    // Did asking raise what she aims at? If it lowered it instead, the value is not the floor it was taken
                    // for, and she is left alone from then on.
                    m_herCheckAt = 0f;
                    float after = m_herShine.m_targetBrillos.vag;
                    bool lowered = after < m_herTargetBefore - 0.01f;
                    Plugin.ModLog.LogInfo($"Ride: wet: her target went {m_herTargetBefore:F2} -> {after:F2} with {m_herAskSet:F2} asked: {(lowered ? "asking LOWERS it, she is left alone" : after > m_herTargetBefore + 0.01f ? "asking works" : "no change yet")}.");
                    if (lowered)
                    {
                        ForgetHerAsk();
                        m_herAskOk = false;
                    }
                }
                if (read)
                {
                    m_herReadAt = Time.unscaledTime + 0.25f;
                    var now = m_herShine.m_currentBrillos;
                    var config = m_herShine.config;
                    float maxVag = config != null && config.maxBrilloVag > 0.01f ? config.maxBrilloVag : 1f;
                    float maxSkin = config != null && config.maxBrilloCuerpo > 0.01f ? config.maxBrilloCuerpo : 1f;
                    // Only her vagina: that is what he is in. Her sweat does not make him wet.
                    m_herWet = Mathf.Clamp01(now.vag / maxVag);
                    if (contact && m_herLogAt == 0f)
                    {
                        m_herLogAt = Time.unscaledTime + 15f;
                    }
                    if (m_herLogAt > 0f && Time.unscaledTime >= m_herLogAt)
                    {
                        m_herLogAt = -1f;
                        var target = m_herShine.m_targetBrillos;
                        Plugin.ModLog.LogInfo($"Ride: wet: after 15 s on him: her vagina {now.vag:F2} (target {target.vag:F2}, most {maxVag:F2}), her skin {now.main:F2} (most {maxSkin:F2}), asked {m_herAskW:F2}; taken as {m_herWet:F2}, he is {m_wet:F2} wet.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Her wetness could not be read: " + ex.Message);
                ForgetHerAsk();
                m_herShine = null;
                m_herAskW = 0f;
            }
        }

        private void ForgetHerAsk()
        {
            try
            {
                if (m_herShine != null && m_herAskSet >= 0f)
                {
                    m_herShine.minVagWeight.RemoverModificador(HerWetId);
                }
            }
            catch (Exception)
            {
            }
            m_herAskSet = -1f;
        }

        // ---- putting it on and taking it off ----

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
                    float f = Mathf.Lerp(1f, 0.92f, level);
                    m_wetBlock.SetColor(p.colorId, new Color(c.r * f, c.g * f, c.b * f, c.a));
                }
                p.renderer.SetPropertyBlock(m_wetBlock, p.slot);
            }
            FilmLook(level);
        }

        // The film is seen when there is something to see and the penis itself is: it follows its shape every frame.
        private void FilmFollow(float level)
        {
            foreach (WetFilm f in m_wetFilms)
            {
                if (f.film == null || f.source == null)
                {
                    continue;
                }
                bool show = level > 0.01f && f.source.enabled && !f.source.forceRenderingOff
                    && f.source.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (f.film.enabled != show)
                {
                    f.film.enabled = show;
                }
                if (!show)
                {
                    continue;
                }
                if (f.film.sharedMesh != f.source.sharedMesh)
                {
                    // The game gave him another mesh (a change of size, of clothes): the film takes it too.
                    f.film.sharedMesh = f.source.sharedMesh;
                    f.film.bones = f.source.bones;
                    f.film.rootBone = f.source.rootBone;
                    f.film.localBounds = f.source.localBounds;
                    f.shapes = f.source.sharedMesh != null ? f.source.sharedMesh.blendShapeCount : 0;
                    f.last = null;
                }
                if (f.last == null || f.last.Length != f.shapes)
                {
                    f.last = new float[f.shapes];
                    for (int i = 0; i < f.shapes; i++)
                    {
                        f.last[i] = float.NaN;
                    }
                }
                for (int i = 0; i < f.shapes; i++)
                {
                    float weight = f.source.GetBlendShapeWeight(i);
                    if (weight != f.last[i])
                    {
                        f.last[i] = weight;
                        f.film.SetBlendShapeWeight(i, weight);
                    }
                }
            }
        }

        // Once more at the end of the frame: the game may shape him after the film was first set, and the film would lag.
        private void WetLate()
        {
            if (m_wetFilms.Count == 0 || m_wetApplied <= 0.01f)
            {
                return;
            }
            try
            {
                FilmFollow(m_wetApplied);
            }
            catch (Exception)
            {
            }
        }

        // Takes the wet look off: what the renderer had before is put back, nothing is left on it if it had nothing, and
        // the film is removed.
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
                foreach (WetFilm f in m_wetFilms)
                {
                    if (f.go != null)
                    {
                        UnityEngine.Object.Destroy(f.go);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Wet look could not be taken off: " + ex.Message);
            }
            m_wetParts.Clear();
            m_wetFilms.Clear();
            m_wetApplied = -1f;
        }

        // Runs every frame: while he is in her he gets wet, the faster the deeper he goes and the wetter she is; out of her he dries.
        private void UpdateWet(float dt)
        {
            try
            {
                if (Plugin.RideWet.Value && m_heroReady && m_hero != null && !m_wetCaptured)
                {
                    // Switched on in the middle of a ride: what to make wet is found now.
                    WetCapture(m_hero);
                }
                // How deep he is in her this frame, as the ride worked it out (nothing if it did not run just now).
                float inHer = Plugin.RideWet.Value && m_heroReady && m_ride && m_toggled && !m_switching && m_heroW > 0.95f
                    && Time.frameCount - m_rideInFrame <= 1 ? m_rideIn : 0f;
                bool contact = inHer > 0.01f;
                HerWetStep(dt, contact);
                if (!Plugin.RideWet.Value)
                {
                    m_wet = Mathf.MoveTowards(m_wet, 0f, dt / 2f);
                }
                else if (contact)
                {
                    // Inside her he is never quite dry: a quarter is the least that she leaves on him.
                    m_wet = Mathf.Clamp01(m_wet + dt * inHer * Mathf.Max(m_herWet, 0.25f) / 5f);
                }
                else
                {
                    m_wet = Mathf.Clamp01(m_wet - dt / 45f);
                }
                if (m_wetParts.Count == 0 && m_wetFilms.Count == 0)
                {
                    if (m_wet <= 0.001f && m_herAskW <= 0.005f && m_herAskSet >= 0f)
                    {
                        ForgetHerAsk();
                    }
                    return;
                }
                // Dry and the ride over: everything is taken off. Dry while he is still under her (the ride has only just
                // begun): it all stays, unseen, for her to make wet.
                if (m_wet <= 0.001f && !m_heroReady)
                {
                    WetRestore();
                    return;
                }
                float level = Mathf.SmoothStep(0f, 1f, m_wet) * Plugin.RideWetShine.Value;
                if (Mathf.Abs(level - m_wetApplied) > 0.004f)
                {
                    WetApply(level);
                    m_wetApplied = level;
                }
                FilmFollow(level);
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
