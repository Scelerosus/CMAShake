using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using UnityEngine;

namespace CMAShake
{
    // The ride: you are the first-person camera, so the camera drops low in front of her and she folds into a wide
    // squat over where you would be, leaning toward you with her hands reaching for your chest, and moves with the
    // same choreography as the twerk, at a slower pace. The hero, the male character the camera is attached to, is
    // carried with it: he lies on his back under her while it lasts and goes back to where he stood when it ends.
    public sealed partial class ShakeController
    {
        private bool m_ride;
        private Woman m_rideWoman;
        private Vector3 m_camPos;
        private bool m_camValid;
        private bool m_rideGeoInit;
        private bool m_herCrotchValid;
        private Vector3 m_herCrotch;
        private Vector3 m_crotchS;
        private Vector3 m_heroHomeGroin;
        private const float CamDistance = 0.6f;
        private const float CamHeight = 0.38f;
        private Vector3 m_rideFwd = Vector3.forward;
        private Vector3 m_ridePelvis;
        private Vector3 m_rideAnchor;
        private float m_rideGroundY;

        // The hero is moved for real. His body hangs from one root (the 'Anim' object of his puppet): the skeleton that is
        // drawn is its children and his physical body, the ragdoll with the colliders the game gave him, follows it. For
        // the ride that root is carried under her and turned onto its back, and the physical body is put on it with the
        // puppet's own teleport call, so he lies there with all of his physics and collision, as the game made them.
        // When the ride ends it is put back, with the physical body, exactly where he stood. His colliders, his puppet
        // and the camera are left alone.
        private Character m_hero;
        private float m_heroNextTry;
        private int m_heroCheckFrame;
        private bool m_heroReady;
        private RootMotion.Dynamics.PuppetMaster m_heroPuppet;
        private Transform m_heroRoot;
        private Vector3 m_heroRootPos;
        private Quaternion m_heroRootRot;
        private readonly List<Transform> m_heroBones = new List<Transform>();
        private readonly List<Vector3> m_heroHomePts = new List<Vector3>();
        private Transform m_heroPivot, m_heroHead;
        private Vector3 m_heroHomeHips, m_heroHomeHead, m_heroHomeFacing;
        private Vector3 m_heroAim;
        private Vector3 m_heroSetPos;
        private Quaternion m_heroSetRot;
        private bool m_heroSet;
        private int m_heroSetFrame = -1;
        private int m_heroFightLogs;

        // The game's own switch for the hero's movement controllers (Ootii's motion and actor controllers). It turns them
        // off in its scenes where he is placed by hand, such as the spa; left on, they treat a lying man as one in the air
        // and pull him down. A request to turn them off is added for the ride and taken away when it ends.
        private const string HeroFreezeId = "CMAShake.Ride";
        private Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.MotionAndActorControllerActivable m_heroActivable;

        private static Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.MotionAndActorControllerActivable FindHeroActivable(Character hero)
        {
            try
            {
                var puppet = hero.GetComponent<Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.Controllers.MotionControllerPuppet>();
                if (puppet != null && puppet.m_MotionAndActorControllerActivable != null)
                {
                    return puppet.m_MotionAndActorControllerActivable;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                return hero.GetComponentInChildren<Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.MotionAndActorControllerActivable>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void FreezeHeroControllers(Character hero)
        {
            m_heroActivable = null;
            try
            {
                var act = FindHeroActivable(hero);
                if (act == null || act.estanDesactivadosModificable == null)
                {
                    Plugin.ModLog.LogInfo("Ride: the hero's movement controllers were not found, they are left on.");
                    return;
                }
                bool actorWas = act.actorController != null && act.actorController.enabled;
                bool motionWas = act.motionController != null && act.motionController.enabled;
                var request = act.estanDesactivadosModificable.ObtenerModificadorNotNull(HeroFreezeId);
                var value = request.valor;
                value.valor = true;
                request.valor = value;
                act.Actualizar();
                m_heroActivable = act;
                bool actorNow = act.actorController != null && act.actorController.enabled;
                bool motionNow = act.motionController != null && act.motionController.enabled;
                Plugin.ModLog.LogInfo($"Ride: hero movement controllers turned off for the ride (actor {actorWas}->{actorNow}, motion {motionWas}->{motionNow}).");
            }
            catch (Exception ex)
            {
                m_heroActivable = null;
                Plugin.ModLog.LogWarning("Hero controllers could not be turned off: " + ex.Message);
            }
        }

        // His own idle animation (the shifting of weight, the breathing) goes on under the lying pose, and with it he sways a
        // little from side to side. It is stopped for the ride, with the rest of his own movement, and let go with it.
        private Animator m_heroAnimator;
        private float m_heroAnimSpeed = 1f;

        private void FreezeHeroAnimation(Character hero)
        {
            m_heroAnimator = null;
            try
            {
                Animator anim = hero.bodyAnimator;
                if (anim != null)
                {
                    m_heroAnimSpeed = anim.speed;
                    anim.speed = 0f;
                    m_heroAnimator = anim;
                }
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero animation could not be stopped: " + ex.Message);
            }
        }

        private void ThawHeroAnimation()
        {
            try
            {
                if (m_heroAnimator != null)
                {
                    m_heroAnimator.speed = m_heroAnimSpeed;
                }
            }
            catch (Exception)
            {
            }
            m_heroAnimator = null;
        }

        private void ThawHeroControllers()
        {
            if (m_heroActivable == null)
            {
                return;
            }
            try
            {
                m_heroActivable.estanDesactivadosModificable.RemoverModificador(HeroFreezeId);
                m_heroActivable.Actualizar();
                bool actorNow = m_heroActivable.actorController != null && m_heroActivable.actorController.enabled;
                Plugin.ModLog.LogInfo($"Ride: hero movement controllers given back (actor enabled={actorNow}).");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero controllers could not be given back: " + ex.Message);
            }
            m_heroActivable = null;
        }

        private sealed class HeroMuscleHome { public Transform t; public Rigidbody rb; public Vector3 p; public Quaternion r; }
        private readonly List<HeroMuscleHome> m_heroMuscleHome = new List<HeroMuscleHome>();
        private int m_heroAfterFrame;
        private RootMotion.Dynamics.PuppetMaster m_heroAfterPuppet;
        private Transform m_heroAfterHips;

        // His body as a few capsules along his bones, where they lie this frame. Her hips, bottom and thighs are kept out
        // of them, so the two bodies do not pass through each other.
        private sealed class HeroSeg
        {
            public Transform a, b;
            public float radius;
            public Vector3 pa, pb;
        }

        private readonly List<HeroSeg> m_heroSegs = new List<HeroSeg>();
        private Transform m_heroChest;
        private Transform m_heroThighL, m_heroThighR, m_heroKneeL, m_heroKneeR;
        private bool m_heroSolid;
        private float m_heroPush;
        // How deep he is in her right now (0: out of her, 1: all the way), and the frame it was last worked out in.
        private float m_rideIn;
        private int m_rideInFrame = -10;
        private float m_pushVel;

        // How far he is lying down (0 standing at home, 1 lying under her). It has a will of its own, so that however the
        // ride ends (the key, or a switch to the twerk) he is eased back home first and let go only when he is there.
        private float m_heroW;
        private int m_heroLowTries;

        // The way he lies follows the way she faces slowly, much more slowly than she moves, so that her rocking does not
        // turn him. The sway fields only measure how much he moves sideways while lying (for the log).
        private Vector3 m_heroFwd = Vector3.forward;
        private float m_swayFrom;
        private float m_swayMinX, m_swayMaxX, m_swayMinY, m_swayMaxY, m_herMinX, m_herMaxX;
        private Vector3 m_swayAlong, m_swaySide, m_swayOrigin, m_swayHerOrigin;

        private void AddHeroSeg(Transform a, Transform b, float radius)
        {
            if (a != null && b != null)
            {
                m_heroSegs.Add(new HeroSeg { a = a, b = b, radius = radius });
            }
        }

        private void BuildHeroSegs(Character hero)
        {
            m_heroSegs.Clear();
            m_heroChest = null;
            m_heroThighL = m_heroThighR = m_heroKneeL = m_heroKneeR = null;
            Animator anim = hero.bodyAnimator;
            if (anim != null && anim.isHuman)
            {
                Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                m_heroThighL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                m_heroThighR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                m_heroKneeL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                m_heroKneeR = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                m_heroChest = anim.GetBoneTransform(HumanBodyBones.Chest);
                if (m_heroChest == null)
                {
                    m_heroChest = anim.GetBoneTransform(HumanBodyBones.Spine);
                }
                // The thickness of a body, from the line of its bones to its skin: she sits on him, so these are kept close.
                AddHeroSeg(hips, anim.GetBoneTransform(HumanBodyBones.Spine), 0.12f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.Spine), anim.GetBoneTransform(HumanBodyBones.Chest), 0.12f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.Chest), anim.GetBoneTransform(HumanBodyBones.Neck), 0.13f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.Neck), head, 0.06f);
                AddHeroSeg(hips, anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), 0.09f);
                AddHeroSeg(hips, anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), 0.09f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), 0.08f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), anim.GetBoneTransform(HumanBodyBones.RightLowerLeg), 0.08f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg), anim.GetBoneTransform(HumanBodyBones.LeftFoot), 0.06f);
                AddHeroSeg(anim.GetBoneTransform(HumanBodyBones.RightLowerLeg), anim.GetBoneTransform(HumanBodyBones.RightFoot), 0.06f);
            }
            if (m_heroSegs.Count == 0)
            {
                AddHeroSeg(m_heroPivot, m_heroHead, 0.12f);
            }
        }

        // The height of the top of his body above a point, or minus infinity when nothing of him is under it.
        private float HeroTopAt(Vector3 p)
        {
            float best = float.NegativeInfinity;
            if (!m_heroSolid)
            {
                return best;
            }
            Vector2 q = new Vector2(p.x, p.z);
            foreach (HeroSeg s in m_heroSegs)
            {
                Vector2 a = new Vector2(s.pa.x, s.pa.z);
                Vector2 ab = new Vector2(s.pb.x, s.pb.z) - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
                float d = (q - (a + ab * t)).magnitude;
                if (d >= s.radius)
                {
                    continue;
                }
                best = Mathf.Max(best, Mathf.Lerp(s.pa.y, s.pb.y, t) + Mathf.Sqrt(s.radius * s.radius - d * d));
            }
            return best;
        }

        // How far a ball of her body has sunk into his.
        private float SinkOf(Vector3 centre, float radius)
        {
            float top = HeroTopAt(centre);
            // Soft bodies give a little: she may sink into him by the amount the setting allows.
            return float.IsNegativeInfinity(top) ? 0f : Mathf.Max(0f, top + 0.005f + radius - centre.y - Plugin.RideSink.Value);
        }

        // How far her hips have to be lifted, with the offset they are about to be given, for her hips, bottom and thighs
        // to rest on him instead of going into him.
        internal float HeroPushFor(Woman w, Vector3 offset, Vector3 right, Vector3 fwd)
        {
            float want = 0f;
            if (m_heroSolid)
            {
                Vector3 hip = w.hip.position + offset;
                // Her body is soft: the balls that stand for it are small, so that she settles onto him rather than hovering.
                want = Mathf.Max(want, SinkOf(hip, 0.09f));
                Vector3 bottom = hip - fwd * 0.09f - Vector3.up * 0.03f;
                want = Mathf.Max(want, SinkOf(bottom + right * 0.1f, 0.07f));
                want = Mathf.Max(want, SinkOf(bottom - right * 0.1f, 0.07f));
                for (int i = 0; i < 2; i++)
                {
                    want = Mathf.Max(want, SinkOf(w.legs[i].upper.t.position + offset, 0.085f));
                }
            }
            // The lift follows what is needed the way a well damped spring would: quickly, but without a step when what is under
            // her changes (the edge of his body, his lying down) and without ringing after it.
            float dt = Mathf.Clamp(Time.deltaTime, 0.004f, 0.05f);
            float zeta = 1f;
            float omega = 2f * Mathf.PI * 7f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 14);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                m_pushVel += (omega * omega * (want - m_heroPush) - 2f * zeta * omega * m_pushVel) * h;
                m_heroPush += m_pushVel * h;
            }
            if (m_heroPush < 0f)
            {
                m_heroPush = 0f;
                m_pushVel = Mathf.Max(0f, m_pushVel);
            }
            return m_heroPush;
        }

        // How much air there is between her crotch and his body below it (0 or less: she is down on him). Infinity when
        // nothing of him is under her.
        internal float HeroGapUnder(Woman w, Vector3 offset)
        {
            if (!m_heroSolid)
            {
                return float.PositiveInfinity;
            }
            Vector3 hip = w.hip.position + offset;
            float top = HeroTopAt(hip);
            return float.IsNegativeInfinity(top) ? float.PositiveInfinity : hip.y - 0.09f - top;
        }

        // How far a foot has to be moved out, on its side, to stand outside the hero's legs: the width of his body, from his
        // thighs, and the width of a foot.
        internal float HeroFootRoom(Vector3 foot, Vector3 outward)
        {
            if (!m_heroSolid || m_heroPivot == null || m_heroThighL == null || m_heroThighR == null)
            {
                return 0f;
            }
            Vector3 middle = m_heroPivot.position;
            float half = Mathf.Max(Mathf.Abs(Vector3.Dot(m_heroThighL.position - middle, outward)), Mathf.Abs(Vector3.Dot(m_heroThighR.position - middle, outward))) + 0.09f + 0.08f;
            float out_ = Vector3.Dot(foot - middle, outward);
            return Mathf.Clamp(half - out_, 0f, 0.25f);
        }

        // The hero is the male character the game's camera is attached to. When none is (a third-person scene), the
        // nearest man right next to the view is taken, and nobody if there is none: a bystander is never moved.
        private static Character FindHero(Vector3 near)
        {
            var all = Resources.FindObjectsOfTypeAll<Character>();
            if (all == null)
            {
                return null;
            }
            Character nearest = null;
            float nearestD = 2.5f * 2.5f;
            foreach (Character c in all)
            {
                if (c == null || !c.gameObject.scene.IsValid() || !c.gameObject.activeInHierarchy || c.sexo != Sexo.masculino)
                {
                    continue;
                }
                bool attached = false;
                try
                {
                    attached = c.cameraAtada != null;
                }
                catch (Exception)
                {
                }
                if (attached)
                {
                    return c;
                }
                float d = (c.transform.position - near).sqrMagnitude;
                if (d < nearestD)
                {
                    nearestD = d;
                    nearest = c;
                }
            }
            return nearest;
        }

        private static bool NameHas(string lower, params string[] words)
        {
            foreach (string w in words)
            {
                if (lower.Contains(w))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsLeftName(string n)
        {
            return n.Contains("left") || n.Contains("_l_") || n.Contains(".l") || n.EndsWith("_l") || n.Contains("l_thigh");
        }

        private static bool IsRightName(string n)
        {
            return n.Contains("right") || n.Contains("_r_") || n.Contains(".r") || n.EndsWith("_r") || n.Contains("r_thigh");
        }

        private static RootMotion.Dynamics.PuppetMaster FindHeroPuppet(Character hero)
        {
            try
            {
                var puppetChar = hero.GetComponent<Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.MalePuppetChar>();
                if (puppetChar != null)
                {
                    RootMotion.Dynamics.PuppetMaster pm = puppetChar.puppetMaster;
                    if (pm != null)
                    {
                        return pm;
                    }
                    pm = puppetChar.m_PuppetMaster;
                    if (pm != null)
                    {
                        return pm;
                    }
                }
            }
            catch (Exception)
            {
            }
            try
            {
                return hero.GetComponentInChildren<RootMotion.Dynamics.PuppetMaster>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // His body as it stands now, before the ride touches it: the bones that are drawn, which way he faces, and where his
        // root, his hips and his head are. The ride is planned from this alone, never from the body while it moves.
        private bool CaptureHeroRig(Character hero)
        {
            m_heroBones.Clear();
            m_heroHomePts.Clear();
            m_heroPivot = m_heroHead = null;
            var seen = new HashSet<int>();
            int meshCount = 0;
            Component[] places = { hero, hero.bodyAnimator };
            foreach (Component place in places)
            {
                if (m_heroBones.Count >= 10)
                {
                    break;
                }
                if (place == null)
                {
                    continue;
                }
                var meshes = place.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (meshes == null)
                {
                    continue;
                }
                foreach (SkinnedMeshRenderer smr in meshes)
                {
                    if (smr == null)
                    {
                        continue;
                    }
                    meshCount++;
                    var bones = smr.bones;
                    if (bones == null)
                    {
                        continue;
                    }
                    foreach (Transform b in bones)
                    {
                        if (b != null && seen.Add(b.GetInstanceID()))
                        {
                            m_heroBones.Add(b);
                        }
                    }
                }
            }
            if (m_heroBones.Count < 10)
            {
                Plugin.ModLog.LogWarning($"Ride: the hero's drawn skeleton was not found (meshes={meshCount} bones={m_heroBones.Count}), he is not moved.");
                return false;
            }

            Transform left = null, right = null, highest = null;
            foreach (Transform b in m_heroBones)
            {
                string n = b.name.ToLowerInvariant();
                if (m_heroPivot == null && (n.EndsWith("hip") || n.EndsWith("hips") || n.EndsWith("pelvis")))
                {
                    m_heroPivot = b;
                }
                if (m_heroHead == null && n.EndsWith("head"))
                {
                    m_heroHead = b;
                }
                if (NameHas(n, "thigh", "upleg", "upperleg", "upper_leg"))
                {
                    if (left == null && IsLeftName(n))
                    {
                        left = b;
                    }
                    else if (right == null && IsRightName(n))
                    {
                        right = b;
                    }
                }
                if (highest == null || b.position.y > highest.position.y)
                {
                    highest = b;
                }
            }
            if (m_heroPivot == null)
            {
                m_heroPivot = m_heroBones[0];
            }
            if (m_heroHead == null)
            {
                m_heroHead = highest;
            }

            foreach (Transform b in m_heroBones)
            {
                m_heroHomePts.Add(b.position);
            }
            m_heroHomeHips = m_heroPivot.position;
            m_heroHomeHead = m_heroHead != null ? m_heroHead.position : m_heroHomeHips + Vector3.up * 0.7f;

            // The ride is planned from his body as it stands. A body that is not standing (fallen, knocked out) would be a bad
            // plan to come back to, so it is waited for a little, and only then taken as it is.
            float lowY = float.MaxValue, highY = float.MinValue;
            foreach (Vector3 p in m_heroHomePts)
            {
                lowY = Mathf.Min(lowY, p.y);
                highY = Mathf.Max(highY, p.y);
            }
            if (highY - lowY < 1.0f && m_heroLowTries < 3)
            {
                m_heroLowTries++;
                Plugin.ModLog.LogWarning($"Ride: the hero is not standing (his bones span {highY - lowY:F2} m in height), waiting for him to get up ({m_heroLowTries}/3).");
                m_heroHomePts.Clear();
                m_heroNextTry = Time.unscaledTime + 2f;
                return false;
            }

            // He faces the way that is across his hips, turned a quarter, the same way her own front is found.
            Vector3 facing = Vector3.zero;
            if (left != null && right != null)
            {
                Vector3 across = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up);
                if (across.sqrMagnitude > 1e-6f)
                {
                    facing = Vector3.Cross(across.normalized, Vector3.up);
                }
            }
            if (facing.sqrMagnitude < 1e-6f)
            {
                facing = Vector3.ProjectOnPlane(hero.transform.forward, Vector3.up);
            }
            m_heroHomeFacing = facing.sqrMagnitude < 1e-6f ? Vector3.forward : facing.normalized;

            BuildHeroSegs(hero);
            // His crotch, as the middle of his hip joints: the point that is matched to hers.
            m_heroHomeGroin = m_heroThighL != null && m_heroThighR != null
                ? 0.5f * (m_heroThighL.position + m_heroThighR.position)
                : m_heroHomeHips + Vector3.down * 0.07f;
            Plugin.ModLog.LogInfo($"Ride: hero rig: meshes={meshCount} bones={m_heroBones.Count} hips='{m_heroPivot.name}' head='{(m_heroHead != null ? m_heroHead.name : "?")}' legs={(left != null && right != null ? "found" : "NOT FOUND")} hipsAt={m_heroHomeHips.ToString("F2")} rootAt={hero.transform.position.ToString("F2")}");
            return true;
        }

        // Where his root and his physical bones are before the ride, to be put back exactly when it ends.
        private void CaptureHeroHome(Character hero)
        {
            m_heroMuscleHome.Clear();
            m_heroRoot = null;
            try
            {
                if (m_heroPuppet != null)
                {
                    m_heroRoot = m_heroPuppet.targetRoot;
                }
                if (m_heroRoot == null && hero.bodyAnimator != null)
                {
                    m_heroRoot = hero.bodyAnimator.transform;
                }
                if (m_heroRoot != null)
                {
                    m_heroRootPos = m_heroRoot.position;
                    m_heroRootRot = m_heroRoot.rotation;
                }
                var muscles = m_heroPuppet != null ? m_heroPuppet.muscles : null;
                for (int i = 0; muscles != null && i < muscles.Length; i++)
                {
                    if (muscles[i] != null && muscles[i].joint != null)
                    {
                        Transform t = muscles[i].joint.transform;
                        m_heroMuscleHome.Add(new HeroMuscleHome { t = t, rb = t.GetComponent<Rigidbody>(), p = t.position, r = t.rotation });
                    }
                }
                Plugin.ModLog.LogInfo($"Ride: hero home: root '{(m_heroRoot != null ? m_heroRoot.name : "?")}' at {m_heroRootPos.ToString("F2")}, puppet={(m_heroPuppet != null)}, {m_heroMuscleHome.Count} physical bone(s) saved.");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero home could not be saved: " + ex.Message);
            }
        }

        // When the ride ends his root and his physical bones go back exactly where they were before it.
        private void RestoreHeroHome()
        {
            try
            {
                if (m_heroRoot != null)
                {
                    m_heroRoot.SetPositionAndRotation(m_heroRootPos, m_heroRootRot);
                }
                foreach (HeroMuscleHome h in m_heroMuscleHome)
                {
                    if (h.t != null)
                    {
                        h.t.SetPositionAndRotation(h.p, h.r);
                    }
                }
                CalmHeroBodies();
                Physics.SyncTransforms();
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero home could not be restored: " + ex.Message);
            }
        }

        // His physical bones keep no speed from the last step: whatever the joints were pulling or the floor pushing against is
        // forgotten, so that being put somewhere is not followed by a fall, or a throw, from the speed gathered meanwhile.
        private void CalmHeroBodies()
        {
            foreach (HeroMuscleHome h in m_heroMuscleHome)
            {
                if (h.rb != null)
                {
                    h.rb.velocity = Vector3.zero;
                    h.rb.angularVelocity = Vector3.zero;
                }
            }
        }

        private void ReleaseHero()
        {
            RestoreHeroHome();
            ThawHeroControllers();
            ThawHeroAnimation();
            // A moment later: where is he now, against where he was.
            m_heroAfterPuppet = m_heroPuppet;
            m_heroAfterHips = m_heroPivot;
            m_heroAfterFrame = Time.frameCount + 45;
            m_heroPuppet = null;
            m_heroRoot = null;
            m_heroSet = false;
            m_heroBones.Clear();
            m_heroHomePts.Clear();
            m_heroMuscleHome.Clear();
            m_heroSegs.Clear();
            m_heroChest = null;
            m_heroThighL = m_heroThighR = m_heroKneeL = m_heroKneeR = null;
            m_heroSolid = false;
            m_heroPush = 0f;
            m_pushVel = 0f;
            m_heroW = 0f;
            m_hero = null;
            m_heroReady = false;
        }

        // Where the pivot (his hips) has to go, and how the whole body has to turn, for him to lie on his back with his head at
        // the place the ride gives it, his body running back under her pelvis and his back on the floor.
        private void HeroGoal(out Vector3 pivotTarget, out Quaternion turn)
        {
            Vector3 up = Vector3.up;
            Quaternion standing = Quaternion.LookRotation(m_heroHomeFacing, up);
            // Facing him, his head is toward where she faces; reversed, it is behind her and he lies the other way round.
            bool away = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            Vector3 headDir = m_heroFwd;
            Quaternion lying = Quaternion.LookRotation(up, headDir);
            turn = lying * Quaternion.Inverse(standing);

            float lowest = float.MaxValue;
            foreach (Vector3 p in m_heroHomePts)
            {
                lowest = Mathf.Min(lowest, (turn * (p - m_heroHomeHips)).y);
            }
            float floor = m_rideGroundY - 0.05f + Plugin.RideHeroClearance.Value;
            if (m_herCrotchValid)
            {
                // Matching the crotches: he is laid so that the middle of his hip joints is under the middle of hers, whatever
                // the pose, the lean and the facing, and then moved along his body by the fine-tuning, if it is set.
                Vector3 groinRel = turn * (m_heroHomeGroin - m_heroHomeHips);
                Vector3 target = m_crotchS + headDir * Plugin.RideHeroShift.Value;
                // The bones run along the middle of the body: his back is about a hand's width below the lowest one.
                pivotTarget = new Vector3(target.x - groinRel.x, floor + 0.10f - lowest, target.z - groinRel.z);
                return;
            }
            // Before her crotch has been measured (the first frame): his head goes to the view point, in front of her pelvis
            // (or behind it when she faces away).
            Vector3 headRel = turn * (m_heroHomeHead - m_heroHomeHips);
            Vector3 aim = away ? 2f * m_ridePelvis - m_heroAim : m_heroAim;
            Vector3 headAt = new Vector3(aim.x, 0f, aim.z) + headDir * Plugin.RideHeroShift.Value;
            pivotTarget = new Vector3(headAt.x - headRel.x, floor + 0.10f - lowest, headAt.z - headRel.z);
        }

        // Carries his root to where it has to be this frame (blended with the ride's pose, so he lies down as it begins and
        // gets up as it ends) and puts his physical body on it.
        private void MoveHeroRoot(float weight)
        {
            HeroGoal(out Vector3 pivotTarget, out Quaternion turn);
            Quaternion part = Quaternion.Slerp(Quaternion.identity, turn, weight);
            Vector3 pivotAt = Vector3.Lerp(m_heroHomeHips, pivotTarget, weight);
            m_heroSetPos = pivotAt + part * (m_heroRootPos - m_heroHomeHips);
            m_heroSetRot = part * m_heroRootRot;
            m_heroSet = true;
            m_heroSetFrame = Time.frameCount;
            ApplyHeroRoot();
        }

        private void ApplyHeroRoot()
        {
            m_heroRoot.SetPositionAndRotation(m_heroSetPos, m_heroSetRot);
            if (m_heroPuppet != null)
            {
                // The puppet's own teleport: his physical body is put on his root, so it is never dragged there by forces.
                m_heroPuppet.MoveToTarget();
                CalmHeroBodies();
            }
        }

        private void LogHeroPuppet(string when)
        {
            try
            {
                if (m_heroPuppet == null)
                {
                    Plugin.ModLog.LogInfo($"Ride: hero puppet ({when}): none found");
                    return;
                }
                var muscles = m_heroPuppet.muscles;
                string at = "?";
                if (muscles != null && muscles.Length > 0 && muscles[0] != null && muscles[0].joint != null && muscles[0].target != null)
                {
                    at = $"puppet {muscles[0].joint.transform.position.ToString("F2")} target {muscles[0].target.position.ToString("F2")} ({muscles[0].name})";
                }
                Plugin.ModLog.LogInfo($"Ride: hero puppet ({when}): mode={m_heroPuppet.mode} state={m_heroPuppet.state} pin={m_heroPuppet.pinWeight:F2} muscle={m_heroPuppet.muscleWeight:F2} muscles={(muscles != null ? muscles.Length : 0)} {at}");
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero puppet report failed: " + ex.Message);
            }
        }

        // Runs early in the frame, before the game poses its puppets. He lies down and gets up with the ride, on his own ramp.
        private void PoseHero()
        {
            m_heroSolid = false;
            if (m_heroAfterFrame != 0 && Time.frameCount >= m_heroAfterFrame)
            {
                m_heroAfterFrame = 0;
                try
                {
                    var keep = m_heroPuppet;
                    m_heroPuppet = m_heroAfterPuppet;
                    LogHeroPuppet("after");
                    m_heroPuppet = keep;
                    if (m_heroAfterHips != null)
                    {
                        Plugin.ModLog.LogInfo($"Ride: hero after: hips={m_heroAfterHips.position.ToString("F2")} before the ride={m_heroHomeHips.ToString("F2")} (off by {(m_heroAfterHips.position - m_heroHomeHips).magnitude:F2} m)");
                    }
                }
                catch (Exception)
                {
                }
                m_heroAfterPuppet = null;
                m_heroAfterHips = null;
            }
            try
            {
                // He lies down while the ride is on and gets up when it is off, or when it turns into the twerk, each at his own
                // pace. He is let go only once he is standing at home again, never in the middle of a step.
                bool lieDown = m_ride && m_toggled && !m_switching;
                if (!m_heroReady)
                {
                    m_heroW = 0f;
                    if (!lieDown || !m_camValid || m_rideWoman == null)
                    {
                        return;
                    }
                    if (Time.unscaledTime < m_heroNextTry)
                    {
                        return;
                    }
                    Character hero = FindHero(m_camPos);
                    if (hero == null)
                    {
                        m_heroNextTry = Time.unscaledTime + 3f;
                        Plugin.ModLog.LogInfo("Ride: no hero found (no male character has the camera), he is not moved.");
                        return;
                    }
                    m_hero = hero;
                    if (!CaptureHeroRig(hero))
                    {
                        m_hero = null;
                        if (m_heroNextTry <= Time.unscaledTime)
                        {
                            m_heroNextTry = Time.unscaledTime + 10f;
                        }
                        return;
                    }
                    m_heroPuppet = FindHeroPuppet(hero);
                    CaptureHeroHome(hero);
                    if (m_heroRoot == null)
                    {
                        Plugin.ModLog.LogWarning("Ride: the hero has no root to carry, he is not moved.");
                        m_hero = null;
                        m_heroPuppet = null;
                        m_heroNextTry = Time.unscaledTime + 10f;
                        return;
                    }
                    LogHeroPuppet("before");
                    FreezeHeroControllers(hero);
                    FreezeHeroAnimation(hero);
                    WetCapture(hero);
                    m_heroFwd = HeroHeadGoal();
                    m_swayFrom = 0f;
                    m_heroAim = m_camPos;
                    m_crotchS = m_herCrotchValid ? m_herCrotch : m_camPos;
                    m_heroFightLogs = 0;
                    m_heroReady = true;
                    m_heroW = 0f;
                    m_heroCheckFrame = Time.frameCount + 30;
                    Plugin.ModLog.LogInfo($"Ride: '{hero.name}' is carried under her.");
                }
                if (m_hero == null || m_heroRoot == null || m_heroPivot == null)
                {
                    ReleaseHero();
                    return;
                }

                float ramp = Mathf.Max(0.8f, Plugin.PoseRamp.Value);
                m_heroW = Mathf.MoveTowards(m_heroW, lieDown ? 1f : 0f, Mathf.Min(Time.deltaTime, 0.1f) / ramp);
                float weight = Mathf.SmoothStep(0f, 1f, m_heroW);

                // The place he lies at follows the ride's view point, a little slowly so that her bounce does not move him.
                // (When the ride has just been switched, the view point is not known yet: the last one is kept.)
                if (m_camValid)
                {
                    float dtU = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    float k = 1f - Mathf.Exp(-dtU / 0.9f);
                    m_heroAim = Vector3.Lerp(m_heroAim, m_camPos, k);
                    if (m_herCrotchValid)
                    {
                        m_crotchS = Vector3.Lerp(m_crotchS, m_herCrotch, k);
                    }
                    float kf = 1f - Mathf.Exp(-dtU / 1.2f);
                    // He is turned about the upright only, by a part of the angle that is left: turning her round on him (the
                    // reverse) swings him round under her, flat on the floor, instead of flipping him over at once.
                    float turnLeft = Vector3.SignedAngle(m_heroFwd, HeroHeadGoal(), Vector3.up);
                    m_heroFwd = (Quaternion.AngleAxis(turnLeft * kf, Vector3.up) * m_heroFwd).normalized;
                }
                if (m_heroAnimator != null && m_heroAnimator.speed != 0f)
                {
                    // Whatever of the game's gave it speed again is told to wait.
                    m_heroAnimator.speed = 0f;
                }
                MoveHeroRoot(weight);
                if (!lieDown && m_heroW <= 0.001f)
                {
                    ReleaseHero();
                    Plugin.ModLog.LogInfo("Ride: the hero is back where he was.");
                    return;
                }

                // Where his body lies now, for her to be kept out of.
                bool solid = m_heroSegs.Count > 0;
                foreach (HeroSeg s in m_heroSegs)
                {
                    if (s.a == null || s.b == null)
                    {
                        solid = false;
                        break;
                    }
                    s.pa = s.a.position;
                    s.pb = s.b.position;
                }
                m_heroSolid = solid;

                if (m_heroCheckFrame != 0 && Time.frameCount >= m_heroCheckFrame && weight > 0.99f)
                {
                    m_heroCheckFrame = 0;
                    HeroGoal(out Vector3 wanted, out Quaternion unused);
                    float lowestNow = float.MaxValue;
                    foreach (Transform b in m_heroBones)
                    {
                        if (b != null)
                        {
                            lowestNow = Mathf.Min(lowestNow, b.position.y);
                        }
                    }
                    Plugin.ModLog.LogInfo($"Ride: hero lying: root={m_heroRoot.position.ToString("F2")} (set {m_heroSetPos.ToString("F2")}) hips={m_heroPivot.position.ToString("F2")} wanted hips={wanted.ToString("F2")} head={(m_heroHead != null ? m_heroHead.position.ToString("F2") : "?")} floor={m_rideGroundY.ToString("F2")} lowest bone y={lowestNow:F2}");
                    LogHeroPuppet("lying");
                    if (m_herCrotchValid && m_heroThighL != null && m_heroThighR != null)
                    {
                        // How well the crotches match: the distance between the two points across the floor.
                        Vector3 his = 0.5f * (m_heroThighL.position + m_heroThighR.position);
                        float off = new Vector2(his.x - m_herCrotch.x, his.z - m_herCrotch.z).magnitude;
                        Plugin.ModLog.LogInfo($"Ride: crotch match: hers {m_herCrotch.ToString("F2")} his {his.ToString("F2")}, off by {off * 100f:F1} cm across the floor.");
                    }
                }
            }
            catch (Exception ex)
            {
                m_heroNextTry = Time.unscaledTime + 5f;
                Plugin.ModLog.LogWarning("Hero failed: " + ex);
                try
                {
                    ReleaseHero();
                }
                catch (Exception)
                {
                    m_heroReady = false;
                    m_hero = null;
                }
            }
        }

        // How much he moves sideways and along his body once he lies, over five seconds, against how much her crotch does:
        // the first thing to read in the log when he seems to sway.
        private void MeasureSway()
        {
            if (m_swayFrom < 0f || m_heroW < 0.99f || m_heroPivot == null)
            {
                return;
            }
            if (m_swayFrom == 0f)
            {
                m_swayFrom = Time.time;
                m_swayMinX = float.NaN;
                return;
            }
            // The first three seconds are left out: he and she are still settling into the pose then.
            if (Time.time - m_swayFrom < 3f)
            {
                return;
            }
            if (float.IsNaN(m_swayMinX))
            {
                // Measured from where he is now and along the way he lies now, both kept for the whole of the measuring. (Before,
                // the axes were the ones of each frame and the positions were taken from the origin of the world: the least
                // turn of the axes, a third of a degree, showed as 8 cm of sway that was not there.)
                m_swayAlong = m_heroFwd;
                m_swaySide = Vector3.Cross(Vector3.up, m_swayAlong);
                m_swayOrigin = m_heroPivot.position;
                m_swayHerOrigin = m_herCrotch;
                m_swayMinX = m_swayMaxX = 0f;
                m_swayMinY = m_swayMaxY = 0f;
                m_herMinX = m_herMaxX = 0f;
                return;
            }
            float x = Vector3.Dot(m_heroPivot.position - m_swayOrigin, m_swaySide);
            float y = Vector3.Dot(m_heroPivot.position - m_swayOrigin, m_swayAlong);
            float hx = Vector3.Dot(m_herCrotch - m_swayHerOrigin, m_swaySide);
            m_swayMinX = Mathf.Min(m_swayMinX, x);
            m_swayMaxX = Mathf.Max(m_swayMaxX, x);
            m_swayMinY = Mathf.Min(m_swayMinY, y);
            m_swayMaxY = Mathf.Max(m_swayMaxY, y);
            m_herMinX = Mathf.Min(m_herMinX, hx);
            m_herMaxX = Mathf.Max(m_herMaxX, hx);
            if (Time.time - m_swayFrom >= 8f)
            {
                Plugin.ModLog.LogInfo($"Ride: hero sway over 5 s: sideways {(m_swayMaxX - m_swayMinX) * 100f:F1} cm, along his body {(m_swayMaxY - m_swayMinY) * 100f:F1} cm; her crotch sideways {(m_herMaxX - m_herMinX) * 100f:F1} cm.");
                m_swayFrom = -1f;
            }
        }

        // Runs last in the frame, with the camera: whatever the game did to his root since it was set (its own scripts follow
        // his hips with it) is undone, and his physical body is put on it again.
        private void HeroFinal()
        {
            if (!m_heroReady || m_heroRoot == null || !m_heroSet || m_heroSetFrame != Time.frameCount)
            {
                return;
            }
            try
            {
                float moved = (m_heroRoot.position - m_heroSetPos).magnitude;
                if (moved > 0.05f && m_heroFightLogs < 4)
                {
                    m_heroFightLogs++;
                    Plugin.ModLog.LogInfo($"Ride: the game moved the hero's root {moved:F2} m after it was set; put back.");
                }
                ApplyHeroRoot();
                // The physics scene is told now, not at its next step, so that contact checks made before then see him lying.
                Physics.SyncTransforms();
                MeasureSway();
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogWarning("Hero final pose failed: " + ex.Message);
            }
        }
        // Which way his head points as he lies: the way she faces, or the other way when she rides him reversed.
        private Vector3 HeroHeadGoal()
        {
            bool away = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            return away ? -m_rideFwd : m_rideFwd;
        }

        // The nearest woman to the player's view is the one who rides.
        private void StartRide()
        {
            // Started again while she is still in the pose of the ride before: the same woman keeps the layout she has, so
            // that nothing of it is found afresh and jumps.
            Woman before = m_poseW > 0.001f ? m_rideWoman : null;
            bool hadCam = m_camValid, hadGeo = m_rideGeoInit, hadCrotch = m_herCrotchValid;
            m_rideWoman = null;
            m_camValid = false;
            m_rideGeoInit = false;
            m_herCrotchValid = false;
            m_heroLowTries = 0;
            Camera cam = Camera.main;
            if (m_women.Count == 0)
            {
                FindWomen();
            }
            float best = float.MaxValue;
            foreach (Woman w in m_women)
            {
                if (w == null || w.hip == null)
                {
                    continue;
                }
                float d = cam != null ? (w.hip.position - cam.transform.position).sqrMagnitude : 0f;
                if (d < best)
                {
                    best = d;
                    m_rideWoman = w;
                }
            }
            if (before != null && m_rideWoman == before)
            {
                m_camValid = hadCam;
                m_rideGeoInit = hadGeo;
                m_herCrotchValid = hadCrotch;
            }
            Plugin.ModLog.LogInfo(m_rideWoman != null ? $"Ride: with '{m_rideWoman.name}'." : "Ride: no model found yet.");
        }

        private static void ApplyRide(Woman w, float poseW, float shakeW, Motion mo)
        {
            float phase = mo.phase;
            BeginAll(w);

            if (poseW <= 0.0005f && shakeW <= 0.0005f)
            {
                ReleaseAll(w);
                return;
            }

            Vector3 right = Vector3.ProjectOnPlane(w.legs[1].upper.t.position - w.legs[0].upper.t.position, Vector3.up).normalized;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);

            // Where her feet are on the floor, before anything moves.
            Vector3[] footPos = new Vector3[2];
            Quaternion[] footRot = new Quaternion[2];
            for (int i = 0; i < 2; i++)
            {
                footPos[i] = w.legs[i].end.t.position;
                footRot[i] = w.legs[i].end.t.rotation;
            }
            float groundY = Mathf.Min(footPos[0].y, footPos[1].y);
            Vector3 pelvisBase = w.sPelvis.t.position;

            // The ride is laid out from the unposed body, and the layout is eased: her own idle sway must not move him.
            float geoK = 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / 0.3f);
            // Her own idle animation shifts her hips about (8 cm from side to side in the log), and he, laid to match her, was
            // carried about with them. Her hips are held to an anchor instead: the place they have on average, which follows
            // where the game has her only very slowly. So her crotch stays in one place over him, and he stays still.
            Vector3 baseCrotch = 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position);
            bool firstGeo = !Instance.m_rideGeoInit;
            if (firstGeo)
            {
                Instance.m_rideAnchor = baseCrotch;
            }
            else
            {
                // The anchor all but stands still (half a minute to follow), so the sway, which comes back every few
                // seconds, is held off whole and not just smoothed. Only when the game has truly moved her does it go after
                // her: quickly from 15 cm away, almost at once from 30 cm.
                Vector3 off = baseCrotch - Instance.m_rideAnchor;
                off.y = 0f;
                float gone = off.magnitude;
                float tau = gone > 0.3f ? 0.4f : gone > 0.15f ? 1.5f : 30f;
                Instance.m_rideAnchor = Vector3.Lerp(Instance.m_rideAnchor, baseCrotch, 1f - Mathf.Exp(-Mathf.Clamp(Time.deltaTime, 0.004f, 0.1f) / tau));
            }
            // How far her hips are carried back to the anchor: never so far that her legs, planted where the game has her
            // feet, would be stretched.
            Vector3 hold = Vector3.ClampMagnitude(new Vector3(Instance.m_rideAnchor.x - baseCrotch.x, 0f, Instance.m_rideAnchor.z - baseCrotch.z), 0.12f);
            if (firstGeo)
            {
                Instance.m_rideFwd = fwd;
                Instance.m_rideGroundY = groundY;
                Instance.m_ridePelvis = pelvisBase;
                Instance.m_rideGeoInit = true;
            }
            else
            {
                Instance.m_rideFwd = Vector3.Slerp(Instance.m_rideFwd, fwd, geoK).normalized;
                Instance.m_rideGroundY = Mathf.Lerp(Instance.m_rideGroundY, groundY, geoK);
                // The pelvis the ride is laid out from is the held one too, so the view point and his aim do not sway either.
                Instance.m_ridePelvis = Vector3.Lerp(Instance.m_ridePelvis, pelvisBase + hold, geoK);
            }
            Vector3 pin = hold * poseW;
            Instance.m_camPos = new Vector3(Instance.m_ridePelvis.x, Instance.m_rideGroundY, Instance.m_ridePelvis.z) + Instance.m_rideFwd * CamDistance + Vector3.up * CamHeight;
            Instance.m_camValid = true;

            float skew = Mathf.Lerp(0.45f, 0.2f, Plugin.Smoothness.Value);
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float sB = Skew(phase, skew);
            float cB = Skew(phase + Mathf.PI * 0.5f, skew);
            float dir = mo.circleDir;
            float depth = mo.jitter * mo.amp;

            // The stroke starts from where she sits. The squat is the lowest she goes; from there her hips rise by the length
            // of the stroke and come back down, one whole smooth wave with nothing in its way at the bottom. (It used to swing
            // above and below the squat, and his body cut the lower half of it off: she hit him, stopped dead and sprang.)
            float squatDepth = Glide(w, G.RideSquat, Plugin.RideSquat.Value) * poseW;
            float rise = 0.5f * (1f + sB) * Plugin.RideBounce.Value * depth * (mo.bounce + 0.5f * mo.circle);
            Vector3 hipMove =
                Vector3.up * rise
                + fwd * ((s * Plugin.RideGrind.Value * mo.sway + s * dir * Plugin.CircleRadius.Value * mo.circle) * mo.amp)
                + right * (c * Plugin.CircleRadius.Value * mo.circle * mo.amp)
                + right * (mo.weight * 0.012f);
            Vector3 squat = Vector3.down * squatDepth + hipMove * shakeW;

            // Lean over him; the pelvis rocks with every stroke and the rock travels up the spine a moment later.
            // With her hands on his thighs behind her she leans back to reach them, instead of forward over him. Turned away
            // from him, his chest is behind her back: her hands go to his thighs, which are then in front of her.
            bool facingAway = string.Equals(Plugin.RideFacing.Value, "Away", StringComparison.OrdinalIgnoreCase);
            bool handsOnThem = string.Equals(Plugin.RideHandsOn.Value, "HeroThighs", StringComparison.OrdinalIgnoreCase)
                || (facingAway && string.Equals(Plugin.RideHandsOn.Value, "Hero", StringComparison.OrdinalIgnoreCase));
            bool handsBehind = handsOnThem && !facingAway;
            // What is set glides: leaning the other way, or her hands going somewhere else, is a movement, not a jump.
            float behind = Glide(w, G.RideBehind, handsBehind ? 1f : 0f);

            // Her hands rest on the hero's chest, on his thighs, or on her own thighs, as chosen. Each of the three places has
            // a weight that glides, and her hands are at the blend of them: changing the place moves her arms there, and his
            // body coming under her hands (or going) does not make them jump.
            bool onThighs = string.Equals(Plugin.RideHandsOn.Value, "Thighs", StringComparison.OrdinalIgnoreCase);
            bool hisThighsThere = Instance.m_heroSolid && Instance.m_heroThighL != null && Instance.m_heroThighR != null
                && Instance.m_heroKneeL != null && Instance.m_heroKneeR != null && Instance.m_heroPivot != null;
            bool chestThere = Instance.m_heroSolid && Instance.m_heroChest != null;
            float onHis = Glide(w, G.RideHandsHis, handsOnThem && hisThighsThere ? 1f : 0f);
            float onOwn = Glide(w, G.RideHandsOwn, onThighs ? 1f : 0f);
            float onChest = Glide(w, G.RideHandsChest, !onThighs && !(handsOnThem && hisThighsThere) ? 1f : 0f);
            float chestReal = Glide(w, G.RideChestReal, chestThere ? 1f : 0f);
            if (!hisThighsThere)
            {
                onHis = 0f;
            }
            if (onHis + onOwn + onChest < 0.001f)
            {
                onChest = 1f;
            }
            float onSum = onHis + onOwn + onChest;
            // Where his chest is taken to be until he is there, and his real chest once he lies.
            Vector3 chest = Instance.m_camPos + Vector3.down * 0.12f - fwd * 0.2f;
            if (chestThere)
            {
                chest = Vector3.Lerp(chest, Instance.m_heroChest.position, chestReal);
            }
            // Where each hand goes on him: on his chest, or on the thigh of his that is on her side.
            Vector3[] chestSpot = new Vector3[2];
            Vector3[] thighSpot = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                Vector3 outward = i == 0 ? -right : right;
                // Close enough to the middle of his chest to be on it, not off its side.
                Vector3 spot = chest + outward * 0.09f;
                float top = Instance.HeroTopAt(spot);
                // On his chest, not inside it: with nothing found under the hand, the skin is taken to be a chest's
                // thickness above its bone.
                float skin = float.IsNegativeInfinity(top) ? chest.y + 0.12f : top;
                spot.y = Mathf.Lerp(spot.y, skin + 0.03f, chestReal);
                chestSpot[i] = spot;
                if (hisThighsThere)
                {
                    Vector3 middle = Instance.m_heroPivot.position;
                    bool useLeft = Vector3.Dot(Instance.m_heroThighL.position - middle, outward) > Vector3.Dot(Instance.m_heroThighR.position - middle, outward);
                    Vector3 th = Vector3.Lerp(useLeft ? Instance.m_heroThighL.position : Instance.m_heroThighR.position,
                        useLeft ? Instance.m_heroKneeL.position : Instance.m_heroKneeR.position, 0.4f);
                    float surface = Instance.HeroTopAt(th);
                    thighSpot[i] = new Vector3(th.x, float.IsNegativeInfinity(surface) ? th.y + 0.1f : surface + 0.03f, th.z);
                }
            }

            // Leaning forward to put her hands on him, she leans as far as it takes to reach him: the lean that is set is
            // the least of it. Worked out for the top of the stroke, so her hands stay on him all the way up.
            float leanGoal = handsBehind ? -Plugin.RideLeanBack.Value : Plugin.RideLean.Value;
            float onHimAhead = onChest + (handsBehind ? 0f : onHis);
            if (!handsBehind && onHimAhead > 0.001f && w.arms[0] != null && w.arms[1] != null)
            {
                Vector3 reachTo = Vector3.zero;
                for (int i = 0; i < 2; i++)
                {
                    reachTo += (onChest * chestSpot[i] + onHis * thighSpot[i]) / onHimAhead;
                }
                reachTo *= 0.5f;
                float armLen = float.MaxValue;
                Vector3 shoulders = Vector3.zero;
                for (int i = 0; i < 2; i++)
                {
                    Limb arm = w.arms[i];
                    armLen = Mathf.Min(armLen, (arm.fore.t.position - arm.upper.t.position).magnitude + (arm.end.t.position - arm.fore.t.position).magnitude);
                    shoulders += 0.5f * arm.upper.t.position;
                }
                float topRise = Plugin.RideBounce.Value * depth * (mo.bounce + 0.5f * mo.circle) * shakeW;
                Vector3 pelvisThen = pelvisBase + pin + Vector3.down * squatDepth + Vector3.up * (Instance.m_heroPush + topRise);
                Vector3 torso = shoulders - pelvisBase;
                float need = 70f;
                for (float a = 0f; a <= 70f; a += 2f)
                {
                    if ((pelvisThen + Quaternion.AngleAxis(a, right) * torso - reachTo).sqrMagnitude <= armLen * armLen * 0.85f)
                    {
                        need = a;
                        break;
                    }
                }
                leanGoal = Mathf.Max(leanGoal, Mathf.Lerp(Plugin.RideLean.Value, need, onHimAhead / onSum));
            }
            float lean = (Glide(w, G.RideLean, leanGoal) + (1f - behind) * mo.leanExtra * 0.5f) * poseW;
            float pitch = Plugin.RideTilt.Value * depth * (cB * mo.bounce + s * mo.sway + s * dir * mo.circle) * shakeW;
            float roll = Plugin.RollDegrees.Value * c * mo.circle * shakeW + mo.weight * 1.6f * shakeW;
            float wave = Plugin.RideTilt.Value * depth * mo.bounce * shakeW;
            float waistPitch = -wave * 0.3f * Skew(phase + Mathf.PI * 0.5f - 0.75f, skew);
            float sp1Pitch = -wave * 0.12f * Skew(phase + Mathf.PI * 0.5f - 1.5f, skew);

            Ease(w, ref squat, ref pitch, ref roll, ref lean);
            // She sits on him, not in him. How far she has to be lifted for that is found for the bottom of the stroke (the
            // squat itself, with the sideways and forward-and-back of this moment), so the lift is a steady thing that sets
            // where she sits, and the stroke rides on top of it untouched.
            Vector3 seat = new Vector3(squat.x, -squatDepth, squat.z) + pin;
            float lift = Instance.HeroPushFor(w, seat, right, fwd);
            // Whether she is really down on him there (asked before her hips are moved, from where the game has them).
            float seated = 1f - Mathf.Clamp01(Instance.HeroGapUnder(w, seat + Vector3.up * lift) / 0.03f);
            // A soft body gives as it lands: at the very bottom she sinks a little further, by as much as the setting allows.
            float low = 0.5f * (1f - sB);
            float give = Mathf.Clamp01(Plugin.RideSpring.Value) * 0.02f * shakeW * low * low * low;
            w.sHip.Offset(squat + pin + Vector3.up * (lift - give));

            // How deep he is in her: all the way while she sits on him at the bottom of the stroke, less as she rises (the
            // tip stays in at the top), and not at all while she is not down on him. His wetness comes from this.
            float strokeShare = shakeW * Mathf.Clamp01(Plugin.RideBounce.Value * depth / 0.04f);
            Instance.m_rideIn = seated * Mathf.Lerp(1f, 0.2f + 0.8f * low, strokeShare) * poseW;
            Instance.m_rideInFrame = Time.frameCount;

            w.sPelvis.t.rotation = Quaternion.AngleAxis(roll, fwd) * Quaternion.AngleAxis(lean * 0.45f + pitch, right) * w.sPelvis.t.rotation;
            w.sWaist.t.rotation = Quaternion.AngleAxis(lean * 0.25f + waistPitch, right) * w.sWaist.t.rotation;
            w.sSp1.t.rotation = Quaternion.AngleAxis(lean * 0.18f + sp1Pitch, right) * w.sSp1.t.rotation;
            w.sSp2.t.rotation = Quaternion.AngleAxis(lean * 0.12f, right) * w.sSp2.t.rotation;
            w.sPelvis.Commit();
            w.sWaist.Commit();
            w.sSp1.Commit();
            w.sSp2.Commit();

            // Feet stay planted but wider apart, so the knees fold outward.
            float legW = Mathf.Max(poseW, shakeW);
            float stance = Glide(w, G.RideStance, Plugin.RideStance.Value) * poseW;
            for (int i = 0; i < 2; i++)
            {
                Limb leg = w.legs[i];
                Vector3 outward = i == 0 ? -right : right;
                Vector3 target = footPos[i] + outward * stance;
                // Wide enough apart to stand outside his legs, whatever the stance is set to.
                target += outward * (Instance.HeroFootRoom(target, outward) * poseW);
                Ik(leg, target, fwd * 0.5f + outward * 0.9f, legW);
                leg.end.t.rotation = footRot[i];
                leg.end.Commit();
            }

            // Her hands go where it was chosen above, at the blend of the three places.
            for (int i = 0; i < 2; i++)
            {
                Limb arm = w.arms[i];
                if (arm == null)
                {
                    continue;
                }
                Vector3 outward = i == 0 ? -right : right;
                Vector3 target = Vector3.zero;
                Vector3 pole = Vector3.zero;
                if (onHis > 0.001f)
                {
                    // On the thigh of his that is on her side, with the elbow out and back.
                    target += onHis * thighSpot[i];
                    pole += onHis * Vector3.Lerp(outward + Vector3.up * 0.3f, outward * 0.6f - fwd * 0.5f, behind);
                }
                if (onOwn > 0.001f)
                {
                    // On her own thigh, a little above the middle of it, with the elbow out to the side.
                    target += onOwn * (Vector3.Lerp(w.legs[i].upper.t.position, w.legs[i].fore.t.position, 0.6f) + Vector3.up * 0.07f + outward * 0.03f);
                    pole += onOwn * (outward - fwd * 0.3f + Vector3.up * 0.2f);
                }
                if (onChest > 0.001f)
                {
                    target += onChest * chestSpot[i];
                    pole += onChest * (outward + Vector3.up * 0.4f);
                }
                Ik(arm, target / onSum, pole / onSum, poseW);
            }

            // Where her crotch is, without the motion of the stroke: the middle of her hip joints, less the sideways and
            // forward-and-back of the hips. The hero is laid to match it.
            Vector3 hipsMid = 0.5f * (w.legs[0].upper.t.position + w.legs[1].upper.t.position);
            Instance.m_herCrotch = hipsMid - new Vector3(squat.x, 0f, squat.z);
            Instance.m_herCrotchValid = true;

            // The soft bones add their own bounce, shaken by how the hips really moved.
            Wobble(w, squat, shakeW);
        }

    }
}
