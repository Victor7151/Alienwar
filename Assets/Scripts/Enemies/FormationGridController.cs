using System.Collections.Generic;
using UnityEngine;
using Starfall.Core;
using Starfall.Combat;

namespace Starfall.Enemies
{
    /// <summary>Kept for backwards compatibility with older setup scripts.</summary>
    public enum GridShape
    {
        Grid,
        VFormation,
        Pincer,
        Wall,
        Diamond,
        Crescent,
        FlankingColumns,
        Echelon
    }

    /// <summary>How members fly into their slots. Both styles are collision-free by construction.</summary>
    public enum FormationEntryStyle
    {
        /// <summary>Columns descend from above the screen; lower slots are reached first.</summary>
        Drop,
        /// <summary>Rows slide in from the screen edges; the slots furthest from the edge fill first.</summary>
        Sweep
    }

    /// <summary>
    /// Upper-screen formation director.
    ///
    /// Layout: every shape is carved out of a spaced lattice (<see cref="SpacingX"/> x <see cref="SpacingY"/>)
    /// that is clipped to the real screen, so members never overlap, always stay reachable on
    /// portrait phones and wide monitors, and the member count is capped to what physically fits.
    ///
    /// Behaviour: owns the formation's sway, the fire cadence and bullet patterns (single, fan,
    /// curtain-with-gaps), dive selection, and role specials (gunner bursts, mines, blinks, charges).
    /// </summary>
    public class FormationGridController : MonoBehaviour
    {
        public static FormationGridController Instance { get; private set; }

        /// <summary>Horizontal centre-to-centre distance between neighbours (enemies are ~0.9–1.05 wide). Leaves a clean ~0.3 unit gap so they never touch.</summary>
        public const float SpacingX = 1.35f;
        /// <summary>Vertical centre-to-centre distance between rows. Leaves a clean ~0.35 unit gap.</summary>
        public const float SpacingY = 1.20f;
        /// <summary>Minimum distance kept between any two free-flying enemies during swoops/dives so they never touch.</summary>
        public const float MinSeparation = 1.30f;
        /// <summary>Two members closer than this horizontally share a firing lane.</summary>
        private const float LaneWidth = 1.1f;

        [Header("Formation Oscillation")]
        [SerializeField] private float swaySpeed = 1.3f;
        [SerializeField] private float maxSwayAmplitude = 0.45f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private float bobAmplitude = 0.14f;

        [Header("Fire Cadence")]
        [SerializeField] private float shootIntervalMin = 0.8f;
        [SerializeField] private float shootIntervalMax = 1.5f;
        [SerializeField] private float staggerDelay = 0.22f;
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private float doubleShotChance = 0.25f;
        [SerializeField] private float projectileSpeedMultiplier = 1f;
        [SerializeField] private float aimedShotChance = 0.2f;

        [Header("Bullet Patterns")]
        [SerializeField] private float curtainChance = 0f;
        [SerializeField] private float fanChance = 0f;
        [SerializeField] private int maxEnemyBullets = 18;

        [Header("Dives & Specials")]
        [SerializeField] private float diveIntervalMin = 3.5f;
        [SerializeField] private float diveIntervalMax = 5.5f;
        [SerializeField] private float specialIntervalMin = 2.4f;
        [SerializeField] private float specialIntervalMax = 3.6f;

        private Vector3[] slots = new Vector3[0];
        private EnemyBase[] members = new EnemyBase[0];
        private int slotCount;
        private int aliveCount;
        private bool isSetup;
        private float swayAmplitude;
        private Vector3 anchorOffset;
        private float formationTime;

        private float nextShootTime;
        private float nextDiveTime;
        private float nextSpecialTime;
        private bool echelonFlip;
        private float pendingSalvoTime = -1f;
        private readonly List<EnemyBase> pendingSalvo = new List<EnemyBase>(8);
        private readonly List<EnemyBase> scratch = new List<EnemyBase>(64);
        private readonly List<int> scratchIdx = new List<int>(64);

        [Header("Last Stand")]
        [Tooltip("Fire / special interval multiplier once the last-stand threshold is reached.")]
        [SerializeField] private float lastStandFireScale = 0.6f;
        [Tooltip("Dive interval multiplier during the last stand.")]
        [SerializeField] private float lastStandDiveScale = 0.5f;
        [Tooltip("Sway speed multiplier during the last stand.")]
        [SerializeField] private float lastStandSwayScale = 1.6f;
        private bool lastStand;
        private float swayPhase;
        private float demoralizedTimer;

        public bool IsLastStand => lastStand;
        public bool IsDemoralized => demoralizedTimer > 0f;

        public int Rows => 1;
        public int Columns => slotCount;
        public int SlotCount => slotCount;
        public int AliveCount => aliveCount;
        public bool IsSetup => isSetup;
        public bool IsFormationCleared => isSetup && aliveCount <= 0;
        public FormationEntryStyle EntryStyle { get; private set; }
        /// <summary>Height kept free at the top of the band (e.g. for a mini-boss strafing above the escorts).</summary>
        public float ReservedTop { get; set; }

        public event System.Action OnFormationCleared;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Wave tuning
        // ------------------------------------------------------------------

        public void ApplyWaveParameters(float newSwaySpeed, float minInterval, float maxInterval, float newDoubleChance, float stagger = 0.22f, float projSpeedMult = 1f, float diveMin = 3.5f, float diveMax = 5.5f, float aimedChance = 0.2f)
        {
            swaySpeed = newSwaySpeed;
            shootIntervalMin = Mathf.Max(0.25f, minInterval);
            shootIntervalMax = Mathf.Max(shootIntervalMin, maxInterval);
            doubleShotChance = Mathf.Clamp01(newDoubleChance);
            staggerDelay = stagger;
            projectileSpeedMultiplier = Mathf.Max(0.5f, projSpeedMult);
            diveIntervalMin = diveMin;
            diveIntervalMax = Mathf.Max(diveMin, diveMax);
            aimedShotChance = Mathf.Clamp01(aimedChance);
            nextDiveTime = Time.time + Random.Range(Mathf.Max(1.5f, diveIntervalMin), Mathf.Max(2.5f, diveIntervalMax));
        }

        public void ApplyPatternParameters(float curtain, float fan, int bulletCap, float specialMin, float specialMax)
        {
            curtainChance = Mathf.Clamp01(curtain);
            fanChance = Mathf.Clamp01(fan);
            maxEnemyBullets = Mathf.Max(6, bulletCap);
            specialIntervalMin = Mathf.Max(0.6f, specialMin);
            specialIntervalMax = Mathf.Max(specialIntervalMin, specialMax);
        }

        // ------------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------------

        private struct Candidate
        {
            public Vector3 pos;
            public float priority;
        }

        /// <summary>
        /// Builds the slot layout for a shape. Returns how many members actually fit
        /// (never more than requested, never overlapping, always on screen).
        /// </summary>
        public int BuildFormation(FormationType shape, int requestedCount)
        {
            ClearGrid();

            float halfW = PlayfieldBounds.PlayHalfWidth;
            float usableHalf = Mathf.Max(SpacingX, halfW - 0.55f);
            int cMax = Mathf.Max(1, Mathf.FloorToInt(usableHalf / SpacingX));

            float yTop = PlayfieldBounds.FormationTop - bobAmplitude - 0.1f - Mathf.Max(0f, ReservedTop);
            float yBottom = PlayfieldBounds.FormationBottom + bobAmplitude;
            int rowCount = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, yTop - yBottom) / SpacingY) + 1, 1, 5);

            var candidates = new List<Candidate>(128);
            GenerateCandidates(shape, cMax, rowCount, yTop, candidates);
            candidates.Sort((a, b) => a.priority.CompareTo(b.priority));

            // In wide landscape mode, scale requestedCount so enemies span generously from left to right
            int scaledCount = requestedCount;
            if (halfW > 5.2f)
            {
                float widthRatio = Mathf.Clamp(halfW / 5.2f, 1f, 1.65f);
                scaledCount = Mathf.RoundToInt(requestedCount * widthRatio);
            }

            int count = Mathf.Clamp(scaledCount, 0, candidates.Count);
            slotCount = count;
            slots = new Vector3[count];
            members = new EnemyBase[count];
            float maxAllowedX = Mathf.Min(halfW - 0.6f, PlayfieldBounds.CameraRight - 0.7f);
            float maxAbsX = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = candidates[i].pos;
                p.x = Mathf.Clamp(p.x, -maxAllowedX, maxAllowedX);
                p.y = Mathf.Clamp(p.y, yBottom + 0.1f, yTop);
                slots[i] = p;
                maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(slots[i].x));
            }

            // Sway only as far as the screen allows.
            swayAmplitude = Mathf.Clamp(maxAllowedX - maxAbsX, 0f, maxSwayAmplitude);

            EntryStyle = (shape == FormationType.Pincer || shape == FormationType.FlankingColumns || shape == FormationType.Echelon)
                ? FormationEntryStyle.Sweep
                : FormationEntryStyle.Drop;

            aliveCount = 0;
            isSetup = false;
            lastStand = false;
            formationTime = 0f;
            nextShootTime = Time.time + 1.4f;
            nextSpecialTime = Time.time + Random.Range(specialIntervalMin, specialIntervalMax) + 1.2f;
            return count;
        }

        private void GenerateCandidates(FormationType shape, int cMax, int rows, float yTop, List<Candidate> list)
        {
            // r = 0 is the top row; y decreases with r.
            float Y(int r) => yTop - r * SpacingY;
            float X(float c) => c * SpacingX;
            int bottomRow = rows - 1;

            switch (shape)
            {
                case FormationType.Wall:
                {
                    // Two staggered ranks sitting low and close to the player, filling entire width.
                    int r0 = bottomRow, r1 = Mathf.Max(0, bottomRow - 1);
                    for (int c = -cMax; c <= cMax; c++)
                        Add(list, X(c), Y(r0), Mathf.Abs(c));
                    if (r1 != r0)
                        for (int c = -cMax; c < cMax; c++)
                            Add(list, X(c + 0.5f), Y(r1), 100f + Mathf.Abs(c + 0.5f));
                    break;
                }
                case FormationType.VFormation:
                {
                    // Wide nested chevrons pointing down at the player, scaled across the full screen width.
                    float slope = (float)bottomRow / Mathf.Max(1, cMax);
                    for (int layer = 0; layer < rows; layer++)
                    {
                        for (int k = 0; k <= cMax; k++)
                        {
                            int r = Mathf.Clamp(bottomRow - Mathf.RoundToInt(k * slope) - layer, 0, bottomRow);
                            float p = layer * 100f + k;
                            Add(list, X(k), Y(r), p);
                            if (k > 0) Add(list, X(-k), Y(r), p + 0.01f);
                        }
                    }
                    break;
                }
                case FormationType.Diamond:
                {
                    float cy = (rows - 1) * 0.5f;
                    for (int r = 0; r < rows; r++)
                        for (int c = -cMax; c <= cMax; c++)
                        {
                            float d = (Mathf.Abs(c) / (float)cMax) * 10f + Mathf.Abs(r - cy) * 3f;
                            Add(list, X(c), Y(r), d + (c < 0 ? 0.01f : 0f));
                        }
                    break;
                }
                case FormationType.Crescent:
                {
                    // Arch: wings low on flanks from screen edge to screen edge, centre high.
                    for (int layer = 0; layer < rows; layer++)
                        for (int c = -cMax; c <= cMax; c++)
                        {
                            float t = Mathf.Abs(c) / (float)cMax;
                            int r = Mathf.Clamp(Mathf.RoundToInt(t * t * bottomRow) - layer, 0, bottomRow);
                            Add(list, X(c), Y(r), layer * 50f + (cMax - Mathf.Abs(c)) + (c < 0 ? 0.01f : 0f));
                        }
                    break;
                }
                case FormationType.Pincer:
                {
                    // Claws: start from the wide flanks (-cMax and +cMax) sweeping inwards.
                    for (int r = 0; r < rows; r++)
                    {
                        for (int k = 0; k <= cMax; k++)
                        {
                            int c = cMax - k;
                            float p = r * 50f + k;
                            Add(list, X(c), Y(r), p);
                            if (c > 0) Add(list, X(-c), Y(r), p + 0.01f);
                        }
                    }
                    break;
                }
                case FormationType.FlankingColumns:
                {
                    for (int k = 0; k < Mathf.Min(4, cMax); k++)
                        for (int r = 0; r < rows; r++)
                        {
                            int c = cMax - k;
                            float p = k * 100f + r;
                            Add(list, X(c), Y(r), p);
                            if (c > 0) Add(list, X(-c), Y(r), p + 0.01f);
                        }
                    break;
                }
                case FormationType.Echelon:
                {
                    // Diagonal ranks across the entire breadth.
                    echelonFlip = !echelonFlip;
                    float span = Mathf.Max(1, cMax * 2);
                    for (int layer = 0; layer < rows; layer++)
                        for (int c = -cMax; c <= cMax; c++)
                        {
                            float t = (c + cMax) / span;
                            if (echelonFlip) t = 1f - t;
                            int r = Mathf.Clamp(Mathf.RoundToInt(t * Mathf.Max(0, bottomRow - 1)) + (layer % 2) - (layer / 2) * 2, 0, bottomRow);
                            Add(list, X(c), Y(r), layer * 50f + Mathf.Abs(c));
                        }
                    break;
                }
                case FormationType.Swarm:
                case FormationType.Random:
                {
                    for (int r = 0; r < rows; r++)
                    {
                        float offset = (r % 2 == 0) ? 0f : 0.5f;
                        for (int c = -cMax; c <= cMax; c++)
                        {
                            float cx = c + offset;
                            if (Mathf.Abs(cx) > cMax) continue;
                            Add(list, X(cx), Y(r), r * 50f + Mathf.Abs(cx) + Random.Range(0f, 1.5f));
                        }
                    }
                    break;
                }
                case FormationType.Checker:
                {
                    for (int r = 0; r < rows; r++)
                        for (int c = -cMax; c <= cMax; c++)
                        {
                            if (((c + r) & 1) != 0) continue;
                            Add(list, X(c), Y(r), r * 50f + Mathf.Abs(c));
                        }
                    break;
                }
                default: // Grid: fill row-by-row across full width
                {
                    for (int r = 0; r < rows; r++)
                        for (int c = -cMax; c <= cMax; c++)
                            Add(list, X(c), Y(r), r * 100f + Mathf.Abs(c) + (c < 0 ? 0.01f : 0f));
                    break;
                }
            }
        }

        private static void Add(List<Candidate> list, float x, float y, float priority)
        {
            list.Add(new Candidate { pos = new Vector3(x, y, 0f), priority = priority });
        }

        /// <summary>Order in which slots should be filled so entry paths never cross an occupied slot.</summary>
        public List<int> GetSpawnOrder()
        {
            var order = new List<int>(slotCount);
            for (int i = 0; i < slotCount; i++) order.Add(i);
            if (EntryStyle == FormationEntryStyle.Drop)
                order.Sort((a, b) => slots[a].y.CompareTo(slots[b].y)); // lowest first
            else
                order.Sort((a, b) => Mathf.Abs(slots[a].x).CompareTo(Mathf.Abs(slots[b].x))); // innermost first
            return order;
        }

        /// <summary>
        /// Off-screen start position for a slot. Start offsets preserve the slot ordering along each
        /// lane, and all members move at the same speed, so no two ever come closer than their slots.
        public Vector3 GetEntryStart(int slot)
        {
            Vector3 s = slots[slot];
            if (EntryStyle == FormationEntryStyle.Drop)
            {
                float bottom = PlayfieldBounds.FormationBottom;
                float cascade = 0.1f * Mathf.Abs(s.x);
                return new Vector3(s.x, Mathf.Min(PlayfieldBounds.Top + 0.8f + cascade, PlayfieldBounds.Top + 1.4f), 0f);
            }
            else
            {
                // Sweep in from right at screen edge so enemies are quickly visible
                float edge = PlayfieldBounds.CameraRight + 0.6f;
                float sign = s.x >= 0f ? 1f : -1f;
                return new Vector3(sign * edge, s.y, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Membership
        // ------------------------------------------------------------------

        public void RegisterMember(int row, int col, EnemyBase enemy)
        {
            if (col < 0 || col >= slotCount || enemy == null) return;
            if (members[col] == null) aliveCount++;
            members[col] = enemy;
        }

        public bool RegisterInFirstFreeSlot(EnemyBase enemy, out int assignedRow, out int assignedCol)
        {
            assignedRow = 0;
            assignedCol = 0;
            for (int i = 0; i < slotCount; i++)
            {
                if (members[i] == null)
                {
                    members[i] = enemy;
                    aliveCount++;
                    assignedCol = i;
                    return true;
                }
            }
            return false;
        }

        public void FinalizeGridSetup()
        {
            isSetup = true;
            if (aliveCount <= 0) OnFormationCleared?.Invoke();
        }

        public void NotifyMemberDefeated(int row, int col)
        {
            if (col < 0 || col >= slotCount) return;
            if (members[col] == null) return;

            members[col] = null;
            aliveCount = Mathf.Max(0, aliveCount - 1);
            if (isSetup && aliveCount <= 0) OnFormationCleared?.Invoke();
        }

        /// <summary>Moves a member to a random empty slot (Blinker teleport). Returns the new slot or -1.</summary>
        public int RelocateMember(EnemyBase enemy, int fromSlot)
        {
            if (fromSlot < 0 || fromSlot >= slotCount || members[fromSlot] != enemy) return -1;
            scratchIdx.Clear();
            for (int i = 0; i < slotCount; i++)
                if (members[i] == null && Vector3.Distance(slots[i], slots[fromSlot]) > SpacingX * 1.5f)
                    scratchIdx.Add(i);
            if (scratchIdx.Count == 0) return -1;
            int to = scratchIdx[Random.Range(0, scratchIdx.Count)];
            members[fromSlot] = null;
            members[to] = enemy;
            return to;
        }

        public Vector3 GetSlotWorldPosition(int row, int col)
        {
            if (col < 0 || col >= slotCount) return anchorOffset;
            Vector3 pos = slots[col] + anchorOffset;
            float maxX = Mathf.Min(PlayfieldBounds.PlayHalfWidth - 0.4f, PlayfieldBounds.CameraRight - 0.5f);
            pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
            pos.y = Mathf.Clamp(pos.y, PlayfieldBounds.FormationBottom, PlayfieldBounds.FormationTop);
            return pos;
        }

        public void ClearGrid()
        {
            slots = new Vector3[0];
            members = new EnemyBase[0];
            slotCount = 0;
            aliveCount = 0;
            isSetup = false;
            lastStand = false;
            demoralizedTimer = 0f;
            pendingSalvo.Clear();
            pendingSalvoTime = -1f;
        }

        // ------------------------------------------------------------------
        // Last stand & overlapping reinforcements
        // ------------------------------------------------------------------

        /// <summary>
        /// The final members of a wave get angry: faster fire, dives and sway, plus a red tint.
        /// Lasts until the next formation is built.
        /// </summary>
        public void EnterLastStand()
        {
            if (lastStand || !isSetup || aliveCount <= 0) return;
            lastStand = true;

            for (int i = 0; i < slotCount; i++)
                if (members[i] != null && !members[i].IsDead) members[i].EnterLastStand();

            // Pull the next attacks forward so the change is felt immediately.
            nextShootTime = Mathf.Min(nextShootTime, Time.time + 0.35f);
            nextDiveTime = Mathf.Min(nextDiveTime, Time.time + 0.8f);
        }

        /// <summary>
        /// Sends every remaining member on a final raid run and frees all slots, so the next
        /// formation can fly in while the stragglers are still alive. Returns how many broke ranks.
        /// </summary>
        public int ReleaseRemainingMembers()
        {
            int released = 0;
            for (int i = 0; i < slotCount; i++)
            {
                var e = members[i];
                members[i] = null;
                if (e == null || e.IsDead) continue;
                e.BreakFromFormation();
                released++;
            }
            aliveCount = 0;
            pendingSalvo.Clear();
            pendingSalvoTime = -1f;
            return released;
        }

        /// <summary>
        /// Stuns and panics all squad members (e.g. Commander death). Clears pending salvos and delays all actions.
        /// </summary>
        public void DemoralizeSquad(float duration)
        {
            demoralizedTimer = duration;
            pendingSalvo.Clear();
            pendingSalvoTime = -1f;
            nextShootTime = Mathf.Max(nextShootTime, Time.time + duration);
            nextDiveTime = Mathf.Max(nextDiveTime, Time.time + duration);
            nextSpecialTime = Mathf.Max(nextSpecialTime, Time.time + duration);

            for (int i = 0; i < slotCount; i++)
            {
                if (members[i] != null && !members[i].IsDead)
                    members[i].Panick(duration);
            }
        }

        // Legacy setup entry points (older editor tools / scripts). They now route through BuildFormation.
        public void SetupGrid(int newRows, int newCols, float spacingX = 1.18f, float spacingY = 0.88f) => BuildFormation(FormationType.Grid, newRows * newCols);
        public void SetupVFormation(int count, float spacingX = 1.18f, float spacingY = 0.5f) => BuildFormation(FormationType.VFormation, count);
        public void SetupPincer(int count, float spacingX = 1.4f, float spacingY = 1.05f) => BuildFormation(FormationType.Pincer, count);
        public void SetupWall(int count, float spacingX = 1.4f, float spacingY = 1.1f) => BuildFormation(FormationType.Wall, count);
        public void SetupDiamond(int count, float spacingX = 1.35f, float spacingY = 0.95f) => BuildFormation(FormationType.Diamond, count);
        public void SetupCrescent(int count, float radius = 3.5f, float arcDegrees = 115f) => BuildFormation(FormationType.Crescent, count);
        public void SetupFlankingColumns(int count, float spacingX = 1.35f, float spacingY = 1.05f) => BuildFormation(FormationType.FlankingColumns, count);
        public void SetupEchelon(int count, float spacingX = 1.25f, float spacingY = 0.5f, bool leftToRight = true) => BuildFormation(FormationType.Echelon, count);

        // ------------------------------------------------------------------
        // Frame update
        // ------------------------------------------------------------------

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.FormationUpdate.Auto();
            float dt = TimeWarpManager.EnemyDeltaTime;
            formationTime += dt;
            // Phase is integrated (not time * speed) so a last-stand speed change never makes the formation jump.
            swayPhase += dt * swaySpeed * (lastStand ? lastStandSwayScale : 1f);
            float sway = Mathf.Sin(swayPhase) * swayAmplitude;
            float bob = Mathf.Cos(formationTime * bobSpeed) * bobAmplitude;
            anchorOffset = new Vector3(sway, bob, 0f);

            if (!isSetup || aliveCount <= 0) return;

            if (demoralizedTimer > 0f)
            {
                demoralizedTimer -= dt;
                return;
            }

            // Time-warp stretches all cadences.
            float warp = TimeWarpManager.EnemyTimeScale;
            if (warp < 1f)
            {
                float lost = Time.deltaTime * (1f - warp);
                nextShootTime += lost;
                nextDiveTime += lost;
                nextSpecialTime += lost;
                if (pendingSalvoTime > 0f) pendingSalvoTime += lost;
            }

            if (pendingSalvoTime > 0f && Time.time >= pendingSalvoTime)
                ReleaseCurtain();

            float fireScale = lastStand ? lastStandFireScale : 1f;
            float diveScale = lastStand ? lastStandDiveScale : 1f;

            if (Time.time >= nextShootTime)
            {
                TriggerSalvo();
                nextShootTime = Time.time + Random.Range(shootIntervalMin, shootIntervalMax) * fireScale;
            }

            if (diveIntervalMin > 0f && Time.time >= nextDiveTime)
            {
                TriggerDive();
                nextDiveTime = Time.time + Random.Range(diveIntervalMin, diveIntervalMax) * diveScale;
            }

            if (Time.time >= nextSpecialTime)
            {
                TriggerRoleSpecial();
                nextSpecialTime = Time.time + Random.Range(specialIntervalMin, specialIntervalMax) * fireScale;
            }
        }

        // ------------------------------------------------------------------
        // Shooter selection
        // ------------------------------------------------------------------

        private bool IsReady(EnemyBase e) =>
            e != null && !e.IsDead && e.gameObject.activeInHierarchy && e.IsStationed && !e.IsBusy && e.IsVisibleToPlayer();

        /// <summary>True if another member (in any state) sits below this one in its firing lane.</summary>
        private bool HasMemberBelow(int slot)
        {
            Vector3 s = slots[slot];
            for (int i = 0; i < slotCount; i++)
            {
                if (i == slot || members[i] == null || members[i].IsDead) continue;
                Vector3 o = slots[i];
                if (o.y < s.y - 0.2f && Mathf.Abs(o.x - s.x) < LaneWidth) return true;
            }
            return false;
        }

        private void CollectFrontLine(List<EnemyBase> result)
        {
            result.Clear();
            for (int i = 0; i < slotCount; i++)
            {
                var e = members[i];
                if (IsReady(e) && !HasMemberBelow(i)) result.Add(e);
            }
        }

        private void CollectReady(List<EnemyBase> result, EnemyRole role)
        {
            result.Clear();
            for (int i = 0; i < slotCount; i++)
            {
                var e = members[i];
                if (IsReady(e) && e.Role == role) result.Add(e);
            }
        }

        private static Vector3 PlayerPos()
        {
            var p = Starfall.Player.PlayerController.Instance;
            return p != null ? p.transform.position : new Vector3(0f, -4f, 0f);
        }

        // ------------------------------------------------------------------
        // Bullet patterns
        // ------------------------------------------------------------------

        private void TriggerSalvo()
        {
            if (Projectile.ActiveEnemyCount >= maxEnemyBullets) return;

            CollectFrontLine(scratch);
            if (scratch.Count == 0) return;

            float roll = Random.value;
            if (roll < curtainChance && scratch.Count >= 4 && pendingSalvoTime < 0f)
            {
                PrepareCurtain(scratch);
                return;
            }

            // Elites always fan; otherwise a fan is a chance roll.
            EnemyBase elite = null;
            foreach (var e in scratch) if (e.Role == EnemyRole.Elite) { elite = e; break; }
            if (elite != null || Random.value < fanChance)
            {
                var shooter = elite != null ? elite : scratch[Random.Range(0, scratch.Count)];
                shooter.FireFan(PlayerPos(), elite != null ? 5 : 3, projectileDamage, projectileSpeedMultiplier * 0.9f);
                return;
            }

            int shots = (scratch.Count >= 2 && Random.value < doubleShotChance) ? 2 : 1;
            StartCoroutine(StaggeredShots(shots));
        }

        private System.Collections.IEnumerator StaggeredShots(int shots)
        {
            EnemyBase last = null;
            for (int i = 0; i < shots; i++)
            {
                CollectFrontLine(scratch);
                if (scratch.Count == 0) yield break;
                var shooter = PickShooter(scratch, last);
                last = shooter;

                Vector3 target = PlayerPos();
                bool outerWing = Mathf.Abs(shooter.transform.position.x) >= PlayfieldBounds.PlayHalfWidth * 0.45f;
                if ((outerWing && Random.value < 0.5f) || Random.value < aimedShotChance)
                    shooter.FireAimed(target, projectileDamage, projectileSpeedMultiplier);
                else
                    shooter.FireDownwards(projectileDamage, projectileSpeedMultiplier);

                if (i < shots - 1) yield return new WaitForSeconds(staggerDelay);
            }
        }

        private EnemyBase PickShooter(List<EnemyBase> front, EnemyBase avoid)
        {
            // Half the time pick the shooter nearest the player so standing still is punished.
            if (Random.value < 0.5f)
            {
                float px = PlayerPos().x;
                EnemyBase best = front[0];
                float bestD = float.MaxValue;
                foreach (var e in front)
                {
                    if (e == avoid && front.Count > 1) continue;
                    float d = Mathf.Abs(e.transform.position.x - px);
                    if (d < bestD) { bestD = d; best = e; }
                }
                return best;
            }
            EnemyBase pick = front[Random.Range(0, front.Count)];
            if (pick == avoid && front.Count > 1) pick = front[(front.IndexOf(pick) + 1) % front.Count];
            return pick;
        }

        /// <summary>
        /// Curtain: most of the front line fires straight down at once, leaving one or two
        /// telegraphed gaps. Readable, fair and forces the player to reposition.
        /// </summary>
        private void PrepareCurtain(List<EnemyBase> front)
        {
            pendingSalvo.Clear();
            float px = PlayerPos().x;
            // Gap is placed a little away from the player so they have to move to it.
            float gapA = Mathf.Clamp(px + Random.Range(-2.4f, 2.4f), -PlayfieldBounds.PlayHalfWidth + 0.6f, PlayfieldBounds.PlayHalfWidth - 0.6f);
            float gapB = front.Count >= 8 ? -gapA * 0.6f : float.NaN;

            foreach (var e in front)
            {
                float x = e.transform.position.x;
                if (Mathf.Abs(x - gapA) < SpacingX * 0.9f) continue;
                if (!float.IsNaN(gapB) && Mathf.Abs(x - gapB) < SpacingX * 0.9f) continue;
                pendingSalvo.Add(e);
                e.FlashWarning(new Color(1f, 0.85f, 0.3f), 0.45f);
            }

            if (pendingSalvo.Count == 0) return;
            pendingSalvoTime = Time.time + 0.45f;
            Starfall.Audio.AudioManager.Instance?.PlayReinforcementAlert(0.4f);
        }

        private void ReleaseCurtain()
        {
            pendingSalvoTime = -1f;
            foreach (var e in pendingSalvo)
                if (e != null && !e.IsDead && e.gameObject.activeInHierarchy && e.IsStationed)
                    e.FireDownwards(projectileDamage, projectileSpeedMultiplier * 0.85f, true);
            pendingSalvo.Clear();
        }

        // ------------------------------------------------------------------
        // Dives & role specials
        // ------------------------------------------------------------------

        private int CurrentLevel()
        {
            return Starfall.Levels.LevelManager.Instance != null ? Starfall.Levels.LevelManager.Instance.CurrentLevelNumber : 1;
        }

        private void TriggerDive()
        {
            CollectFrontLine(scratch);
            if (scratch.Count == 0) return;
            Vector3 playerPos = PlayerPos();

            // Chargers get first pick: their dive is a telegraphed ram.
            foreach (var e in scratch)
            {
                if (e.Role == EnemyRole.Charger && Random.value < 0.75f)
                {
                    e.StartChargeRun(playerPos);
                    return;
                }
            }

            int level = CurrentLevel();
            float pairChance = level >= 4 ? 0.55f : level == 3 ? 0.45f : level == 2 ? 0.35f : 0.18f;
            if (aliveCount >= 6 && scratch.Count >= 2 && Random.value < pairChance)
            {
                EnemyBase left = null, right = null;
                foreach (var e in scratch)
                {
                    float x = e.transform.position.x;
                    if (x < -0.6f && (left == null || x < left.transform.position.x)) left = e;
                    if (x > 0.6f && (right == null || x > right.transform.position.x)) right = e;
                }
                if (left != null && right != null)
                {
                    left.StartDiveRun(playerPos, DivePatternType.FlankCrossfireDive);
                    right.StartDiveRun(playerPos, DivePatternType.FlankCrossfireDive);
                    return;
                }
            }

            var diver = scratch[Random.Range(0, scratch.Count)];
            float roll = Random.value;
            DivePatternType pattern;
            
            // Scouts have a 35% chance to do a Kamikaze dive instead!
            if (diver is ScoutEnemy && roll < 0.35f)
            {
                pattern = DivePatternType.KamikazeHoming;
            }
            else
            {
                if (level >= 3)
                    pattern = roll < 0.4f ? DivePatternType.HighSpeedStrafe : roll < 0.75f ? DivePatternType.LoopingDive : DivePatternType.FlankCrossfireDive;
                else if (level == 2)
                    pattern = roll < 0.45f ? DivePatternType.LoopingDive : roll < 0.7f ? DivePatternType.HighSpeedStrafe : DivePatternType.StandardSwoop;
                else
                    pattern = roll < 0.3f ? DivePatternType.LoopingDive : DivePatternType.StandardSwoop;
            }
            diver.StartDiveRun(playerPos, pattern);
        }

        private void TriggerRoleSpecial()
        {
            if (Projectile.ActiveEnemyCount >= maxEnemyBullets + 4) return;
            Vector3 playerPos = PlayerPos();

            // Rotate through roles so every special gets airtime.
            int start = Random.Range(0, 4);
            for (int k = 0; k < 4; k++)
            {
                switch ((start + k) % 4)
                {
                    case 0:
                        CollectReady(scratch, EnemyRole.Gunner);
                        if (scratch.Count > 0) { scratch[Random.Range(0, scratch.Count)].StartGunnerBurst(playerPos, projectileDamage, projectileSpeedMultiplier); return; }
                        break;
                    case 1:
                        CollectReady(scratch, EnemyRole.Bomber);
                        if (scratch.Count > 0) { scratch[Random.Range(0, scratch.Count)].DropMine(); return; }
                        break;
                    case 2:
                        CollectReady(scratch, EnemyRole.Blinker);
                        if (scratch.Count > 0) { scratch[Random.Range(0, scratch.Count)].StartBlink(); return; }
                        break;
                    case 3:
                        CollectReady(scratch, EnemyRole.Elite);
                        if (scratch.Count > 0) { scratch[Random.Range(0, scratch.Count)].FireFan(playerPos, 5, projectileDamage, projectileSpeedMultiplier * 0.9f); return; }
                        break;
                }
            }
        }
    }
}
