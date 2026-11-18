using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// You, in Recalled: your kid in third person (over the shoulder, always facing where you look, so the crosshair
    /// is your aim), your health, your two weapons (melee, ranged or a gadget), charging and blocking with them,
    /// the dodge, the ladder, the flashlight, the noise you make, and what happens when the toys get you.
    /// </summary>
    public class RcPlayer : MonoBehaviour
    {
        private FirstPersonController _fps;
        private CharacterController _cc;
        private Camera _cam;

        public float Health;
        public float MaxHealth { get { return Rc.Char.MaxHealth; } }
        public bool Dead { get; private set; }
        public int Slot;                       // which of your two weapons is out (0 or 1)
        public bool LightOn = true;
        public float Shield;                   // what's left of a Lid Shield

        /// <summary>Your feet, in town meters.</summary>
        public Vector3 TownPos { get { return Rc.T(_feet); } }
        public Vector3 WorldPos { get { return _feet; } }
        public float Yaw { get { return _fps != null ? _fps.transform.eulerAngles.y : 0f; } }
        public float LastNoiseAt { get; private set; }

        /// <summary>Holding right click with a weapon out: what hits you from the front is mostly stopped.</summary>
        public bool Blocking { get; private set; }
        /// <summary>0..1: how far a held left click has drawn your slingshot back (or wound up a heavy swing).</summary>
        public float Charge { get { return _charging ? _charge : 0f; } }
        public bool Climbing { get; private set; }
        /// <summary>A boss fight started without you: you're kept outside it, and its toys leave you alone.</summary>
        public bool Waiting;
        /// <summary>The trailer has the camera: your kid, your light and your input are off and nothing hurts you.</summary>
        public bool Cinematic;

        // What friends need to draw you.
        public float KidYaw { get { return _kidYaw; } }
        public bool SwingHeavy { get { return _swingHeavy; } }
        public int SwingCount { get; private set; }
        public int CastCount { get; private set; }
        public float ClimbHeight { get { return _climbY; } }
        public bool Rolling { get { return Time.time < _dodgeUntil; } }
        public float RollYaw { get { return Mathf.Atan2(_dodgeDir.x, _dodgeDir.z) * Mathf.Rad2Deg; } }

        /// <summary>Press block within this long of a hit landing and it's a perfect block.</summary>
        public const float PARRY_WINDOW = 0.3f;
        private const float BLOCK_LOCK = 0.35f;     // after lowering a guard you can't raise it again at once (no tapping for perfect blocks)
        private const float COUNTER_TIME = 3f;      // after a perfect block, your next blow is a counter for this long
        private const float COUNTER_POWER = 1.6f;
        private float _blockLockUntil, _counterUntil;
        private RcBubble _bubble;
        private KidAnimator _kidAnim;

        private Vector3 _feet;
        private float _attackAt, _swingAt = -10f, _castAt = -10f, _hitAt = -1f;
        private bool _charging, _swingHeavy;
        private float _charge, _swingPower = 1f, _blockAt = -10f, _blockedAt = -10f;
        private float _stepOffset = -1f;
        private readonly float[] _gadgetReadyAt = new float[2];
        private float _shieldUntil, _lastHurtAt = -100f, _iframesUntil, _dodgeUntil, _dodgeReadyAt, _stepNoiseAt, _deadAt, _gearAt;
        private Vector3 _dodgeDir;
        private Light _torch;

        public void Begin(FirstPersonController fps)
        {
            _fps = fps;
            _cc = fps.GetComponent<CharacterController>();
            _feet = WorldRays.FeetOf(fps.transform.position);
            BoostSpeed();
            // (the town has real stairs: the game's own step height is for a house with none)
            if (_cc != null) { _stepOffset = _cc.stepOffset; _cc.stepOffset = Mathf.Max(_stepOffset, 0.4f); }
            Health = MaxHealth;
            var t = new GameObject("KE_RcTorch");
            t.transform.SetParent(transform, false);
            _torch = t.AddComponent<Light>();
            _torch.type = LightType.Spot;
            _torch.spotAngle = 58f;
            _torch.range = 26f;
            _torch.intensity = 1.8f;
            _torch.color = new Color(1f, 0.95f, 0.82f);
            _torch.shadows = LightShadows.Hard; // (your light stops at walls: the dark rooms stay dark until you look in)
            Camera.onPreCull += PreCull;
            Rc.World.Uses.Add(new RcUse
            {
                Radius = 2.2f,
                Text = () => "Pick up what you dropped",
                On = () => _bagGo != null,
                Act = TakeBag,
            });
            _bagUse = Rc.World.Uses[Rc.World.Uses.Count - 1];
            Rc.World.Uses.Add(new RcUse
            {
                Radius = 2.4f,
                Text = () => "Help " + (_helpPeer != null ? _helpPeer.Name : "them") + " up",
                On = () => _helpPeer != null && _helpPeer.Live && _helpPeer.Down,
                Act = () => { RcNet.Revive(_helpPeer); Rc.Music.Jingle(RcMusic.J_HEAL); },
            });
            _helpUse = Rc.World.Uses[Rc.World.Uses.Count - 1];
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= PreCull;
            RestoreSpeed();
            if (_cc != null && _stepOffset >= 0f) _cc.stepOffset = _stepOffset;
            if (_rig != null) _rig.Destroy();
            if (_bubble != null) _bubble.Destroy();
            if (_cam != null && _farWas > 0f) { _cam.nearClipPlane = _nearWas; _cam.farClipPlane = _farWas; }
            if (_cam != null) _cam.transform.localPosition = _camLocal;
            if (_kid != null) Destroy(_kid);
            if (_bagGo != null) Destroy(_bagGo);
            if (_fps != null) _fps.enabled = true;
        }

        public void Teleport(Vector3 town, float yaw)
        {
            Dismount(null);
            _fps.TeleportPlayer(Rc.W(town.x, town.y, town.z) + Vector3.up * 0.05f, new Vector3(0f, yaw, 0f));
            _feet = Rc.W(town.x, town.y, town.z);
            _camDist = 1.2f;
            Climbing = false;
        }

        // ── Speed: walking and running are twice as fast here, plus what your gear adds (half that behind a block) ──
        private const float SPEED_BOOST = 2f;
        private static readonly System.Reflection.FieldInfo WalkField = typeof(FirstPersonController).GetField("m_WalkSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo RunField = typeof(FirstPersonController).GetField("m_RunSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo MoveDirField = typeof(FirstPersonController).GetField("m_MoveDir", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.FieldInfo EyeField = typeof(FirstPersonController).GetField("m_OriginalCameraPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private bool _boosted;
        private float _walk, _run;

        // ── Mounts: a toy you ride (V). +55% speed, but no fighting, and a hit throws you off. ──
        public bool Mounted { get; private set; }
        public int MountType = -1;
        private RcMountView _mountView;

        private void ToggleMount()
        {
            if (Mounted) { Dismount(null); return; }
            if (Dead || Climbing || Rolling || Cinematic || Waiting) return;
            RcItem best = null;
            for (int i = 0; i < Rc.Char.Backpack.Count; i++)
            {
                RcItem it = RcData.Item(Rc.Char.Backpack[i].Id);
                if (it != null && it.Kind == RcKind.Mount && (best == null || it.Id > best.Id)) best = it;
            }
            if (best == null) { Rc.Hud.Feed("You don't have a mount. Make one at the crafting bench (it's in the list under the toys it's made from)."); return; }
            SetBlocking(false); LetGo();
            Mounted = true;
            MountType = best.Id - RcData.MOUNT0;
            _mountView = new RcMountView(MountType);
            BoostSpeed();
            Rc.Music.Jingle(RcMusic.J_STEP);
            Rc.Hud.Feed("You're riding the " + best.Name + ": +" + best.SpeedPct + "% speed. V to get off. A hit throws you off.");
        }

        /// <summary>Off the mount (a message says why, or null).</summary>
        public void Dismount(string why)
        {
            if (!Mounted) return;
            Mounted = false;
            MountType = -1;
            if (_mountView != null) { _mountView.Destroy(); _mountView = null; }
            BoostSpeed();
            if (why != null) Rc.Hud.Feed(why);
        }

        private void BoostSpeed()
        {
            if ((object)WalkField == null || (object)RunField == null) return;
            if (!_boosted) { _walk = (float)WalkField.GetValue(_fps); _run = (float)RunField.GetValue(_fps); _boosted = true; }
            float k = SPEED_BOOST * Rc.Char.SpeedMult * (Mounted ? 1f + RcData.MOUNT_SPEED / 100f : 1f) * (Blocking ? 0.5f : 1f);
            WalkField.SetValue(_fps, _walk * k);
            RunField.SetValue(_fps, _run * k);
        }

        private void RestoreSpeed()
        {
            if (!_boosted || _fps == null) return;
            WalkField.SetValue(_fps, _walk);
            RunField.SetValue(_fps, _run);
            _boosted = false;
        }

        /// <summary>Your gear changed: speed and health follow it.</summary>
        public void GearChanged()
        {
            BoostSpeed();
            if (Health > MaxHealth) Health = MaxHealth;
            if (_rig != null) _rig.Forget();
        }

        // ── Every frame ──────────────────────────────────────────────────────
        private void Update()
        {
            if (_fps == null || Rc.Char == null || !Rc.World.Built) return;
            _feet = WorldRays.FeetOf(_fps.transform.position);
            if (Cinematic) return;

            if (Dead)
            {
                // Down. With a friend on their feet you lie there until one helps you up (or you give up, or time
                // runs out); alone, you wake in the treehouse.
                float down = Time.time - _deadAt;
                if (_helpable && !RcNet.FriendStanding) { _helpable = false; _deadAt = Time.time - 1f; }
                float wait = _helpable ? 30f : 2.6f;
                Rc.Hud.DownClock(_helpable ? wait - down : -1f);
                if (down > wait || (_helpable && down > 1.5f && Input.GetKeyDown(KeyCode.Space))) WakeUp();
                return;
            }

            // A Lid Shield runs out; out of a fight you slowly mend.
            if (Shield > 0f && Time.time > _shieldUntil) Shield = 0f;
            if (Time.time - _lastHurtAt > 8f && Health < MaxHealth) Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.02f * Rc.Char.HealingMult * Time.deltaTime);
            if (Time.time > _gearAt) { _gearAt = Time.time + 1f; BoostSpeed(); }

            // The dodge carries you, whatever the menus are doing: quick off the mark, slowing as you come out of the roll.
            if (Time.time < _dodgeUntil && _cc != null)
            {
                float k = 1f - (_dodgeUntil - Time.time) / RcRoll.TIME;
                _cc.Move(_dodgeDir * (ROLL_DISTANCE / RcRoll.TIME) * (1.6f - 1.2f * Mathf.Clamp01(k)) * Time.deltaTime);
            }

            // The delayed part of a swing: the hit lands a moment after it starts.
            if (_hitAt > 0f && Time.time >= _hitAt) { _hitAt = -1f; LandSwing(); }

            if (Climbing) { ClimbTick(); return; }

            if (OverlayPanel.AnyOpen || OverlayPanel.BlocksWorld) { Rc.Hud.Prompt(null); SetBlocking(false); LetGo(); return; }

            // A friend who's down, within reach?
            _helpPeer = null;
            for (int i = 0; i < RcNet.Peers.Count; i++)
            {
                RcPeer peer = RcNet.Peers[i];
                if (!peer.Live || !peer.Down || (peer.Pos - TownPos).sqrMagnitude > 2.4f * 2.4f) continue;
                _helpPeer = peer; _helpUse.Pos = peer.Pos;
            }

            // What you could use here.
            RcUse use = Rc.World.NearestUse(TownPos);
            Rc.Hud.Prompt(use != null ? "[E]  " + use.Text() : null);
            if (use != null && Input.GetKeyDown(KeyCode.E)) { SetBlocking(false); LetGo(); use.Act(); return; }

            int slot = Slot;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Slot = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) Slot = 1;
            if (Input.GetKeyDown(KeyCode.Q) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) Slot = 1 - Slot;
            if (Slot != slot) LetGo();
            if (Input.GetKeyDown(KeyCode.F)) { LightOn = !LightOn; Rc.Music.Jingle(RcMusic.J_STEP); }
            if (Input.GetKeyDown(KeyCode.V)) ToggleMount();
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I)) { RcBagMenu.Open(); return; }
            if (Input.GetKeyDown(KeyCode.J)) { RcObjectiveMenu.Open(); return; }
            if (Input.GetKeyDown(KeyCode.M)) { RcMapMenu.Open(); return; }
            if (Input.GetKeyDown(KeyCode.H)) Rc.Hud.Controls();
            if (Input.GetKeyDown(KeyCode.P)) { RcPartyMenu.Open(); return; }
            // With friends: G marks the spot you're looking at, 5 to 8 say something.
            if (RcNet.Peers.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.G)) { RcNet.Ping(AimPoint()); Rc.Music.Jingle(RcMusic.J_PICKUP); }
                for (int i = 0; i < RcNet.Emotes.Length; i++) if (Input.GetKeyDown(KeyCode.Alpha5 + i)) RcNet.Emote(i);
            }

            // Right click: your weapon comes up in front of you. You can't attack from behind it.
            RcItem w = Weapon;
            if (Mounted) { SetBlocking(false); LetGo(); }   // (you can't fight from a wagon)
            else SetBlocking(Input.GetMouseButton(1) && w != null && Time.time >= _dodgeUntil && (Blocking || Time.time >= _blockLockUntil));
            if (Mounted) { }
            else if (Blocking) LetGo();
            else
            {
                if ((Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.C)) && Time.time >= _dodgeReadyAt) Dodge();
                if (Time.time >= _dodgeUntil) AttackInput(w); else LetGo(); // (no swinging mid-roll)
            }

            // Running is loud; walking isn't.
            if (Time.time >= _stepNoiseAt && _cc != null)
            {
                _stepNoiseAt = Time.time + 0.45f;
                float speed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
                if (speed > _walk * SPEED_BOOST * 1.25f) Noise(9f);
            }
        }

        private void SetBlocking(bool on)
        {
            if (on == Blocking) return;
            Blocking = on;
            if (on) _blockAt = Time.time; else _blockLockUntil = Time.time + BLOCK_LOCK;
            BoostSpeed();
        }

        /// <summary>Whatever you were drawing back or winding up is let down without being used.</summary>
        private void LetGo() { _charging = false; _charge = 0f; }

        // ── The ladder: you hang on it and climb with W and S; you step off at the ground and at each deck ──
        private float _climbY, _climbStart, _climbFar;

        private static float[] LadderLevels() { return new[] { 0f, RcTown.DeckY[0], RcTown.DeckY[1], RcTown.DeckY[2] }; }

        public void GrabLadder()
        {
            if (Dead || Climbing) return;
            Dismount(null);
            float y = TownPos.y, best = 0f;
            foreach (float level in LadderLevels()) if (Mathf.Abs(level - y) < Mathf.Abs(best - y)) best = level;
            Climbing = true;
            _climbY = _climbStart = best;
            _climbFar = 0f;
            Rc.Music.Jingle(RcMusic.J_STEP);
            HoldLadder();
        }

        private void ClimbTick()
        {
            float[] levels = LadderLevels();
            float top = levels[levels.Length - 1], prev = _climbY, v = Input.GetAxisRaw("Vertical");
            _climbY = Mathf.Clamp(_climbY + v * 2.6f * Time.deltaTime, 0f, top);
            _climbFar = Mathf.Max(_climbFar, Mathf.Abs(_climbY - _climbStart));
            Rc.Hud.Prompt("[W] up    [S] down    [E] let go");
            if (Mathf.FloorToInt(_climbY / 0.42f) != Mathf.FloorToInt(prev / 0.42f)) Rc.Music.Jingle(RcMusic.J_STEP); // a rung

            // You step off wherever you arrive: the ground, or a deck (not the one you started from, unless you came back to it).
            foreach (float level in levels)
            {
                bool started = Mathf.Abs(level - _climbStart) < 0.1f;
                bool arrived = (prev < level && _climbY >= level) || (prev > level && _climbY <= level);
                if (arrived && (!started || _climbFar > 0.6f)) { StepOff(level); return; }
            }
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            {
                // Level with a deck (or halfway through its hatch): onto it. Otherwise you just drop.
                for (int i = levels.Length - 1; i >= 0; i--)
                    if (_climbY > levels[i] - 1.9f && _climbY < levels[i] + 1f) { StepOff(levels[i]); return; }
                Climbing = false;
                return;
            }
            HoldLadder();
        }

        private void StepOff(float level)
        {
            Climbing = false;
            Place(Rc.W(RcTown.LADDER_X, level + 0.05f, level < 0.1f ? RcTown.LADDER_OFF_GROUND_Z : RcTown.LADDER_OFF_DECK_Z));
            _camDist = 1.2f;
        }

        /// <summary>Keeps you on the ladder (the game's own walking and falling is undone every frame).</summary>
        private void HoldLadder() { Place(Rc.W(RcTown.LADDER_X, _climbY, RcTown.LADDER_Z)); }

        private void Place(Vector3 feetWorld)
        {
            Vector3 pivot = _fps.transform.position - WorldRays.FeetOf(_fps.transform.position);
            _fps.transform.position = feetWorld + pivot;
            _feet = feetWorld;
            if ((object)MoveDirField != null) MoveDirField.SetValue(_fps, Vector3.zero);
        }

        /// <summary>The toys hear this from 'radius' meters.</summary>
        public void Noise(float radius)
        {
            LastNoiseAt = Time.time;
            if (Rc.Enemies != null) Rc.Enemies.Noise(_feet, radius);
        }

        /// <summary>Up in the treehouse: you mend fast.</summary>
        public void Mend(float dt)
        {
            if (!Dead && Health < MaxHealth) Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.25f * dt);
        }

        public void Heal(float amount) { if (!Dead) Health = Mathf.Min(MaxHealth, Health + amount); }

        /// <summary>Puts items in your backpack (and says so); what doesn't fit is lost, with a warning.</summary>
        public void Give(int id, int count)
        {
            RcItem it = RcData.Item(id);
            if (it == null || count <= 0) return;
            int left = Rc.Char.Add(id, count);
            if (count - left > 0) Rc.Hud.Feed("+" + (count - left) + "  " + it.Name);
            if (left > 0) Rc.Hud.Feed("Backpack full: " + left + " " + it.Name + " left behind.");
            if (it.Id == RcData.GOLD_FUR || it.Id == RcData.DIAMOND_EYE || it.Id >= RcData.THREAD && it.Id <= RcData.MAMAS_EYE) Rc.Music.Jingle(RcMusic.J_RARE);
        }

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            if (Rc.Char.AddXp(amount))
            {
                Health = MaxHealth;
                Rc.Hud.Toast("LEVEL " + Rc.Char.Level, Rc.Char.Level >= RecalledRules.LEVEL_CAP ? "That's as far as this town goes." : "New gear is within reach.");
                Rc.Music.Jingle(RcMusic.J_LEVEL);
                Rc.Save();
            }
        }

        // ── Fighting ─────────────────────────────────────────────────────────
        public RcItem Weapon { get { return Rc.Char.Weapon(Slot); } }

        /// <summary>0..1: how far a slot's gadget is from ready (0 = ready).</summary>
        public float GadgetWait(int slot)
        {
            RcItem it = Rc.Char.Weapon(slot);
            if (it == null || it.Kind != RcKind.Gadget) return 0f;
            return Mathf.Clamp01((_gadgetReadyAt[slot] - Time.time) / (it.Rate * Rc.Char.CooldownMult));
        }

        private Vector3 Chest { get { return _feet + Vector3.up * 0.72f; } }

        /// <summary>The point in the world the crosshair is on.</summary>
        private Vector3 AimPoint()
        {
            if (_cam == null) return Chest + Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward * 30f;
            Vector3 from = _cam.transform.position, dir = _cam.transform.forward;
            RaycastHit hit;
            if (WorldRays.Raycast(from, dir, 80f, out hit) && hit.distance > 2f) return hit.point;
            return from + dir * 60f;
        }

        private const float MIN_CHARGE = 0.3f, HEAVY_TIME = 0.9f;

        /// <summary>
        /// Left click. A ranged weapon is drawn back while you hold and fires when you let go: the longer the draw,
        /// the harder and further it hits (a quick tap does nothing). A melee weapon swings on the click; keep
        /// holding and it winds up a heavy swing for when you let go. A gadget just goes off.
        /// </summary>
        private void AttackInput(RcItem w)
        {
            bool down = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0), ready = Time.time >= _attackAt;
            if (w == null)
            {
                // Bare hands: a shove. Better than nothing, barely.
                if (down && ready) { _attackAt = Time.time + 0.7f; _swingAt = Time.time; _hitAt = Time.time + 0.12f; _swingPower = 1f; _swingHeavy = false; _swingCounter = Counter(); SwingCount++; }
                return;
            }
            switch (w.Kind)
            {
                case RcKind.Melee:
                    if (down && ready) Swing(w, 0f);
                    else if (held && ready) { _charging = true; _charge = Mathf.Min(1f, _charge + Time.deltaTime / HEAVY_TIME); }
                    else if (_charging) { if (_charge >= MIN_CHARGE) Swing(w, _charge); LetGo(); }
                    break;
                case RcKind.Ranged:
                    if (held && ready) { _charging = true; _charge = Mathf.Min(1f, _charge + Time.deltaTime * w.Rate); }
                    else if (_charging) { if (_charge >= MIN_CHARGE) Shoot(w, _charge); LetGo(); }
                    break;
                case RcKind.Gadget:
                    if (!down || !ready) break;
                    if (Time.time < _gadgetReadyAt[Slot]) { _attackAt = Time.time + 0.2f; break; }
                    _gadgetReadyAt[Slot] = Time.time + w.Rate * Rc.Char.CooldownMult;
                    _attackAt = Time.time + 0.5f;
                    _castAt = Time.time;
                    CastCount++;
                    UseGadget(w);
                    break;
            }
        }

        private void Swing(RcItem w, float charge)
        {
            _swingHeavy = charge > 0f;
            _swingPower = 1f + charge;                               // a full wind-up hits twice as hard
            _attackAt = Time.time + 1f / w.Rate + (_swingHeavy ? 0.25f : 0f);
            _swingAt = Time.time;
            _hitAt = Time.time + 0.12f;
            _swingCounter = Counter();
            SwingCount++;
            Rc.Music.Jingle(RcMusic.J_SWING);
        }

        private float _swingCounter = 1f;

        /// <summary>The damage multiplier for a blow struck now: more for the first one after a perfect block (which it uses up).</summary>
        private float Counter()
        {
            if (Time.time >= _counterUntil) return 1f;
            _counterUntil = 0f;
            Rc.Hud.Number(Chest + Vector3.up * 0.6f, "COUNTER", new Color(1f, 0.85f, 0.3f));
            return COUNTER_POWER;
        }

        private void Shoot(RcItem w, float charge)
        {
            _attackAt = Time.time + 0.15f;
            _swingAt = Time.time;
            SwingCount++;
            Vector3 from = Chest + Quaternion.Euler(0f, _kidYaw, 0f) * new Vector3(0.22f, 0.1f, 0.35f);
            float k = Mathf.InverseLerp(MIN_CHARGE, 1f, charge);
            Rc.Enemies.Fire(from, (AimPoint() - from).normalized, Mathf.Lerp(24f, 46f, k), w.Power * Rc.Char.PowerMult * Mathf.Lerp(0.35f, 1f, k) * Counter(), w.Range * Mathf.Lerp(0.5f, 1f, k), true, w.Zone);
            Noise(w.Noise);
            Rc.Music.Jingle(RcMusic.J_SHOT);
        }

        private void LandSwing()
        {
            RcItem w = Weapon;
            bool fists = w == null || w.Kind != RcKind.Melee;
            float dmg = (fists ? 4f : w.Power * Rc.Char.PowerMult) * _swingPower * _swingCounter, range = (fists ? 1.5f : w.Range) + (_swingHeavy ? 0.4f : 0f);
            int hits = Rc.Enemies.HitArc(_feet, _kidYaw, range, _swingHeavy ? 85f : 62f, dmg);
            if (hits > 0) { Noise((fists ? 4f : w.Noise) * (_swingHeavy ? 1.5f : 1f)); Rc.Music.Jingle(RcMusic.J_HIT); }
            if (_swingHeavy) Rc.Hud.Shake(0.12f);
        }

        private void UseGadget(RcItem w)
        {
            Noise(w.Noise);
            switch (w.Ability)
            {
                case RcAbility.Shock:
                    Rc.Enemies.Blast(_feet, w.Range, w.Power * Rc.Char.PowerMult, 1.3f);
                    Rc.Enemies.Ring(_feet, w.Range, new Color(0.5f, 0.8f, 1f), 0.45f);
                    Rc.Music.Jingle(RcMusic.J_SHOCK);
                    break;
                case RcAbility.Repair:
                    Heal(MaxHealth * w.Power / 100f * Rc.Char.HealingMult);
                    Rc.Enemies.Ring(_feet, 1.6f, new Color(0.5f, 1f, 0.6f), 0.6f);
                    Rc.Music.Jingle(RcMusic.J_HEAL);
                    break;
                case RcAbility.Decoy:
                    Vector3 at = _feet + Quaternion.Euler(0f, _kidYaw, 0f) * Vector3.forward * 3.5f;
                    Rc.Enemies.SetDecoy(at, 8f);
                    Rc.Music.Jingle(RcMusic.J_PICKUP);
                    break;
                case RcAbility.Shield:
                    Shield = w.Power;
                    _shieldUntil = Time.time + 6f;
                    Rc.Enemies.Ring(_feet, 1.4f, new Color(1f, 0.85f, 0.4f), 0.6f);
                    Rc.Music.Jingle(RcMusic.J_HEAL);
                    break;
            }
        }

        private const float ROLL_DISTANCE = 3.8f;

        private void Dodge()
        {
            float x = Input.GetAxisRaw("Horizontal"), z = Input.GetAxisRaw("Vertical");
            Quaternion yaw = Quaternion.Euler(0f, _cam != null ? _cam.transform.eulerAngles.y : Yaw, 0f);
            Vector3 dir = yaw * new Vector3(x, 0f, z);
            if (dir.sqrMagnitude < 0.01f) dir = yaw * Vector3.back; // standing still: hop back
            _dodgeDir = dir.normalized;
            _dodgeUntil = Time.time + RcRoll.TIME;
            _iframesUntil = Time.time + 0.42f;   // (hits pass through you for most of the roll, not the very end)
            _dodgeReadyAt = Time.time + 1.1f;
            _kidYaw = RollYaw;
            LetGo();
            Noise(5f);
            Rc.Music.Jingle(RcMusic.J_SWING);
        }

        /// <summary>A hit that comes from somewhere (a toy, a shot): your block stops most of it if you're facing it.</summary>
        public bool Damage(float amount, Vector3 fromWorld)
        {
            if (Dead || Cinematic || Time.time < _iframesUntil) return false;
            Vector3 to = fromWorld - _feet; to.y = 0f;
            if (Blocking && Weapon != null && (to.sqrMagnitude < 0.04f || Vector3.Angle(Quaternion.Euler(0f, _kidYaw, 0f) * Vector3.forward, to) < 80f))
            {
                _blockedAt = Time.time;
                if (Time.time - _blockAt < PARRY_WINDOW)
                {
                    // A perfect block: nothing gets through, whoever's swinging is thrown off balance, and your next blow counters.
                    _counterUntil = Time.time + COUNTER_TIME;
                    _iframesUntil = Time.time + 0.3f;
                    if (_bubble != null) _bubble.Hit(true);
                    Rc.Enemies.Parried(fromWorld);
                    Rc.Enemies.Ring(_feet, 2.8f, new Color(0.45f, 0.85f, 1f), 0.55f);
                    Rc.Hud.Number(Chest + Vector3.up * 0.6f, "PERFECT BLOCK", new Color(0.6f, 0.95f, 1f));
                    Rc.Hud.Shake(0.18f);
                    Rc.Music.Jingle(RcMusic.J_SHOCK);
                    return false;
                }
                // Otherwise a bat or a wrench stops more than a slingshot does.
                amount *= Weapon.Kind == RcKind.Melee ? 0.25f : 0.5f;
                if (_bubble != null) _bubble.Hit(false);
                Rc.Hud.Number(Chest + Vector3.up * 0.5f, "blocked", new Color(0.7f, 0.85f, 1f));
                Rc.Music.Jingle(RcMusic.J_HIT);
                if (amount <= 0f) return false;
            }
            return Damage(amount);
        }

        /// <summary>A toy hits you. False if it didn't land (you were dodging, or already down).</summary>
        public bool Damage(float amount)
        {
            if (Dead || Cinematic || Time.time < _iframesUntil) return false;
            if (Mounted) Dismount("The hit threw you off.");
            amount *= Rc.Char.DamageTaken; // your armor takes its share of every hit
            if (Shield > 0f)
            {
                float soak = Mathf.Min(Shield, amount);
                Shield -= soak; amount -= soak;
                if (amount <= 0f) { Rc.Music.Jingle(RcMusic.J_HIT); return true; }
            }
            Health -= amount;
            _lastHurtAt = Time.time;
            _iframesUntil = Time.time + 0.35f; // (a pack can't take you apart in one frame)
            Rc.Hud.Hurt();
            Rc.Music.Jingle(RcMusic.J_HURT);
            if (Health <= 0f) Die();
            return true;
        }

        // ── Going down ───────────────────────────────────────────────────────
        private readonly List<RcStack> _bag = new List<RcStack>();
        private GameObject _bagGo;
        private RcUse _bagUse;

        private RcUse _helpUse;
        private RcPeer _helpPeer;
        private bool _helpable;
        private Vector3 _fellAt;

        /// <summary>Anyone within reach you could hand something to.</summary>
        public RcPeer NearestFriend(float reach)
        {
            RcPeer best = null;
            float d = reach * reach;
            for (int i = 0; i < RcNet.Peers.Count; i++)
            {
                RcPeer peer = RcNet.Peers[i];
                float k = (peer.Pos - TownPos).sqrMagnitude;
                if (peer.Live && k < d) { d = k; best = peer; }
            }
            return best;
        }

        private void Die()
        {
            Health = 0f;
            Dead = true;
            Climbing = false;
            SetBlocking(false);
            LetGo();
            _deadAt = Time.time;
            _fellAt = _feet;
            _helpable = RcNet.FriendStanding;
            _fps.enabled = false;
            Rc.Char.Deaths++;
            var anim = _kid != null ? _kid.GetComponent<KidAnimator>() : null;
            if (anim != null) anim.Flinch();
            bool carrying = false;
            for (int i = 0; i < Rc.Char.Backpack.Count; i++) { RcItem it = RcData.Item(Rc.Char.Backpack[i].Id); if (it != null && !it.IsGear) carrying = true; }
            Rc.Hud.Died(_helpable, carrying);
            Rc.Music.Jingle(RcMusic.J_DEATH);
        }

        /// <summary>A friend got you back on your feet, right where you fell.</summary>
        public void GetUp()
        {
            if (!Dead) return;
            Dead = false;
            var anim = _kid != null ? _kid.GetComponent<KidAnimator>() : null;
            if (anim != null) anim.ResetFlinch();
            _fps.enabled = true;
            Health = MaxHealth * 0.4f;
            Shield = 0f;
            _iframesUntil = Time.time + 2f;
            _lastHurtAt = Time.time;
            Rc.Music.Jingle(RcMusic.J_HEAL);
        }

        private void WakeUp()
        {
            Dead = false;
            var anim = _kid != null ? _kid.GetComponent<KidAnimator>() : null;
            if (anim != null) anim.ResetFlinch();
            _fps.enabled = true;
            Health = MaxHealth;
            Shield = 0f;
            // The materials you were carrying stay where you fell (anything from an earlier fall is gone).
            _bag.Clear();
            for (int i = Rc.Char.Backpack.Count - 1; i >= 0; i--)
            {
                RcItem it = RcData.Item(Rc.Char.Backpack[i].Id);
                if (it == null || it.IsGear) continue;
                _bag.Add(Rc.Char.Backpack[i]);
                Rc.Char.Backpack.RemoveAt(i);
            }
            if (_bagGo != null) Destroy(_bagGo);
            _bagGo = null;
            if (_bag.Count > 0)
            {
                _bagGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _bagGo.name = "KE_RcBag";
                Destroy(_bagGo.GetComponent<Collider>());
                _bagGo.transform.position = _fellAt + Vector3.up * 0.2f;
                _bagGo.transform.localScale = new Vector3(0.45f, 0.4f, 0.35f);
                _bagGo.GetComponent<Renderer>().sharedMaterial = Mat.Unlit(new Color(0.9f, 0.65f, 0.1f));
                var l = _bagGo.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.75f, 0.3f); l.range = 5f; l.intensity = 1.2f;
                _bagUse.Pos = Rc.T(_fellAt);
            }
            Teleport(new Vector3(0f, RcTown.DeckY[0], -3.9f), 0f);
            Rc.Hud.Toast("THE TREEHOUSE", _bag.Count > 0 ? "What you were carrying is where you fell." : "You're safe.");
            Rc.Save();
        }

        /// <summary>Puts you somewhere without the camera cut of a teleport (kept out of a boss fight you weren't there for).</summary>
        public void MoveTo(Vector3 town) { Place(Rc.W(town.x, town.y, town.z)); }

        private void TakeBag()
        {
            for (int i = 0; i < _bag.Count; i++) Give(_bag[i].Id, _bag[i].Count);
            _bag.Clear();
            Destroy(_bagGo);
            _bagGo = null;
            Rc.Music.Jingle(RcMusic.J_PICKUP);
        }

        // ── Third person: your kid, and the camera behind their shoulder ─────
        private const float CAM_DIST = 2.6f, CAM_UP = 0.3f, CAM_SIDE = 0.6f;
        private GameObject _kid;
        private string _kidKey;
        private Vector3 _camLocal;
        private float _kidYaw, _camDist = CAM_DIST;
        private RcKidRig _rig;
        private float _nearWas, _farWas;

        private void LateUpdate()
        {
            if (_fps == null || Rc.Char == null || !Rc.World.Built) return;
            if (Climbing) HoldLadder();
            if (_cam == null)
            {
                _cam = FactoryController.PlayerCam;
                if (_cam == null) return;
                // Depth precision. The game's camera is set up for a house (it sees things a hand's width away); the
                // town is hundreds of meters, and with a near plane that close two surfaces a few centimeters apart
                // (a sign on a wall, a window frame, a line on the road) flicker into each other from a distance.
                // Third person never needs to see closer than this, and the night fog hides everything past 135 m.
                _nearWas = _cam.nearClipPlane; _farWas = _cam.farClipPlane;
                _cam.nearClipPlane = Mathf.Max(_nearWas, 0.2f);
                _cam.farClipPlane = 320f;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Camera near " + _nearWas + " -> " + _cam.nearClipPlane + ", far " + _farWas + " -> " + _cam.farClipPlane + ".");
                // The eye's resting place (the game bobs the camera around it as you walk: in third person that
                // would shake the whole view, so the camera hangs off the resting place instead).
                _camLocal = (object)EyeField != null ? (Vector3)EyeField.GetValue(_fps) : _cam.transform.localPosition;
                if (_camLocal == Vector3.zero) _camLocal = _cam.transform.localPosition;
                _kidYaw = _fps.transform.eulerAngles.y;
            }

            // Your kid (the one from OUTFITS; the blank mannequin if the Arcade still has your identity).
            string key = KidSkins.Code + "|" + (KidSkins.Gender == KidGender.Custom ? CharacterSpec.LocalString : "");
            if (_kid == null || key != _kidKey)
            {
                if (_kid != null) Destroy(_kid);
                _kid = Identity.Forgotten ? ChildModel.Build("KE_RecalledSelf", KidGender.Boy, KidSkins.MANNEQUIN) : ChildModel.Build("KE_RecalledSelf", KidSkins.Gender, KidSkins.Selected);
                _kidKey = key;
                // (no shadow of their own: the flashlight is at their chest, and their arms would black out the street)
                foreach (var r in _kid.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (_rig != null) _rig.Destroy();
                _rig = new RcKidRig(_kid);
                _kidAnim = _kid.GetComponent<KidAnimator>();
            }
            Vector3 feet = WorldRays.FeetOf(_fps.transform.position);
            if (Cinematic)
            {
                // (the trailer films without you: no kid, no torch, no guard, and the camera is its own)
                if (_kid.activeSelf) _kid.SetActive(false);
                if (_torch != null) _torch.enabled = false;
                if (_bubble != null) _bubble.Tick(false, feet, false);
                return;
            }
            if (!_kid.activeSelf) _kid.SetActive(true);
            if (Climbing) _kidYaw = Mathf.MoveTowardsAngle(_kidYaw, 0f, 900f * Time.deltaTime); // (facing the trunk)
            else if (Mounted && !Dead)
            {
                // (on a mount you face the way you're going)
                Vector3 v = _cc != null ? _cc.velocity : Vector3.zero; v.y = 0f;
                if (v.magnitude > 0.6f) _kidYaw = Mathf.MoveTowardsAngle(_kidYaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 420f * Time.deltaTime);
            }
            else if (!Dead && !Rolling) _kidYaw = Mathf.MoveTowardsAngle(_kidYaw, _cam.transform.eulerAngles.y, 900f * Time.deltaTime);
            bool riding = Mounted && !Dead && _mountView != null;
            if (_mountView != null && _mountView.Root != null && _mountView.Root.activeSelf != riding) _mountView.Root.SetActive(riding);
            if (riding) _mountView.Place(feet, _kidYaw);
            _kid.transform.position = riding ? feet + Vector3.up * (RcMountView.Seat[_mountView.Type] - 0.2f) : feet;
            _kid.transform.rotation = Quaternion.Euler(0f, _kidYaw, 0f);
            if (Rolling && !Dead)
            {
                float tuck = RcRoll.Apply(_kid.transform, feet, _dodgeDir, 1f - (_dodgeUntil - Time.time) / RcRoll.TIME);
                if (_kidAnim != null) _kidAnim.Tuck = tuck;
            }
            else if (_kidAnim != null) _kidAnim.Tuck = riding ? 0.55f : 0f;   // (legs folded: sitting)
            _rig.Hold(Dead || Mounted ? null : Weapon);

            // The guard: a ball of light round you while you hold a block.
            if (_bubble == null) _bubble = new RcBubble();
            _bubble.Tick(Blocking && !Dead, feet, Blocking && Time.time - _blockAt < PARRY_WINDOW);

            // The camera: behind and to the right of the kid's head, along where you're looking; pulled in by walls.
            Transform camT = _cam.transform;
            Vector3 eye = camT.parent != null ? camT.parent.TransformPoint(_camLocal) : camT.position;
            Vector3 pivot = eye + Vector3.up * 0.15f;
            Vector3 want = pivot - camT.forward * CAM_DIST + Vector3.up * CAM_UP + camT.right * CAM_SIDE;
            Vector3 dir = want - pivot;
            float dist = dir.magnitude;
            dir /= dist;
            float free = dist;
            RaycastHit[] hits = Physics.RaycastAll(pivot, dir, dist + 0.25f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.isTrigger || c.transform.IsChildOf(_fps.transform)) continue; // (not your own capsule)
                free = Mathf.Min(free, hits[i].distance - 0.25f);
            }
            // In at once when something's in the way (never look through a wall), back out gently when it clears.
            free = Mathf.Max(0.3f, free);
            _camDist = free < _camDist ? free : Mathf.MoveTowards(_camDist, free, Time.deltaTime * 3.5f);
            camT.position = pivot + dir * _camDist;

            // Your flashlight: from your chest, along where you look.
            if (_torch != null)
            {
                _torch.enabled = LightOn && !Dead;
                // With toys on you it gutters: steady and calm out in the open, a nervous flicker in a fight.
                int danger = Rc.Enemies != null ? Rc.Enemies.Danger : 0;
                float shake = danger >= 2 ? 0.28f : danger == 1 ? 0.14f : 0.03f;
                _torch.intensity = 1.8f * (1f - shake * Mathf.PerlinNoise(3.7f, Time.time * 11f));
                _torch.transform.position = feet + Vector3.up * 0.8f + Quaternion.Euler(0f, _kidYaw, 0f) * Vector3.forward * 0.25f;
                _torch.transform.rotation = camT.rotation;
            }
        }

        // Your arms and what's in your hands (RcKidRig: the same code draws your friends), over the walk cycle:
        // just before the camera draws, after the animator has posed the kid.
        private void PreCull(Camera cam)
        {
            if (cam != _cam || _kid == null || Dead || _rig == null || Rolling || Mounted) return; // (mid-roll the arms are tucked in)
            _rig.Pose(Weapon, Charge, Time.time - _swingAt, Time.time - _castAt, _swingHeavy, Blocking, Time.time - _blockedAt, Climbing, _climbY);
        }
    }
}
