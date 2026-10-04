using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace CMAShake
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Corrupted Modeling Agency.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "community.cma.shake";
        public const string PluginName = "CMA Shake";
        public const string PluginVersion = "0.11.0";

        internal static ManualLogSource ModLog;
        internal static ConfigFile Cfg;
        internal static ConfigEntry<KeyCode> MenuKey;

        internal static ConfigEntry<KeyCode> ToggleKey;
        internal static ConfigEntry<bool> ShowNotice;

        internal static ConfigEntry<KeyCode> RideKey;
        internal static ConfigEntry<float> RideFrequency;
        internal static ConfigEntry<float> RideSquat;
        internal static ConfigEntry<float> RideStance;
        internal static ConfigEntry<float> RideLean;
        internal static ConfigEntry<float> RideBounce;
        internal static ConfigEntry<float> RideGrind;
        internal static ConfigEntry<float> RideTilt;
        internal static ConfigEntry<bool> RideHero;
        internal static ConfigEntry<bool> RideHeroSolid;
        internal static ConfigEntry<string> RideHandsOn;
        internal static ConfigEntry<string> RideFacing;
        internal static ConfigEntry<string> RidePoseType;
        internal static ConfigEntry<string> TwerkPoseType;
        internal static ConfigEntry<string> ActiveProfile;
        internal static ConfigEntry<float> RideLeanBack;
        internal static ConfigEntry<float> RideSpring;
        internal static ConfigEntry<string> Rhythm;
        internal static ConfigEntry<string> PresetName;
        internal static ConfigEntry<KeyCode> PresetKey;
        internal static ConfigEntry<float> RideSink;
        internal static ConfigEntry<bool> RideHeroControllers;
        internal static ConfigEntry<float> RideHeroClearance;
        internal static ConfigEntry<float> RideHeroShift;
        internal static ConfigEntry<bool> RideWet;
        internal static ConfigEntry<float> RideWetShine;
        internal static ConfigEntry<KeyCode> BikiniKey;
        internal static ConfigEntry<bool> BikiniWithTwerk;
        internal static ConfigEntry<string> BikiniTop;
        internal static ConfigEntry<string> BikiniBottom;
        internal static ConfigEntry<string> BikiniColor;
        internal static ConfigEntry<bool> BikiniSolid;
        internal static ConfigEntry<float> LeanDegrees;
        internal static ConfigEntry<float> Stance;
        internal static ConfigEntry<float> ToesOut;
        internal static ConfigEntry<float> Arch;
        internal static ConfigEntry<float> LeanExtra;
        internal static ConfigEntry<float> MoveSeconds;
        internal static ConfigEntry<float> SwayWidth;
        internal static ConfigEntry<float> CircleRadius;
        internal static ConfigEntry<float> RollDegrees;
        internal static ConfigEntry<float> SquatDepth;
        internal static ConfigEntry<float> HipBack;
        internal static ConfigEntry<string> HandsOn;
        internal static ConfigEntry<float> HipWidth;
        internal static ConfigEntry<float> HipHeight;
        internal static ConfigEntry<float> HipForward;
        internal static ConfigEntry<float> ElbowBack;
        internal static ConfigEntry<float> PoseRamp;
        internal static ConfigEntry<float> ShakeDelay;

        internal static ConfigEntry<float> Frequency;
        internal static ConfigEntry<float> BounceHeight;
        internal static ConfigEntry<float> TiltDegrees;
        internal static ConfigEntry<float> ButtAmplitude;
        internal static ConfigEntry<bool> ShakeChest;
        internal static ConfigEntry<float> Smoothness;
        internal static ConfigEntry<bool> RideAutoMatch;
        internal static ConfigEntry<float> ChestAmplitude;

        public override void Load()
        {
            ModLog = Log;
            Cfg = Config;
            // The settings window changes values while a slider is dragged: write the file when it is let go, not on every step.
            Config.SaveOnConfigSet = false;

            MenuKey = Config.Bind("Input", "MenuKey", KeyCode.J, "Key that opens or closes the settings window (bikini, colour, twerk and ride settings).");
            ToggleKey = Config.Bind("Input", "ToggleKey", KeyCode.H, "Key that starts or stops the twerk.");
            ShowNotice = Config.Bind("Input", "ShowNotice", true, "Show an on-screen notice while twerking.");

            RideKey = Config.Bind("Ride", "Key", KeyCode.L, "Key that starts or stops the ride: your view drops low in front of her and she rides you.");
            RideFrequency = Config.Bind("Ride", "Frequency", 1.7f, "Strokes per second (the tempo drifts around this).");
            RideSquat = Config.Bind("Ride", "Squat", 0.64f, "How far her hips drop, in metres. The feet stay planted and the knees fold. She cannot go lower than the hero's body: she is held up by it.");
            RideSink = Config.Bind("Ride", "SinkIntoHim", 0.04f, "How far she may sink into the hero's body, in metres, as soft bodies give: 0 keeps her just touching him, more lets her settle lower onto him.");
            RideHandsOn = Config.Bind("Ride", "HandsOn", "Hero", "Where her hands rest during the ride: Hero (on his chest), HeroThighs (on his thighs behind her, and she leans back to reach them) or Thighs (on her own thighs).");
            RideFacing = Config.Bind("Ride", "Facing", "Face", "Which way she faces during the ride: Face (toward the hero, his head under her face) or Away (reverse: her back to him, his head behind her, her hands on his thighs or knees in front).");
            RidePoseType = Config.Bind("Ride", "PoseType", "", "The ride pose chosen last in the window: Cowgirl, Lean back, Upright or Reverse (empty until one is chosen). Choosing one sets the hands, the facing and the lean.");
            TwerkPoseType = Config.Bind("Pose", "Type", "", "The twerk pose chosen last in the window: Bent over, Deep squat, Hands on hips or Low arch (empty until one is chosen). Choosing one sets the lean, the arch, the squat, the stance and the hands.");
            ActiveProfile = Config.Bind("Profiles", "Active", "", "The name of the profile loaded or saved last in the window.");
            RideLeanBack = Config.Bind("Ride", "LeanBack", 15f, "How far she leans back, in degrees, when her hands are on his thighs behind her.");
            RideSpring = Config.Bind("Ride", "Springiness", 0.6f, "How springy her landing on him is: 0 settles onto him smoothly, 1 sinks in a little on every stroke and springs back, so that she has weight.");
            Rhythm = Config.Bind("Behavior", "Rhythm", "Scenario", "The rhythm of the motion: Steady (one bounce), Random (a move chosen now and then) or Scenario (a fixed sequence that loops: a slow circle, bounces, a pause, a faster stretch, side to side).");
            PresetName = Config.Bind("Behavior", "Preset", "Mine", "Soft (slower and gentler), Fast (quicker) or Mine (your own settings exactly as they are). The preset only changes the speed and the depth of the motion on top of them.");
            PresetKey = Config.Bind("Input", "PresetKey", KeyCode.P, "Key that switches to the next preset: Soft, Fast, Mine.");
            RideStance = Config.Bind("Ride", "Stance", 0.22f, "How far each foot is moved out to the side, in metres.");
            RideLean = Config.Bind("Ride", "Lean", 40f, "How far she leans forward over you, in degrees.");
            RideBounce = Config.Bind("Ride", "Bounce", 0.09f, "How far her hips rise and fall, in metres.");
            RideGrind = Config.Bind("Ride", "Grind", 0.06f, "How far her hips rock forward and back, in metres.");
            RideTilt = Config.Bind("Ride", "Tilt", 9f, "How much her pelvis rocks with each stroke, in degrees.");
            RideHero = Config.Bind("Ride", "Hero", true, "Carry the hero (the male character your view is attached to) with the camera: he lies on his back under her for the ride and goes back to where he was when it ends.");
            RideHeroClearance = Config.Bind("Ride", "HeroClearance", 0.02f, "How far above the floor the hero's back rests while he lies down, in metres. Raise it if he sinks into the floor, lower it (even below zero) if he floats.");
            RideHeroShift = Config.Bind("Ride", "HeroShift", 0f, "Moves the hero along his own body, in metres (positive = toward his head).");
            RideWet = Config.Bind("Ride", "Wet", true, "The hero's penis gets a wet, glossy look while she rides him, building up over a few seconds and drying slowly after. Only the look of its material changes (a per-renderer override that is taken off again), nothing of the game's own.");
            RideWetShine = Config.Bind("Ride", "WetShine", 0.8f, "How wet it looks at most: 0 none, 1 as glossy as the material can be.");
            RideHeroSolid = Config.Bind("Ride", "HeroSolid", true, "Carry the hero's physical body with him: every frame his ragdoll (with the colliders the game gave him) is put on his root with the puppet's own teleport, so it lies where he lies. Off leaves it to be pulled there by the puppet's forces, which lags and can knock him over.");
            RideHeroControllers = Config.Bind("Ride", "HeroControllers", true, "Turn off the hero's own movement controllers for the ride, with the game's own switch (it does the same in its spa and sex scenes). Left on, they treat him lying on the floor as a man falling and pull him down through it.");
            BikiniKey = Config.Bind("Bikini", "Key", KeyCode.K, "Key that puts her in the bikini, or back in her clothes.");
            BikiniWithTwerk = Config.Bind("Bikini", "WithTwerk", false, "Put the bikini on automatically when the twerk starts, and take it off when it stops.");
            BikiniTop = Config.Bind("Bikini", "Top", "microbikinitop, bikinitop", "Part of the in-game name or id of the top, from the installed clothing mods. Several can be listed, comma separated; the first that exists is used.");
            BikiniBottom = Config.Bind("Bikini", "Bottom", "bikinibot, tanga, gstring", "Part of the in-game name or id of the bottom. Several can be listed; the first that exists is used.");
            BikiniColor = Config.Bind("Bikini", "Color", "#D81B60", "Colour of the bikini as #RRGGBB.");
            BikiniSolid = Config.Bind("Bikini", "SolidColor", true, "Use a plain colour instead of the item's own texture.");
            LeanDegrees = Config.Bind("Pose", "LeanDegrees", 58f, "How far she bends forward, in degrees (mostly hinged at the hips).");
            Stance = Config.Bind("Pose", "Stance", 0.22f, "How far each foot is moved out to the side from where it stands, in metres: a twerk is done with the feet wide apart.");
            ToesOut = Config.Bind("Pose", "ToesOut", 20f, "How far the toes are turned outward, in degrees. The knees are pushed out over them.");
            Arch = Config.Bind("Pose", "Arch", 16f, "How far the lower back is arched, in degrees: the pelvis tips forward, the lower back curves the other way and the upper body stays where the lean puts it, which pushes the bottom up and back.");
            LeanExtra = Config.Bind("Behavior", "LeanExtra", 15f, "Extra forward lean she drifts into now and then, in degrees.");
            MoveSeconds = Config.Bind("Behavior", "MoveSeconds", 6f, "About how long she keeps each move, in seconds.");
            SwayWidth = Config.Bind("Behavior", "SwayWidth", 0.07f, "Side to side move: how far the hips swing, in metres.");
            CircleRadius = Config.Bind("Behavior", "CircleRadius", 0.055f, "Hip circle move: radius of the circle, in metres.");
            RollDegrees = Config.Bind("Behavior", "RollDegrees", 4f, "Side to side and circle moves: how far the pelvis rolls, in degrees.");
            SquatDepth = Config.Bind("Pose", "SquatDepth", 0.2f, "How far the hips drop, in metres. The feet stay planted and the knees bend.");
            HipBack = Config.Bind("Pose", "HipBack", 0.12f, "How far the hips are pushed back, in metres.");
            HandsOn = Config.Bind("Pose", "HandsOn", "Knees", "Where the hands rest: Hips, Knees or None.");
            HipWidth = Config.Bind("Pose", "HipWidth", 0.09f, "Hands on hips: how far out from the hip joint the wrist sits, in metres.");
            HipHeight = Config.Bind("Pose", "HipHeight", 0.15f, "Hands on hips: how far up the flank from the hip joint the wrist sits, in metres.");
            HipForward = Config.Bind("Pose", "HipForward", 0.03f, "Hands on hips: how far behind the hip joint the wrist sits (the fingers then reach the front), in metres.");
            ElbowBack = Config.Bind("Pose", "ElbowBack", 0.4f, "How far the elbows point backwards as well as outwards.");
            PoseRamp = Config.Bind("Pose", "PoseRamp", 0.8f, "Seconds to ease into and out of the pose.");
            ShakeDelay = Config.Bind("Pose", "ShakeDelay", 0.9f, "Seconds after pressing the key before the bouncing starts.");

            Frequency = Config.Bind("Motion", "Frequency", 2.4f, "Bounces per second (the tempo drifts around this).");
            BounceHeight = Config.Bind("Motion", "BounceHeight", 0.035f, "How far the hips go up and down, in metres.");
            TiltDegrees = Config.Bind("Motion", "TiltDegrees", 8f, "How much the pelvis rocks with each bounce, in degrees.");
            ButtAmplitude = Config.Bind("Motion", "ButtAmplitude", 0.03f, "Extra up and down movement of the glute bones, in metres.");
            ShakeChest = Config.Bind("Motion", "ShakeChest", false, "Also bounce the chest.");
            Smoothness = Config.Bind("Motion", "Smoothness", 0.5f, "How smooth the motion is, from 0 to 1: every part of it is eased a little, so that nothing moves in steps and the knocks of the bounce are rounded off. 0 is the raw motion; more also takes some of the depth off a fast bounce.");
            RideAutoMatch = Config.Bind("Ride", "AutoMatch", true, "Match the hero to her automatically: he is laid so that his crotch is under hers, whatever the pose, the lean and the facing. Off puts his head where the view point is instead.");
            ChestAmplitude = Config.Bind("Motion", "ChestAmplitude", 0.012f, "How far the chest bones move, in metres.");

            AddComponent<ShakeController>();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded: press {ToggleKey.Value} to twerk, {MenuKey.Value} for the settings window.");
        }
    }

    // A rotation the game animates every frame, that we modify on top and put back first thing the next frame.
    internal sealed class RotSlot
    {
        public readonly Transform t;
        private bool m_has;
        private Quaternion m_last;
        private Quaternion m_base;

        public RotSlot(Transform t)
        {
            this.t = t;
        }

        public void Begin()
        {
            Quaternion cur = t.localRotation;
            if (m_has && Quaternion.Angle(cur, m_last) < 0.01f)
            {
                cur = m_base;
            }
            m_base = cur;
            t.localRotation = cur;
        }

        public void Commit()
        {
            m_last = t.localRotation;
            m_has = true;
        }

        public void Release()
        {
            m_has = false;
        }
    }

    internal sealed class PosSlot
    {
        public readonly Transform t;
        private bool m_has;
        private Vector3 m_last;
        private Vector3 m_base;

        public PosSlot(Transform t)
        {
            this.t = t;
        }

        public Vector3 Begin()
        {
            Vector3 cur = t.localPosition;
            if (m_has && (cur - m_last).sqrMagnitude < 1e-12f)
            {
                cur = m_base;
            }
            m_base = cur;
            t.localPosition = cur;
            return cur;
        }

        public void Offset(Vector3 worldOffset)
        {
            Vector3 local = t.parent != null ? t.parent.InverseTransformVector(worldOffset) : worldOffset;
            t.localPosition = m_base + local;
            m_last = t.localPosition;
            m_has = true;
        }

        public void Release()
        {
            m_has = false;
        }
    }

    // What she is doing right now: how much of each move, and where she is in the beat.
    internal sealed class Motion
    {
        public float phase;
        public float bounce = 1f;
        public float sway;
        public float circle;
        public float circleDir = 1f;
        public float leanExtra;
        public float intensity = 1f;
        // Natural motion: how far the weight is on one leg (-1..1) and a slow drift of the depth of each bounce.
        public float weight;
        public float jitter = 1f;
        // What the preset does to the speed and to the depth of everything.
        public float speed = 1f;
        public float amp = 1f;
    }

    // Soft tissue: a mass on a spring that is shaken by the body's own acceleration, so it lags behind each bounce and
    // keeps wobbling for a moment after it, a little differently on each side.
    internal sealed class Jiggle
    {
        public Vector3 x, v;
        public readonly float freq, damping;

        public Jiggle(float freq, float damping)
        {
            this.freq = freq;
            this.damping = damping;
        }

        public void Reset()
        {
            x = Vector3.zero;
            v = Vector3.zero;
        }

        public void Step(Vector3 shake, float gain, float dt)
        {
            float k = (2f * Mathf.PI * freq) * (2f * Mathf.PI * freq);
            float c = 2f * damping * Mathf.Sqrt(k);
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 14);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += (-k * x - c * v - shake * gain) * h;
                x += v * h;
            }
            if (x.sqrMagnitude > 0.0036f)
            {
                x = x.normalized * 0.06f;
            }
        }
    }

    internal sealed class Limb
    {
        public RotSlot upper, fore, end;
        public int side;
    }

    // A hair's anchor, kept rigidly attached to the head while we move the body, plus the game's own
    // followers and hair updaters that have to be run again once the body has been posed.
    internal sealed class HairRig
    {
        public Transform pivot;
        public GPUTools.Hair.Scripts.HairSettings settings;
        public List<Assets._ReusableScripts.Miscellaneous.BaseFolowTransform> followers = new List<Assets._ReusableScripts.Miscellaneous.BaseFolowTransform>();
        public List<Assets._ReusableScripts.Miscellaneous.MatrixFollowerBase> matrixFollowers = new List<Assets._ReusableScripts.Miscellaneous.MatrixFollowerBase>();
        public List<Assets._ReusableScripts.CuchiCuchi.Dependentes.Hair.HairUpdater> updaters = new List<Assets._ReusableScripts.CuchiCuchi.Dependentes.Hair.HairUpdater>();
        public Matrix4x4 rel;
        public bool has;
        public bool warned;
    }

    internal sealed class Woman
    {
        public Transform head;
        public readonly Dictionary<int, HairRig> hair = new Dictionary<int, HairRig>();
        public Character character;
        public string name;
        public Transform hip;
        public PosSlot sHip;
        public RotSlot sPelvis, sWaist, sSp1, sSp2;
        public PosSlot glL, glR, peL, peR;
        public Limb[] legs = new Limb[2];
        public Limb[] arms = new Limb[2];

        // The wobble of the soft bones, and the hips' motion it is driven by.
        public readonly Jiggle jglL = new Jiggle(5.3f, 0.2f);
        public readonly Jiggle jglR = new Jiggle(4.7f, 0.24f);
        // The chest is stiffer and better damped than it was: it stays on the body and does not ring.
        public readonly Jiggle jpeL = new Jiggle(5.6f, 0.45f);
        public readonly Jiggle jpeR = new Jiggle(5.2f, 0.5f);
        public Vector3 lastHip, lastHipVel, hipAcc;
        public int hipSamples;

        // The smoothing of what poses her (two stages of easing), so that nothing moves in steps.
        public Vector3 fHipA, fHipB;
        public float fPitchA, fPitchB, fRollA, fRollB, fLeanA, fLeanB;
        public bool fHas;
    }

    public sealed partial class ShakeController : MonoBehaviour
    {
        private readonly List<Woman> m_women = new List<Woman>();
        private readonly HashSet<long> m_known = new HashSet<long>();
        private bool m_toggled;
        private float m_shakeAt;
        private float m_poseW;
        private float m_shakeW;
        private float m_nextScan;
        private GUIStyle m_style;

        private string m_notice = "";
        private float m_noticeUntil;

        private void Notice(string text)
        {
            m_notice = text;
            m_noticeUntil = Time.unscaledTime + 3f;
            Plugin.ModLog.LogInfo(text);
        }

        public ShakeController(IntPtr pointer) : base(pointer)
        {
        }

        private bool m_reapplyPending;
        private float m_reapplyAt;

        // The game hides and locks the mouse pointer while you play. Its own windows show it through this driver, which
        // also holds the game's input (camera look, movement), so the settings window uses the same one.
        private Assets.NewFeatures.Common.CursorInputDriver m_cursorDriver;
        private bool m_cursorForced;
        private bool m_cursorWarned;
        private CursorLockMode m_prevLock;
        private bool m_prevVisible;

        internal void SetMenuCursor(bool shown)
        {
            if (shown)
            {
                m_prevLock = Cursor.lockState;
                m_prevVisible = Cursor.visible;
            }
            try
            {
                if (m_cursorDriver == null)
                {
                    m_cursorDriver = new Assets.NewFeatures.Common.CursorInputDriver(this);
                }
                m_cursorDriver.SetCursorShown(shown);
            }
            catch (Exception ex)
            {
                if (!m_cursorWarned)
                {
                    m_cursorWarned = true;
                    Plugin.ModLog.LogWarning("Could not use the game's cursor driver, showing the pointer directly: " + ex.Message);
                }
            }
            if (shown)
            {
                m_cursorForced = true;
                KeepCursor();
            }
            else if (m_cursorForced)
            {
                m_cursorForced = false;
                Cursor.lockState = m_prevLock;
                Cursor.visible = m_prevVisible;
            }
        }

        // Called every frame while the window is open: the game puts the pointer away again when it can.
        internal void KeepCursor()
        {
            if (m_cursorForced)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        internal bool Twerking => m_toggled && !m_ride;
        internal bool Riding => m_toggled && m_ride;

        // Starts or stops the twerk, or the ride, the same way their keys do.
        internal void ToggleMode(bool ride)
        {
            SetActive(ride ? !(m_toggled && m_ride) : !(m_toggled && !m_ride), ride);
        }

        // Puts the bikini on or takes it off, the same way its key does.
        internal void ToggleBikini()
        {
            bool on = !Bikini.AnyOn();
            if (on && string.IsNullOrWhiteSpace(Plugin.BikiniTop.Value) && string.IsNullOrWhiteSpace(Plugin.BikiniBottom.Value))
            {
                Notice("Pick a top or a bottom first.");
                return;
            }
            m_reapplyPending = false;
            m_bikini = on;
            Bikini.Set(this, on);
        }

        // After another piece or colour mode is picked while she wears the bikini: take it off and put it on again.
        internal void ReapplyBikini()
        {
            if (!Bikini.AnyOn() && !m_reapplyPending)
            {
                return;
            }
            if (Bikini.AnyOn())
            {
                Bikini.Set(this, false);
            }
            m_reapplyPending = true;
            m_reapplyAt = Time.unscaledTime + 0.5f;
        }

        private void Update()
        {
            try
            {
                if (m_reapplyPending && Time.unscaledTime >= m_reapplyAt)
                {
                    m_reapplyPending = false;
                    m_bikini = true;
                    Bikini.Set(this, true);
                }
                // Typing in a text box of the window ("j" included) must not open, close or trigger anything.
                if (Menu.TypingText)
                {
                    return;
                }
                if (Plugin.MenuKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.MenuKey.Value))
                {
                    Menu.Toggle(this);
                }
                if (Plugin.PresetKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.PresetKey.Value))
                {
                    CyclePreset();
                }
                if (Plugin.BikiniKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.BikiniKey.Value))
                {
                    ToggleBikini();
                }
                if (Plugin.ToggleKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.ToggleKey.Value))
                {
                    SetActive(!(m_toggled && !m_ride), false);
                }
                if (Plugin.RideKey.Value != KeyCode.None && Input.GetKeyDown(Plugin.RideKey.Value))
                {
                    SetActive(!(m_toggled && m_ride), true);
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Input failed: " + ex.Message);
            }
        }

        // Starts, stops or switches between the two modes: the twerk and the ride.
        private void SetActive(bool on, bool ride)
        {
            bool wasOn = m_toggled;
            m_toggled = on;
            if (on)
            {
                m_ride = ride;
            }
            m_shakeAt = Time.time + Plugin.ShakeDelay.Value;
            m_nextScan = 0f;
            if (on)
            {
                ResetDirector();
                if (ride)
                {
                    StartRide();
                }
            }
            Plugin.ModLog.LogInfo((m_ride ? "Ride " : "Twerk ") + (on ? "on" : "off"));
            if (Plugin.BikiniWithTwerk.Value && on != wasOn)
            {
                Bikini.Set(this, on);
            }
        }

        internal static ShakeController Instance;
        private int m_lastFrame = -1;
        private bool m_bikini;

        private static string PathOf(Transform t)
        {
            if (t == null)
            {
                return "(null)";
            }
            string path = t.name;
            for (int i = 0; t.parent != null && i < 30; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // The game runs everything on its own ordered update stages (GlobalUpdater). Posing her at the first late
        // stage of the frame, before the IK, hair, constraint and collider stages, makes all of them see the posed body.
        private static readonly string[] LateStagePrefixes =
        {
            "lateUpdate", "onLookAt", "afterLookAt", "beforeOral", "onOral", "afterOral",
            "beforeAnimation", "onAnimation", "afterAnimation", "beforeDynamic", "onDynamic", "afterDynamic",
            "meshGeneral", "meshVertex", "meshUpdate", "onAI"
        };

        private readonly Dictionary<int, bool> m_isLateStage = new Dictionary<int, bool>();
        private long m_subscribedTo;
        private Il2CppSystem.Action<Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType> m_stageCallback;
        private bool m_loggedStage;

        private void TrySubscribe()
        {
            var updater = Assets._ReusableScripts.Globales.Updater.GlobalUpdater.m_Singleton;
            if (updater == null)
            {
                return;
            }
            long ptr = updater.Pointer.ToInt64();
            if (ptr == m_subscribedTo)
            {
                return;
            }
            try
            {
                if (m_stageCallback == null)
                {
                    m_stageCallback = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType>>(
                        new Action<Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType>(OnStage));
                }
                updater.add_updatingType(m_stageCallback);
                m_subscribedTo = ptr;
                Plugin.ModLog.LogInfo("Subscribed to the game's update stages.");
            }
            catch (Exception ex)
            {
                m_subscribedTo = ptr;
                Plugin.ModLog.LogWarning("Could not subscribe to the game's update stages, posing in LateUpdate instead: " + ex.Message);
            }
        }

        private void OnStage(Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType stage)
        {
            try
            {
                int key = (int)stage;
                if (!m_isLateStage.TryGetValue(key, out bool late))
                {
                    string name = stage.ToString();
                    late = false;
                    foreach (string prefix in LateStagePrefixes)
                    {
                        if (name.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            late = true;
                            break;
                        }
                    }
                    m_isLateStage[key] = late;
                }
                if (stage == Assets._ReusableScripts.Globales.Updater.GlobalUpdater.UpdateType.lateUpdateAfterCameraController)
                {
                    HeroFinal();
                }
                if (late && RunFrame(true) && !m_loggedStage && m_poseW > 0f)
                {
                    m_loggedStage = true;
                    Plugin.ModLog.LogInfo($"Posing from the game's '{stage}' stage.");
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Stage callback failed: " + ex.Message);
            }
        }

        private void LateUpdate()
        {
            TrySubscribe();
            RunFrame(false);
            HeroFinal();
            KeepCursor();
        }

        // Decides what she does: which move, how fast, when she eases off, and how deep she leans.
        private readonly Motion m_motion = new Motion();
        private readonly float m_seed = UnityEngine.Random.value * 100f;
        private int m_move = -1;
        private float m_moveEnd;
        private float m_nextPause;
        private float m_pauseEnd;
        private float m_leanTarget;

        // The rhythm. Steady is one bounce. Random picks a move now and then. Scenario is a fixed sequence that loops: a slow
        // circle, bounces, a pause, a faster stretch, side to side, and round again the other way. Moves melt into each other,
        // and the speed and depth of everything follow the preset.
        private struct Step
        {
            public float seconds;
            public int move;
            public float tempo;
            public float intensity;
            public int dir;
        }

        private static readonly Step[] Scenario =
        {
            new Step { seconds = 10f, move = 2, tempo = 0.65f, intensity = 1f, dir = 1 },
            new Step { seconds = 8f, move = 0, tempo = 1.0f, intensity = 1f, dir = 1 },
            new Step { seconds = 2.2f, move = 0, tempo = 0.9f, intensity = 0.12f, dir = 1 },
            new Step { seconds = 9f, move = 0, tempo = 1.45f, intensity = 1f, dir = 1 },
            new Step { seconds = 7f, move = 1, tempo = 0.9f, intensity = 1f, dir = 1 },
            new Step { seconds = 6f, move = 3, tempo = 1.1f, intensity = 1f, dir = 1 },
            new Step { seconds = 2f, move = 3, tempo = 0.8f, intensity = 0.15f, dir = 1 },
            new Step { seconds = 9f, move = 2, tempo = 0.7f, intensity = 1f, dir = -1 }
        };

        private int m_step = -1;
        private float m_tempoGoal = 1f;
        private float m_tempoNow = 1f;
        private float m_intensityGoal = 1f;

        // What a preset does to the speed and the depth of the motion. Mine leaves the settings exactly as they are.
        private static void PresetFactors(out float speed, out float amp)
        {
            switch ((Plugin.PresetName.Value ?? "Mine").ToLowerInvariant())
            {
                case "soft":
                    speed = 0.7f;
                    amp = 0.8f;
                    break;
                case "fast":
                    speed = 1.4f;
                    amp = 0.9f;
                    break;
                default:
                    speed = 1f;
                    amp = 1f;
                    break;
            }
        }

        internal void CyclePreset()
        {
            string[] names = { "Soft", "Fast", "Mine" };
            int i = Array.FindIndex(names, n => string.Equals(n, Plugin.PresetName.Value, StringComparison.OrdinalIgnoreCase));
            string next = names[(i + 1) % names.Length];
            Plugin.PresetName.Value = next;
            try
            {
                Plugin.Cfg.Save();
            }
            catch (Exception)
            {
            }
            Notice("Preset: " + next);
        }

        private void ResetDirector()
        {
            m_move = -1;
            m_step = -1;
            m_moveEnd = 0f;
            m_tempoGoal = 1f;
            m_tempoNow = 1f;
            m_intensityGoal = 1f;
            m_nextPause = Time.time + UnityEngine.Random.Range(9f, 15f);
            m_pauseEnd = 0f;
            m_motion.bounce = 1f;
            m_motion.sway = 0f;
            m_motion.circle = 0f;
            m_motion.intensity = 1f;
            m_motion.leanExtra = 0f;
        }

        private void StepDirector(float dt, float shakeW)
        {
            Motion mo = m_motion;
            float t = Time.time;
            string rhythm = Plugin.Rhythm.Value ?? "Scenario";
            bool scenario = string.Equals(rhythm, "Scenario", StringComparison.OrdinalIgnoreCase);
            bool random = string.Equals(rhythm, "Random", StringComparison.OrdinalIgnoreCase);

            // The preset eases in, so that changing it never makes a jump.
            PresetFactors(out float speedGoal, out float ampGoal);
            float presetBlend = 1f - Mathf.Exp(-dt * 3f);
            mo.speed = Mathf.Lerp(mo.speed, speedGoal, presetBlend);
            mo.amp = Mathf.Lerp(mo.amp, ampGoal, presetBlend);

            float tempo = 1f;
            if (scenario || random)
            {
                if (shakeW > 0.5f && t >= m_moveEnd)
                {
                    if (scenario)
                    {
                        m_step = (m_step + 1) % Scenario.Length;
                        Step s = Scenario[m_step];
                        m_move = s.move;
                        m_tempoGoal = s.tempo;
                        m_intensityGoal = s.intensity;
                        if (s.move == 2)
                        {
                            mo.circleDir = s.dir;
                        }
                        m_moveEnd = t + s.seconds * Mathf.Max(0.3f, Plugin.MoveSeconds.Value / 6f);
                    }
                    else
                    {
                        int next;
                        do { next = UnityEngine.Random.Range(0, 4); } while (next == m_move);
                        m_move = next;
                        float len = Plugin.MoveSeconds.Value;
                        m_moveEnd = t + UnityEngine.Random.Range(len * 0.7f, len * 1.4f);
                        if (m_move == 2 && UnityEngine.Random.value > 0.5f)
                        {
                            mo.circleDir = -mo.circleDir;
                        }
                        m_tempoGoal = 1f;
                        m_intensityGoal = 1f;
                    }
                }

                // The tempo glides to the one the step wants.
                m_tempoNow = Mathf.Lerp(m_tempoNow, m_tempoGoal, 1f - Mathf.Exp(-dt / 1.2f));
                tempo = m_tempoNow;

                float tb, ts, tc;
                switch (m_move)
                {
                    case 1: tb = 0.3f; ts = 1f; tc = 0f; break;
                    case 2: tb = 0.2f; ts = 0f; tc = 1f; break;
                    case 3: tb = 0.7f; ts = 0.6f; tc = 0.4f; break;
                    default: tb = 1f; ts = 0f; tc = 0f; break;
                }
                // One move melts into the next instead of changing at a constant rate and stopping with a corner.
                float blend = 1f - Mathf.Exp(-dt * 2.4f);
                mo.bounce = Mathf.Lerp(mo.bounce, tb, blend);
                mo.sway = Mathf.Lerp(mo.sway, ts, blend);
                mo.circle = Mathf.Lerp(mo.circle, tc, blend);

                // Eases off now and then and settles a little deeper: by the script in Scenario, at random in Random.
                bool pausing = false;
                if (scenario)
                {
                    pausing = m_intensityGoal < 0.5f;
                }
                else if (shakeW > 0.99f)
                {
                    if (t >= m_nextPause)
                    {
                        m_pauseEnd = t + UnityEngine.Random.Range(1.2f, 2.2f);
                        m_nextPause = m_pauseEnd + UnityEngine.Random.Range(12f, 24f);
                    }
                    pausing = t < m_pauseEnd;
                }
                float intensityGoal = scenario ? m_intensityGoal : (pausing ? 0.12f : 1f);
                mo.intensity = Mathf.Lerp(mo.intensity, intensityGoal, 1f - Mathf.Exp(-dt * 2.5f));

                float wantLean = Mathf.PerlinNoise(t * 0.1f, m_seed + 7f);
                m_leanTarget = pausing ? Mathf.Max(wantLean, 0.8f) : wantLean;
                mo.leanExtra = Mathf.Lerp(mo.leanExtra, Plugin.LeanExtra.Value * m_leanTarget, 1f - Mathf.Exp(-dt * 2f));
            }
            else
            {
                mo.bounce = 1f;
                mo.sway = 0f;
                mo.circle = 0f;
                mo.intensity = 1f;
                mo.leanExtra = 0f;
            }

            // Nobody keeps the same depth and speed for every bounce: both wander a little, slowly, and her weight moves from
            // one leg to the other.
            mo.jitter = 1f + 0.14f * (Mathf.PerlinNoise(t * 0.55f, m_seed + 21f) * 2f - 1f);
            float lean = Mathf.Sin(t * 0.5f + m_seed) * 0.7f + (Mathf.PerlinNoise(t * 0.25f, m_seed + 31f) * 2f - 1f) * 0.3f;
            mo.weight = Mathf.Lerp(mo.weight, lean, 1f - Mathf.Exp(-dt * 3f));
            tempo *= 1f + 0.05f * (Mathf.PerlinNoise(t * 0.9f, m_seed + 41f) * 2f - 1f) + 0.1f * (Mathf.PerlinNoise(t * 0.12f, m_seed) * 2f - 1f);

            mo.phase += dt * (m_ride ? Plugin.RideFrequency.Value : Plugin.Frequency.Value) * tempo * mo.speed * 2f * Mathf.PI;
            if (mo.phase > 2000f * Mathf.PI)
            {
                mo.phase -= 2000f * Mathf.PI;
            }
        }
        private bool RunFrame(bool early)
        {
            if (Time.frameCount == m_lastFrame)
            {
                return false;
            }
            m_lastFrame = Time.frameCount;
            Instance = this;
            try
            {
                float dt = Time.deltaTime;
                float ramp = Mathf.Max(0.05f, Plugin.PoseRamp.Value);
                m_poseW = Mathf.MoveTowards(m_poseW, m_toggled ? 1f : 0f, dt / ramp);
                m_shakeW = Mathf.MoveTowards(m_shakeW, m_toggled && Time.time >= m_shakeAt ? 1f : 0f, dt / 0.4f);

                bool active = m_toggled || m_poseW > 0f || m_shakeW > 0f || m_heroReady || m_wet > 0f;
                if (active && Time.unscaledTime >= m_nextScan)
                {
                    m_nextScan = Time.unscaledTime + 2f;
                    FindWomen();
                }
                if (!active && m_women.Count == 0)
                {
                    return true;
                }


                float poseW = Mathf.SmoothStep(0f, 1f, m_poseW);
                float shakeW = Mathf.SmoothStep(0f, 1f, m_shakeW);
                StepDirector(dt, shakeW);
                shakeW *= m_motion.intensity;
                // The hero is laid down first, before the game poses its puppets, so that they and she see him lying.
                PoseHero();
                UpdateWet(dt);
                foreach (Woman w in m_women)
                {
                    if (m_ride)
                    {
                        // Only the woman being ridden is posed; any other is let go.
                        if (w == m_rideWoman)
                        {
                            ApplyRide(w, poseW, shakeW, m_motion);
                        }
                        else
                        {
                            Apply(w, 0f, 0f, m_motion);
                        }
                    }
                    else
                    {
                        Apply(w, poseW, shakeW, m_motion);
                    }
                    if (!early)
                    {
                        // Fallback only: the game already updated its hair from the unposed head, so redo it.
                        ApplyHair(w, poseW, shakeW);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Twerk failed: " + ex);
            }
            return true;
        }

        private void FindWomen()
        {
            m_women.RemoveAll(w => w.character == null || w.hip == null);
            var characters = Resources.FindObjectsOfTypeAll<Character>();
            if (characters == null)
            {
                return;
            }
            foreach (Character c in characters)
            {
                if (c == null || !c.gameObject.scene.IsValid() || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (c.GetComponentInChildren<InteraccionesBasicasDeFemale>(true) == null)
                {
                    continue;
                }
                long key = c.Pointer.ToInt64();
                if (m_known.Contains(key))
                {
                    continue;
                }
                Woman w = Build(c);
                if (w != null)
                {
                    m_known.Add(key);
                    m_women.Add(w);
                }
            }
            foreach (Woman w in m_women)
            {
                RefreshHair(w);
            }
        }

        // The hair's anchor is not a bone, so it does not move with the body by itself.
        private static void RefreshHair(Woman w)
        {
            var dead = new List<int>();
            foreach (var kv in w.hair)
            {
                if (kv.Value.pivot == null)
                {
                    dead.Add(kv.Key);
                }
            }
            foreach (int id in dead)
            {
                w.hair.Remove(id);
            }
            foreach (GPUTools.Hair.Scripts.HairSettings hs in w.character.GetComponentsInChildren<GPUTools.Hair.Scripts.HairSettings>(true))
            {
                if (hs == null || !hs.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Transform pivot = null;
                try { pivot = hs.StandsSettings?.Provider != null ? hs.StandsSettings.Provider.transform : null; } catch (Exception) { }
                pivot = pivot ?? hs.transform;
                int id = pivot.GetInstanceID();
                if (!w.hair.ContainsKey(id))
                {
                    var rig = new HairRig { pivot = pivot, settings = hs };
                    foreach (var f in hs.GetComponentsInChildren<Assets._ReusableScripts.Miscellaneous.BaseFolowTransform>(true))
                    {
                        rig.followers.Add(f);
                    }
                    foreach (var f in hs.GetComponentsInChildren<Assets._ReusableScripts.Miscellaneous.MatrixFollowerBase>(true))
                    {
                        rig.matrixFollowers.Add(f);
                    }
                    foreach (var u in w.character.GetComponentsInChildren<Assets._ReusableScripts.CuchiCuchi.Dependentes.Hair.HairUpdater>(true))
                    {
                        if (u != null && u.m_HairSettings != null && u.m_HairSettings.Pointer == hs.Pointer)
                        {
                            rig.updaters.Add(u);
                        }
                    }
                    w.hair[id] = rig;
                    Plugin.ModLog.LogInfo($"Hair anchor tracked: {PathOf(pivot)} ({rig.followers.Count} followers, {rig.matrixFollowers.Count} matrix followers, {rig.updaters.Count} hair updaters)");
                }
            }
        }

        private static void ApplyHair(Woman w, float poseW, float shakeW)
        {
            if (w.head == null)
            {
                return;
            }
            bool idle = poseW <= 0.001f && shakeW <= 0.001f;
            foreach (HairRig rig in w.hair.Values)
            {
                if (rig.pivot == null)
                {
                    continue;
                }
                if (idle)
                {
                    // Her normal pose: the game drives the hair. Just remember where the anchor sits relative to the head.
                    rig.rel = w.head.worldToLocalMatrix * rig.pivot.localToWorldMatrix;
                    rig.has = true;
                }
                else if (rig.has)
                {
                    // The game updated its hair earlier this frame, from the unposed head. Redo it from the posed one,
                    // with the game's own code: anchor, followers, then the hair updater.
                    Matrix4x4 m = w.head.localToWorldMatrix * rig.rel;
                    rig.pivot.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                    try
                    {
                        foreach (var f in rig.followers) { if (f != null && f.enabled) { f.Follow(); } }
                        foreach (var f in rig.matrixFollowers) { if (f != null && f.enabled) { f.Follow(); } }
                        foreach (var u in rig.updaters) { if (u != null && u.enabled) { u.GPUTools_Hair_Scripts_IHairUpdaterTValle_LateUpdate(); } }
                    }
                    catch (Exception ex)
                    {
                        if (!rig.warned)
                        {
                            rig.warned = true;
                            Plugin.ModLog.LogWarning("Re-running the game's hair update failed: " + ex.Message);
                        }
                    }
                }
            }
        }

        private static Woman Build(Character c)
        {
            Animator anim = c.bodyAnimator;
            Transform hip = null;
            if (anim != null && anim.isHuman)
            {
                Transform mapped = anim.GetBoneTransform(HumanBodyBones.Hips);
                for (Transform t = mapped; t != null && hip == null; t = t.parent)
                {
                    if (t.name == "CC_Base_Hip")
                    {
                        hip = t;
                    }
                }
                if (hip == null)
                {
                    hip = mapped;
                }
            }
            if (hip == null)
            {
                foreach (Transform t in c.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "CC_Base_Hip")
                    {
                        hip = t;
                        break;
                    }
                }
            }
            if (hip == null)
            {
                return null;
            }

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in hip.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!byName.ContainsKey(n))
                {
                    byName[n] = t;
                }
            }

            Transform Get(string n) => byName.TryGetValue(n, out Transform t) ? t : null;

            Transform pelvis = Get("CC_Base_Pelvis");
            Transform waist = Get("CC_Base_Waist");
            Transform sp1 = Get("CC_Base_Spine01");
            Transform sp2 = Get("CC_Base_Spine02");
            Transform[] up = new Transform[2], fo = new Transform[2], en = new Transform[2];
            Transform[] aup = new Transform[2], afo = new Transform[2], aen = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                string s = i == 0 ? ".L" : ".R";
                up[i] = Get("CC_Base_Thigh" + s);
                fo[i] = Get("CC_Base_Calf" + s);
                en[i] = Get("CC_Base_Foot" + s);
                aup[i] = Get("CC_Base_Upperarm" + s);
                afo[i] = Get("CC_Base_Forearm" + s);
                aen[i] = Get("CC_Base_Hand" + s);
            }
            if (anim != null && anim.isHuman)
            {
                for (int i = 0; i < 2; i++)
                {
                    bool l = i == 0;
                    up[i] = up[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                    fo[i] = fo[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                    en[i] = en[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                    aup[i] = aup[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                    afo[i] = afo[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    aen[i] = aen[i] ?? anim.GetBoneTransform(l ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                }
            }

            string missing = "";
            if (pelvis == null) missing += " pelvis";
            if (waist == null) missing += " waist";
            if (sp1 == null) missing += " spine01";
            if (sp2 == null) missing += " spine02";
            for (int i = 0; i < 2; i++)
            {
                if (up[i] == null || fo[i] == null || en[i] == null) missing += " leg" + i;
                if (aup[i] == null || afo[i] == null || aen[i] == null) missing += " arm" + i;
            }
            Plugin.ModLog.LogInfo($"Woman '{c.name}': hip='{hip.name}' humanoid={(anim != null && anim.isHuman)} missing:[{missing}]");
            if (pelvis == null || waist == null || sp1 == null || sp2 == null || up[0] == null || up[1] == null || fo[0] == null || fo[1] == null || en[0] == null || en[1] == null)
            {
                return null;
            }

            Transform headBone = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            headBone = headBone ?? Get("CC_Base_Head");
            var w = new Woman { character = c, name = c.name, hip = hip, sHip = new PosSlot(hip), head = headBone };
            w.sPelvis = new RotSlot(pelvis);
            w.sWaist = new RotSlot(waist);
            w.sSp1 = new RotSlot(sp1);
            w.sSp2 = new RotSlot(sp2);
            for (int i = 0; i < 2; i++)
            {
                w.legs[i] = new Limb { side = i, upper = new RotSlot(up[i]), fore = new RotSlot(fo[i]), end = new RotSlot(en[i]) };
                if (aup[i] != null && afo[i] != null && aen[i] != null)
                {
                    w.arms[i] = new Limb { side = i, upper = new RotSlot(aup[i]), fore = new RotSlot(afo[i]), end = new RotSlot(aen[i]) };
                }
            }
            Transform gl = Get("DEF_Glute.L"), gr = Get("DEF_Glute.R"), pl = Get("DEF_Pecho.L"), pr = Get("DEF_Pecho.R");
            w.glL = gl != null ? new PosSlot(gl) : null;
            w.glR = gr != null ? new PosSlot(gr) : null;
            w.peL = pl != null ? new PosSlot(pl) : null;
            w.peR = pr != null ? new PosSlot(pr) : null;
            return w;
        }

        internal static void Ik(Limb limb, Vector3 target, Vector3 pole, float weight)
        {
            Transform upper = limb.upper.t, fore = limb.fore.t, end = limb.end.t;
            Quaternion baseU = upper.localRotation;
            Quaternion baseF = fore.localRotation;
            Vector3 s = upper.position, e = fore.position, wrist = end.position;
            float a = (e - s).magnitude;
            float b = (wrist - e).magnitude;
            Vector3 st = target - s;
            float d = Mathf.Clamp(st.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 u = st.normalized;
            Vector3 reach = s + u * d;
            float x = (a * a - b * b + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 p = pole - u * Vector3.Dot(pole, u);
            p = p.sqrMagnitude < 1e-6f ? Vector3.forward : p.normalized;
            Vector3 elbow = s + u * x + p * h;

            upper.rotation = Quaternion.FromToRotation(e - s, elbow - s) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(end.position - fore.position, reach - fore.position) * fore.rotation;

            upper.localRotation = Quaternion.Slerp(baseU, upper.localRotation, weight);
            fore.localRotation = Quaternion.Slerp(baseF, fore.localRotation, weight);
            limb.upper.Commit();
            limb.fore.Commit();
        }

        // A sine that is not symmetric: with amount 0 it is a plain sine, and the more it is raised the quicker the wave
        // falls and the slower it climbs back, like a body dropping onto its knees and coming up again.
        internal static float Skew(float phase, float amount)
        {
            return Mathf.Sin(phase + amount * Mathf.Sin(phase));
        }

        // The glutes and, if asked for, the chest wobble as soft tissue. Each is a mass on a spring that is shaken by the
        // acceleration of the hips, so it lags behind every bounce and rings on after it, a little differently on each side.
        internal static void Wobble(Woman w, Vector3 hipMotion, float shakeW)
        {
            if (shakeW <= 0.0005f)
            {
                w.jglL.Reset();
                w.jglR.Reset();
                w.jpeL.Reset();
                w.jpeR.Reset();
                w.hipSamples = 0;
                w.hipAcc = Vector3.zero;
                return;
            }
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
            if (w.hipSamples >= 1)
            {
                Vector3 vel = (hipMotion - w.lastHip) / dt;
                if (w.hipSamples >= 2)
                {
                    Vector3 acc = (vel - w.lastHipVel) / dt;
                    w.hipAcc = Vector3.Lerp(w.hipAcc, acc, 0.6f);
                }
                w.lastHipVel = vel;
            }
            w.lastHip = hipMotion;
            if (w.hipSamples < 2)
            {
                w.hipSamples++;
            }
            Vector3 shake = Vector3.ClampMagnitude(w.hipAcc, 80f);

            float gain = Plugin.ButtAmplitude.Value / 0.03f * 2.4f;
            w.jglL.Step(shake, gain, dt);
            w.jglR.Step(shake, gain, dt);
            w.glL?.Offset(w.jglL.x);
            w.glR?.Offset(w.jglR.x);
            if (Plugin.ShakeChest.Value)
            {
                // The breasts move a little, and never far from where they are on the body: the setting is how far they may
                // go at most (a fraction of it), the push comes from a half of the hips' shaking, and what goes sideways or
                // forward is cut down, so they bounce on the chest and do not slide off it.
                float reach = ChestReach();
                float chestGain = reach * 68f;
                Vector3 chestShake = Vector3.ClampMagnitude(shake * 0.5f, 25f);
                w.jpeL.Step(chestShake, chestGain, dt);
                w.jpeR.Step(chestShake, chestGain, dt);
                w.peL?.Offset(Hold(w.jpeL.x, reach));
                w.peR?.Offset(Hold(w.jpeR.x, reach));
            }
        }

        // The most the chest may move from where it is on the body, in metres: 40 % of the setting, which is at most 5 cm.
        internal static float ChestReach()
        {
            return Mathf.Min(Plugin.ChestAmplitude.Value, 0.05f) * 0.4f;
        }

        // A displacement held to a limit softly (it slows as it nears it), with little of it sideways or forward.
        private static Vector3 Hold(Vector3 d, float limit)
        {
            d = new Vector3(d.x * 0.35f, d.y, d.z * 0.35f);
            float m = d.magnitude;
            if (m < 1e-6f || limit < 1e-6f)
            {
                return Vector3.zero;
            }
            return d / m * (limit * (float)Math.Tanh(m / limit));
        }

        // Put everything back to what the game animated this frame.
        internal static void BeginAll(Woman w)
        {
            w.sHip.Begin();
            w.sPelvis.Begin();
            w.sWaist.Begin();
            w.sSp1.Begin();
            w.sSp2.Begin();
            foreach (Limb l in w.legs) { l.upper.Begin(); l.fore.Begin(); l.end.Begin(); }
            foreach (Limb l in w.arms) { if (l != null) { l.upper.Begin(); l.fore.Begin(); l.end.Begin(); } }
            w.glL?.Begin();
            w.glR?.Begin();
            w.peL?.Begin();
            w.peR?.Begin();
        }

        internal static void ReleaseAll(Woman w)
        {
            w.fHas = false;
            w.sHip.Release();
            w.sPelvis.Release();
            w.sWaist.Release();
            w.sSp1.Release();
            w.sSp2.Release();
            foreach (Limb l in w.legs) { l.upper.Release(); l.fore.Release(); l.end.Release(); }
            foreach (Limb l in w.arms) { if (l != null) { l.upper.Release(); l.fore.Release(); l.end.Release(); } }
            w.glL?.Release();
            w.glR?.Release();
            w.peL?.Release();
            w.peR?.Release();
        }

        // Smoothing: the values that pose her (the lift of the hips, the rock and the roll of the pelvis, the lean) are eased
        // every frame, in two stages, toward what the motion asks for, so that nothing she does moves in steps and the knock of
        // a bounce is rounded off. Smoothness 0 leaves them as they are.
        internal static void Ease(Woman w, ref Vector3 hip, ref float pitch, ref float roll, ref float lean)
        {
            float tau = Plugin.Smoothness.Value * 0.1f;
            if (!w.fHas || tau < 0.002f)
            {
                w.fHipA = w.fHipB = hip;
                w.fPitchA = w.fPitchB = pitch;
                w.fRollA = w.fRollB = roll;
                w.fLeanA = w.fLeanB = lean;
                w.fHas = true;
                return;
            }
            float k = 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / (tau * 0.5f));
            w.fHipA = Vector3.Lerp(w.fHipA, hip, k);
            w.fHipB = Vector3.Lerp(w.fHipB, w.fHipA, k);
            w.fPitchA = Mathf.Lerp(w.fPitchA, pitch, k);
            w.fPitchB = Mathf.Lerp(w.fPitchB, w.fPitchA, k);
            w.fRollA = Mathf.Lerp(w.fRollA, roll, k);
            w.fRollB = Mathf.Lerp(w.fRollB, w.fRollA, k);
            w.fLeanA = Mathf.Lerp(w.fLeanA, lean, k);
            w.fLeanB = Mathf.Lerp(w.fLeanB, w.fLeanA, k);
            hip = w.fHipB;
            pitch = w.fPitchB;
            roll = w.fRollB;
            lean = w.fLeanB;
        }

        private static void Apply(Woman w, float poseW, float shakeW, Motion mo)
        {
            float phase = mo.phase;
            BeginAll(w);

            if (poseW <= 0.0005f && shakeW <= 0.0005f)
            {
                ReleaseAll(w);
                return;
            }

            // The character's own frame, from where the thighs are.
            Vector3 right = Vector3.ProjectOnPlane(w.legs[1].upper.t.position - w.legs[0].upper.t.position, Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);

            Vector3[] footPos = new Vector3[2];
            Quaternion[] footRot = new Quaternion[2];
            for (int i = 0; i < 2; i++)
            {
                footPos[i] = w.legs[i].end.t.position;
                footRot[i] = w.legs[i].end.t.rotation;
            }

            // The bounce is not a pure sine: it drops quicker than it rises, and the pelvis tilts a quarter ahead. The
            // smoother the motion is set to be, the less lopsided that is.
            float skew = Mathf.Lerp(0.6f, 0.3f, Plugin.Smoothness.Value);
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float sB = Skew(phase, skew);
            float cB = Skew(phase + Mathf.PI * 0.5f, skew);
            float dir = mo.circleDir;
            float depth = mo.jitter * mo.amp;

            // Drop and push back the hips, then move them: up and down (bounce), side to side (sway) and round (circle), and
            // let her weight sit on one leg and then the other.
            Vector3 hipMove =
                Vector3.up * (sB * Plugin.BounceHeight.Value * depth * (mo.bounce + 0.6f * mo.circle))
                + right * ((s * Plugin.SwayWidth.Value * mo.sway + c * Plugin.CircleRadius.Value * mo.circle) * mo.amp)
                + fwd * (s * dir * Plugin.CircleRadius.Value * mo.circle * mo.amp)
                + right * (mo.weight * 0.022f);
            Vector3 hipOffset = Vector3.down * (Plugin.SquatDepth.Value * poseW) - fwd * (Plugin.HipBack.Value * poseW) + hipMove * shakeW;

            // Hinge forward at the pelvis and let the spine follow. The pelvis rocks with each move and the rock travels up the
            // spine as a wave: each level answers the one below a moment later and against it, and shows less of it.
            float lean = (Plugin.LeanDegrees.Value + mo.leanExtra) * poseW;
            float pitch = Plugin.TiltDegrees.Value * depth * (cB * mo.bounce + s * dir * mo.circle) * shakeW;
            float roll = Plugin.RollDegrees.Value * (s * mo.sway + c * mo.circle) * shakeW + mo.weight * 2.4f * shakeW;
            float wave = Plugin.TiltDegrees.Value * depth * mo.bounce * shakeW;
            float waistPitch = -wave * 0.34f * Skew(phase + Mathf.PI * 0.5f - 0.75f, skew);
            float sp1Pitch = -wave * 0.16f * Skew(phase + Mathf.PI * 0.5f - 1.5f, skew);
            float sp2Pitch = -wave * 0.06f * Skew(phase + Mathf.PI * 0.5f - 2.2f, skew);

            Ease(w, ref hipOffset, ref pitch, ref roll, ref lean);
            w.sHip.Offset(hipOffset);

            // The arch: the pelvis tips forward and the spine curves back the same amount in total, so the chest stays at
            // the angle the lean gives it while the bottom is pushed up and back.
            float arch = Plugin.Arch.Value * poseW;
            w.sPelvis.t.rotation = Quaternion.AngleAxis(roll, fwd) * Quaternion.AngleAxis(lean * 0.5f + pitch + arch, right) * w.sPelvis.t.rotation;
            w.sWaist.t.rotation = Quaternion.AngleAxis(-roll * 0.5f, fwd) * Quaternion.AngleAxis(lean * 0.2f + waistPitch - arch * 0.55f, right) * w.sWaist.t.rotation;
            w.sSp1.t.rotation = Quaternion.AngleAxis(lean * 0.15f + sp1Pitch - arch * 0.3f, right) * w.sSp1.t.rotation;
            w.sSp2.t.rotation = Quaternion.AngleAxis(lean * 0.15f + sp2Pitch - arch * 0.15f, right) * w.sSp2.t.rotation;
            w.sPelvis.Commit();
            w.sWaist.Commit();
            w.sSp1.Commit();
            w.sSp2.Commit();

            // The feet go wide apart with the toes turned out, and the knees are pushed out over them: the stance of a
            // twerk. The feet stay planted there while the hips drop and move.
            float legW = Mathf.Max(poseW, shakeW);
            float stance = Plugin.Stance.Value * poseW;
            float toes = Plugin.ToesOut.Value * poseW;
            for (int i = 0; i < 2; i++)
            {
                Limb leg = w.legs[i];
                Vector3 outward = i == 0 ? -right : right;
                Ik(leg, footPos[i] + outward * stance, fwd * 0.6f + outward, legW);
                leg.end.t.rotation = Quaternion.AngleAxis((i == 1 ? 1f : -1f) * toes, Vector3.up) * footRot[i];
                leg.end.Commit();
            }

            // Hands.
            string hands = Plugin.HandsOn.Value ?? "Knees";
            if (!string.Equals(hands, "None", StringComparison.OrdinalIgnoreCase))
            {
                bool onHips = string.Equals(hands, "Hips", StringComparison.OrdinalIgnoreCase);
                for (int i = 0; i < 2; i++)
                {
                    Limb arm = w.arms[i];
                    if (arm == null)
                    {
                        continue;
                    }
                    Vector3 outward = i == 0 ? -right : right;
                    // The side of the hip: out from the hip joint and up the flank, tilted with the pelvis as she leans.
                    Vector3 pelvisUp = Quaternion.AngleAxis(lean * 0.5f, right) * Vector3.up;
                    Vector3 target = onHips
                        ? w.legs[i].upper.t.position + outward * Plugin.HipWidth.Value + pelvisUp * Plugin.HipHeight.Value - fwd * Plugin.HipForward.Value
                        : w.legs[i].fore.t.position + Vector3.up * 0.06f + fwd * 0.03f;
                    Ik(arm, target, outward - fwd * Plugin.ElbowBack.Value, poseW);
                }
            }

            // The soft bones bounce on top of all that, shaken by how the hips really moved.
            Wobble(w, hipOffset, shakeW);
        }
        private void OnGUI()
        {
            try
            {
                if (m_style == null)
                {
                    m_style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                }
                if (Time.unscaledTime < m_noticeUntil && !string.IsNullOrEmpty(m_notice))
                {
                    Rect nr = new Rect(0f, 110f, Screen.width, 50f);
                    Color keep = GUI.color;
                    GUI.color = new Color(0f, 0f, 0f, 0.8f);
                    GUI.Label(new Rect(nr.x + 2f, nr.y + 2f, nr.width, nr.height), m_notice, m_style);
                    GUI.color = new Color(1f, 0.85f, 0.4f, 1f);
                    GUI.Label(nr, m_notice, m_style);
                    GUI.color = keep;
                }
                Menu.Draw(this);
                if (!Plugin.ShowNotice.Value || m_poseW <= 0.01f)
                {
                    return;
                }
                string label = m_ride ? "RIDE" : "TWERK";
                string stopKey = (m_ride ? Plugin.RideKey.Value : Plugin.ToggleKey.Value).ToString();
                string text = m_women.Count == 0 ? $"{label} (no model found)" : $"{label} ON  -  {stopKey} to stop";
                Rect rect = new Rect(0f, 40f, Screen.width, 50f);
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.8f * m_poseW);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, m_style);
                GUI.color = new Color(1f, 0.45f, 0.75f, m_poseW);
                GUI.Label(rect, text, m_style);
                GUI.color = previous;
            }
            catch (Exception)
            {
            }
        }
    }
}
