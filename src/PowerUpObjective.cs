using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum PowerUpAbility { MaxStats, StrongBattery, TotemOfUndying }

    /// <summary>
    /// "Power Ups" run modifier — each round (each new objective) there's a
    /// 20% chance one coin spawns at a random hand-marked spot with a
    /// random ability, floating just above the ground and spinning in
    /// place. Walking near it (not grabbing it) reveals which ability it
    /// is at the bottom-middle of the screen; grabbing it applies that
    /// ability immediately. All positions below are real E-key marks
    /// (DebugLocationMarker), not estimates.
    /// </summary>
    public class PowerUpObjective : MonoBehaviour
    {
        public static PowerUpObjective Instance { get; private set; }

        // Debug-only marks (E-key marks 1-3) — spawned one of each ability
        // every round while this was true for testing. Real gameplay uses
        // the 20%-chance single-coin pool below now.
        public static bool DebugSpawnAllPowerUps = false;
        // Endless in the Waygetter Facility uses the owner's marks (FacilityMarks) instead.
        private static Vector3[] SpawnPositions { get { return FactoryMode.EndlessPreview ? FacilityMarks.World(FacilityMarks.Coin) : HouseSpawnPositions; } }

        private static readonly Vector3[] DebugPositions =
        {
            new Vector3(4.2f, 1.1f, 2.0f),
            new Vector3(4.6f, 1.1f, -3.7f),
            new Vector3(4.1f, 1.1f, 6.0f),
        };
        private static readonly PowerUpAbility[] DebugAbilities =
        {
            PowerUpAbility.MaxStats, PowerUpAbility.StrongBattery, PowerUpAbility.TotemOfUndying,
        };

        // Real spawn pool (E-key marks 4-11) — one coin, 20% chance per
        // round, random position + random ability.
        private static readonly Vector3[] HouseSpawnPositions =
        {
            new Vector3(4.4f, 0.7f, 10.9f),
            new Vector3(-9.1f, 0.7f, 10.6f),
            new Vector3(2.4f, -2.0f, 1.0f),
            new Vector3(6.7f, -2.0f, -4.0f),
            new Vector3(9.7f, -2.0f, 6.3f),
            new Vector3(2.5f, -2.0f, -6.6f),
            new Vector3(-5.6f, -2.0f, -1.0f),
            new Vector3(7.6f, 0.7f, -8.5f),
        };

        private static float RoundSpawnChance => (RunPowerUps.CoinMagnet ? 0.4f : 0.2f) * EndlessTraits.CoinChanceMult(); // Coin Magnet shop power up doubles it (and so does Persistent Curiosity)
        private const float TOTEM_CHANCE = 0.05f;
        private const float HOVER_HEIGHT = 0.35f; // just above the ground it spawned near, not up near the ceiling/floor above
        private const float SPIN_SPEED = 160f;    // degrees/sec, around world Y — spins in place, doesn't drift (moving broke grabbing it mid-hold)
        private static float RevealRadius => (RunPowerUps.SharperEyes ? 4f : 2.5f) * EndlessTraits.CoinRevealMult();      // Sharper Eyes shop power up
        private const float STRONG_BATTERY_DURATION = 30f;
        private static int MaxTotems => RunPowerUps.BiggerPockets ? 4 : 3;              // Bigger Pockets shop power up

        private class PowerUpCoin
        {
            public GameObject GO;
            public PowerUpAbility Ability;
        }
        private readonly List<PowerUpCoin> _activeCoins = new List<PowerUpCoin>();

        private float _strongBatteryTimeRemaining;
        private int _totemCount;

        public float StrongBatteryTimeRemaining => _strongBatteryTimeRemaining;
        public int TotemCount => _totemCount;

        // AudioController.Play("music_sting_positive", ...) produced no
        // audible sound for the totem save in testing (same "no error, no
        // sound" shape as the Speaker objective's own reasoning for
        // avoiding it) — resolved to a real raw AudioClip instead (dug out
        // of AudioItem.subItems[i].Clip via a one-time reflection-free
        // lookup, confirmed real via decompile) and played through our own
        // dedicated 2D AudioSource, same proven pattern SpeakerObjective
        // already uses successfully.
        private static AudioClip _totemClip;
        private static bool _totemClipResolved;
        private AudioSource _sfxSource;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.spatialBlend = 0f;
            _sfxSource.playOnAwake = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;

            TickStrongBattery();
            TickCoins();
        }

        private void TickStrongBattery()
        {
            if (_strongBatteryTimeRemaining <= 0f) return;
            // Re-fills the charge every frame for the whole window — the
            // simplest possible "can't get dimmer" guarantee, since it
            // overrides whatever's draining it (vanilla shake-decay, Low
            // Battery modifier, anything) without needing to know or fight
            // any of those mechanisms individually.
            if (RM.flashlight != null) RM.flashlight.MaxLight();
            _strongBatteryTimeRemaining -= Time.deltaTime;
            if (_strongBatteryTimeRemaining < 0f) _strongBatteryTimeRemaining = 0f;
        }

        private void TickCoins()
        {
            if (_activeCoins.Count == 0) return;

            Vector3? playerPos = RM.fpsController != null ? RM.fpsController.transform.position : (Vector3?)null;
            PowerUpCoin nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var coin in _activeCoins)
            {
                if (coin.GO == null) continue;

                // Spins in place only — no drift. It used to orbit its
                // spawn point, which moved the collider out from under the
                // player mid-hold and made it impossible to actually
                // finish grabbing.
                coin.GO.transform.Rotate(Vector3.up, SPIN_SPEED * Time.deltaTime, Space.World);

                if (!playerPos.HasValue) continue;

                float dist = Vector3.Distance(playerPos.Value, coin.GO.transform.position);
                // Straight-line distance alone read as "through walls" in a
                // house this tightly packed — adjacent rooms are easily
                // within a few units of each other. A blocked line-of-
                // sight check (ignoring the coin's own trigger collider)
                // makes it only reveal when actually visible/nearby.
                bool inRange = dist <= RevealRadius &&
                    !Physics.Linecast(playerPos.Value, coin.GO.transform.position, out _, ~0, QueryTriggerInteraction.Ignore);
                if (inRange && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = coin;
                }
            }

            if (nearest != null)
                EndlessHud.Instance?.ShowAbilityHint(AbilityName(nearest.Ability));
            else
                EndlessHud.Instance?.HideAbilityHint();
        }

        /// <summary>
        /// Called from EndlessRunManager.BeginObjective() at the start of
        /// every round, gated on the Power Ups modifier — despawns
        /// whatever's still uncollected first. Debug mode always spawns
        /// one of each ability at the 3 fixed debug marks; normal mode
        /// rolls a flat 20% chance for a single random coin from the pool.
        /// </summary>
        public void SpawnCoin()
        {
            DespawnAllCoins();

            if (DebugSpawnAllPowerUps)
            {
                for (int i = 0; i < DebugPositions.Length; i++)
                    SpawnOneCoin(DebugPositions[i], DebugAbilities[i]);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PowerUp] Debug mode — spawned all 3 abilities.");
                return;
            }

            if (Random.value >= RoundSpawnChance) return; // most rounds: nothing spawns

            Vector3 pos = SpawnPositions[Random.Range(0, SpawnPositions.Length)];
            SpawnOneCoin(pos, RollAbility());
        }

        // Totems are extremely rare: 5% of coins, the other two split the rest evenly. Shared by normal coins and Gift from Mama.
        private static PowerUpAbility RollAbility()
        {
            float roll = Random.value;
            if (roll < TOTEM_CHANCE) return PowerUpAbility.TotemOfUndying;
            return roll < TOTEM_CHANCE + (1f - TOTEM_CHANCE) / 2f ? PowerUpAbility.MaxStats : PowerUpAbility.StrongBattery;
        }

        /// <summary>Gift from Mama event: adds a coin at a random spot, without touching any coins already out. Same ability odds as any coin (Totem 5%).</summary>
        public PowerUpAbility SpawnGiftCoin()
        {
            Vector3 pos = SpawnPositions[Random.Range(0, SpawnPositions.Length)];
            PowerUpAbility ability = RollAbility();
            SpawnOneCoin(pos, ability);
            return ability;
        }

        private void SpawnOneCoin(Vector3 pos, PowerUpAbility ability)
        {
            Vector3 coinPos = pos + Vector3.up * HOVER_HEIGHT;

            // Plain generated coin — a flattened Cylinder primitive, gold
            // tint, no per-ability visual variation (per explicit design:
            // proximity text is what tells you which one it is).
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "KE_PowerUpCoin";
            go.transform.position = coinPos;
            go.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // stand it on edge so it reads as a coin, not a flat puck

            Collider primCol = go.GetComponent<Collider>();
            if (primCol != null) Destroy(primCol);

            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = new Color(1f, 0.85f, 0.2f);
                rend.material = mat;
            }

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.9f, 0.9f, 0.9f); // was 1.2f — reached too far
            col.isTrigger = true;

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing.
            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
                go.layer = existingInteractables[0].gameObject.layer;

            var interactable = go.AddComponent<Interactable>();
            interactable.actionText = "Grab Power-Up";
            interactable.holdTime = 0.5f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            _activeCoins.Add(new PowerUpCoin { GO = go, Ability = ability });
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PowerUp] Spawned '" + ability + "' coin at " + pos);
        }

        public bool IsPowerUpCoin(Interactable interactable)
        {
            if (interactable == null) return false;
            return _activeCoins.Exists(c => c.GO == interactable.gameObject);
        }

        /// <summary>Shop "Starting Totem" power up.</summary>
        public void GrantStartingTotem() { ApplyAbility(PowerUpAbility.TotemOfUndying); }

        /// <summary>Shop "Head Start Battery" power up.</summary>
        public void GrantStrongBattery() { ApplyAbility(PowerUpAbility.StrongBattery); }

        /// <summary>Called from the Harmony OnInteractComplete patch.</summary>
        public void OnCoinGrabbed(Interactable interactable)
        {
            var coin = _activeCoins.Find(c => c.GO == interactable.gameObject);
            if (coin == null) return;

            _activeCoins.Remove(coin);
            Daily.Add("coins", 1);
            if (coin.GO != null) Destroy(coin.GO);

            ApplyAbility(coin.Ability);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PowerUp] Grabbed '" + coin.Ability + "'.");
        }

        /// <summary>3.7: a trait hands you a Totem of Undying (up to the usual limit).</summary>
        public void GrantTotem() { _totemCount = Mathf.Min(MaxTotems, _totemCount + 1); }

        private void ApplyAbility(PowerUpAbility ability)
        {
            EndlessTraits.OnCoinGrabbed();
            switch (ability)
            {
                case PowerUpAbility.MaxStats:
                    // Real vanilla station methods (confirmed via
                    // GamePatches.cs's care-station patch — Charge/Feed/
                    // Groom fully replenish, same as walking up to a real
                    // station), no reflection needed.
                    var needs = RM.tattletail?.GetNeeds();
                    if (needs != null) { needs.Charge(); needs.Feed(); needs.Groom(); }
                    break;
                case PowerUpAbility.StrongBattery:
                    _strongBatteryTimeRemaining = STRONG_BATTERY_DURATION;
                    if (RM.flashlight != null) RM.flashlight.MaxLight();
                    break;
                case PowerUpAbility.TotemOfUndying:
                    _totemCount = Mathf.Min(MaxTotems, _totemCount + 1);
                    break;
            }
        }

        private static string AbilityName(PowerUpAbility ability)
        {
            switch (ability)
            {
                case PowerUpAbility.MaxStats: return "Max Stats";
                case PowerUpAbility.StrongBattery: return "Strong Battery";
                case PowerUpAbility.TotemOfUndying: return "Totem of Undying";
                default: return ability.ToString();
            }
        }

        /// <summary>Called from EndlessRunManager.OnMamaCaughtPlayer() before falling back to a real death. Consumes one totem, plays the save beat, sends Mama away.</summary>
        public bool TryConsumeTotem(float mamaDifficultyMult)
        {
            if (_totemCount <= 0) return false;
            _totemCount--;
            PlayTotemSaveEffect();
            MamaController.Instance?.RelocateAway(mamaDifficultyMult);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PowerUp] Totem consumed — " + _totemCount + " remaining.");
            return true;
        }

        private void PlayTotemSaveEffect()
        {
            EnsureTotemClipResolved();
            if (_totemClip != null && _sfxSource != null)
                _sfxSource.PlayOneShot(_totemClip);
            else
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [PowerUp] Totem save clip unavailable — no sound played.");

            SpawnTotemPopup();
        }

        // AudioItem itself has no public clip field (confirmed via
        // reflection dump against the real Assembly-CSharp.dll) — the
        // actual AudioClip lives one level down, on each of its
        // AudioSubItem.Clip entries. Resolved once, cached, then played
        // through our own AudioSource — same real-object-not-the-ID-system
        // approach as SpeakerObjective.
        private static void EnsureTotemClipResolved()
        {
            if (_totemClipResolved) return;
            _totemClipResolved = true;

            AudioItem item = AudioController.GetAudioItem("music_sting_positive");
            if (item == null || item.subItems == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [PowerUp] AudioItem 'music_sting_positive' not found — totem save will be silent.");
                return;
            }

            foreach (var sub in item.subItems)
            {
                if (sub != null && sub.Clip != null) { _totemClip = sub.Clip; break; }
            }

            if (_totemClip != null)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PowerUp] Totem save clip resolved: '" + _totemClip.name + "'.");
            else
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [PowerUp] AudioItem 'music_sting_positive' has no clip in its subItems.");
        }

        // Procedural totem shape (built from primitives, no external asset
        // needed) that pops up in front of the camera, glowing green, then
        // shrinks away for the save moment — placed once in world space at
        // trigger time rather than parented to the (about-to-freeze)
        // camera, simplest safe option.
        private void SpawnTotemPopup()
        {
            if (RM.fpsController == null) return;
            Transform cam = RM.fpsController.transform;

            var root = new GameObject("KE_TotemPopup");
            root.transform.position = cam.position + cam.forward * 1.2f + Vector3.down * 0.3f;
            root.transform.rotation = Quaternion.LookRotation(root.transform.position - cam.position);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            pole.transform.localPosition = Vector3.zero;
            TintUnlit(pole, new Color(0.1f, 0.6f, 0.25f));

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = new Vector3(0.28f, 0.16f, 0.2f);
            head.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            TintUnlit(head, new Color(0.75f, 0.85f, 0.2f));

            foreach (Collider c in root.GetComponentsInChildren<Collider>())
                Destroy(c);

            StartCoroutine(AnimateTotemPopup(root));
        }

        private static void TintUnlit(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            r.material = mat;
        }

        private System.Collections.IEnumerator AnimateTotemPopup(GameObject root)
        {
            const float growTime = 0.25f;
            const float holdTime = 1f;
            const float shrinkTime = 0.4f;

            Vector3 fullScale = Vector3.one;
            root.transform.localScale = Vector3.zero;

            float t = 0f;
            while (t < growTime)
            {
                t += Time.deltaTime;
                root.transform.localScale = Vector3.Lerp(Vector3.zero, fullScale, t / growTime);
                yield return null;
            }
            root.transform.localScale = fullScale;

            yield return new WaitForSeconds(holdTime);

            t = 0f;
            while (t < shrinkTime)
            {
                t += Time.deltaTime;
                root.transform.localScale = Vector3.Lerp(fullScale, Vector3.zero, t / shrinkTime);
                yield return null;
            }

            Destroy(root);
        }

        private void DespawnAllCoins()
        {
            foreach (var coin in _activeCoins)
                if (coin.GO != null) Destroy(coin.GO);
            _activeCoins.Clear();
            EndlessHud.Instance?.HideAbilityHint();
        }

        /// <summary>Called from EndlessRunManager.StartRun()/EndRun() — resets all per-run state.</summary>
        public void CleanUp()
        {
            DespawnAllCoins();
            _strongBatteryTimeRemaining = 0f;
            _totemCount = 0;
        }
    }
}
