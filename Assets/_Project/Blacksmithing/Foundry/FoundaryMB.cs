using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    public class FoundaryMB : MonoBehaviour
    {
        // ---- Capacity ----
        public int CapacityLiters = 5;

        // ---- Contents (solid items, some may become IsMelted) ----
        public List<CrucibleEntry> Contents = new List<CrucibleEntry>();

        // ---- Furnace Temperature (°C) ----
        public int MinTempC = 200;
        public int MaxTempC = 1000;
        public int CurrentTempC = 200;

        // ---- Smelt Timing ----
        public float RequiredHoldSeconds = 8f;   // reserved for your later “hold” phase
        public float HoldTimer = 0f;             // reserved; not used in this step

        // Global melt speed scale (1 = normal; <1 faster; >1 slower)
        public float GlobalMeltTimeScale = 1f;

        // ---- Loop State Flags ----
        public bool IsHeating = false;   // becomes true once anything is inside
        public bool IsSmelting = false;  // reserved; not used yet

        // ---- Stability & Impurities ----
        public float StabilityMax = 100f;
        public float Stability = 100f;   // collapse → Scrap (logic later)
        public float Impurities = 0f;    // 0..100 (logic later)
        public float ImpuritiesMax = 100f;

        // ---- AUTO-PLACEMENT (bottom grid in local space) ----
        [Header("Auto Placement (local space)")]
        public Transform PlacementOrigin;              // anchor at crucible bottom-center
        public Vector2 GridSize = new Vector2(4, 3);   // columns (x), rows (y)
        public Vector2 CellSpacing = new Vector2(0.2f, 0.2f);

        // ---- UI/Alloying helpers ----
        [Header("Alloying")]
        [Tooltip("Step size used by UI balancing and snapping.")]
        public float UIStepLiters = 0.1f;

        public const float kTargetPourLiters = 1.0f;
        const float kEpsilon = 0.0005f;

        // ----------------
        // Derived helpers
        // ----------------
        public float FillLiters
        {
            get
            {
                float total = 0f;
                if (Contents != null)
                {
                    foreach (var entry in Contents)
                        if (entry != null)
                            total += entry.Liters;
                }
                return total;
            }
        }

        private Vector3 GetLocalGridPosition(int n)
        {
            if (GridSize.x < 1f) GridSize.x = 1f;
            if (GridSize.y < 1f) GridSize.y = 1f;

            int cols = Mathf.Max(1, Mathf.RoundToInt(GridSize.x));
            int row = n / cols;
            int col = n % cols;

            float totalW = (cols - 1) * CellSpacing.x;
            float x = -totalW * 0.5f + col * CellSpacing.x;
            float y = row * CellSpacing.y; // upward from bottom
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// Commit an ore the player dropped inside the crucible:
        /// - Freezes physics
        /// - Parents under crucible
        /// - Auto-places at bottom grid
        /// - Adds a CrucibleEntry (solid; will melt over time)
        /// </summary>
        public bool CommitOreAtAutoPosition(OreMB ore)
        {
            if (ore == null) return false;
            if (ore.AddedToCrucible) return false;
            if (ore.Liters <= 0f) return false;

            // Must be flagged by your trigger forwarder as inside this crucible
            if (!ore.InCrucibleZone || ore.PendingCrucible != this) return false;

            // Capacity check
            if (FillLiters + ore.Liters > CapacityLiters) return false;

            // Map enum -> Metal
            Metal metal = MetalsUtil.FromId(ore.MetalId);

            // Freeze physics and mark as added
            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.isKinematic = true;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            ore.AddedToCrucible = true;

            // Parent to crucible
            ore.transform.SetParent(transform, worldPositionStays: true);

            // Placement index = current count (simple fill order)
            int index = Contents != null ? Contents.Count : 0;

            // Compute world position from local grid
            Vector3 worldPos = (PlacementOrigin != null)
                ? PlacementOrigin.TransformPoint(GetLocalGridPosition(index))
                : transform.TransformPoint(GetLocalGridPosition(index));

            ore.transform.position = worldPos;

            // Add entry (keeps GameObject for later hiding when melted)
            Contents.Add(new CrucibleEntry(metal, ore.Liters, ore.gameObject));

            if (!IsHeating) IsHeating = true; // passive-start rule
            ore.PendingCrucible = null;
            ore.InCrucibleZone = false;

            return true;
        }

        /// <summary>
        /// Basic API if you ever want to add directly (bypassing the world object).
        /// </summary>
        public bool TryAddOre(Metal metal, float liters)
        {
            if (metal == null || liters <= 0f) return false;
            if (FillLiters + liters > CapacityLiters) return false;

            Contents.Add(new CrucibleEntry(metal, liters));
            if (!IsHeating) IsHeating = true;
            return true;
        }

        // ---- MELTING LOOP ----
        private void Update()
        {
            if (Contents == null || Contents.Count == 0) return;

            float dt = Time.deltaTime;

            for (int i = 0; i < Contents.Count; i++)
            {
                var e = Contents[i];
                if (e == null) continue;
                if (e.IsMelted) continue;

                // Only melt if hot enough (re-check every frame)
                if (CurrentTempC >= e.Metal.MeltingPointC)
                {
                    float required = Mathf.Max(0.01f, e.Metal.MeltTimeSeconds * GlobalMeltTimeScale);
                    e.MeltProgressSeconds += dt;

                    if (e.MeltProgressSeconds >= required)
                    {
                        // Mark melted and hide the solid object
                        e.IsMelted = true;

                        if (e.Obj != null)
                        {
                            var rend = e.Obj.GetComponent<Renderer>();
                            if (rend) rend.enabled = false;

                            var col2d = e.Obj.GetComponent<Collider2D>();
                            if (col2d) col2d.enabled = false;

                            var oreMB = e.Obj.GetComponent<OreMB>();
                            if (oreMB) oreMB.enabled = false;
                        }

                        // NOTE: No LiquidChunk; we just keep the entry marked as melted.
                        // Alloy composition can later be computed from melted entries.
                    }
                }
                else
                {
                    // Not hot enough → no progress (no cooling rollback here)
                }
            }
        }

        // ---- Simple data helpers (no visuals) ----

        /// <summary> Total liters that have finished melting. </summary>
        public float GetMeltedLiters()
        {
            float sum = 0f;
            if (Contents == null) return sum;
            foreach (var e in Contents)
                if (e != null && e.IsMelted) sum += e.Liters;
            return sum;
        }

        /// <summary> Total liters that are still solid. </summary>
        public float GetSolidLiters()
        {
            float sum = 0f;
            if (Contents == null) return sum;
            foreach (var e in Contents)
                if (e != null && !e.IsMelted) sum += e.Liters;
            return sum;
        }

        /// <summary>
        /// Returns a Metal->liters map of the melted portion only.
        /// </summary>
        public Dictionary<Metal, float> GetMeltedComposition()
        {
            var map = new Dictionary<Metal, float>();
            if (Contents == null) return map;

            foreach (var e in Contents)
            {
                if (e == null || !e.IsMelted || e.Metal == null || e.Liters <= 0f)
                    continue;

                if (!map.ContainsKey(e.Metal)) map[e.Metal] = 0f;
                map[e.Metal] += e.Liters;
            }
            return map;
        }

        /// <summary>
        /// Alias for UI: availability map of melted metal liters (per metal).
        /// </summary>
        public Dictionary<Metal, float> GetMeltedAvailability() => GetMeltedComposition();

        // -----------------------------
        // Alloy UI integration methods
        // -----------------------------

        /// <summary>
        /// Balances a requested selection to exactly 1.0 L, respecting availability and step snapping.
        /// - Clamps to melted availability
        /// - Snaps to UIStepLiters
        /// - If underfilled, distributes remaining proportionally to available headroom
        /// - If overfilled, trims proportionally
        /// </summary>
        public Dictionary<Metal, float> TryBalanceToOneLiter(Dictionary<Metal, float> request)
        {
            var avail = GetMeltedAvailability();
            var result = new Dictionary<Metal, float>();
            if (avail.Count == 0) return result;

            float step = Mathf.Max(0.0001f, UIStepLiters);

            // 1) Clamp requested to availability and snap
            if (request != null)
            {
                foreach (var kv in request)
                {
                    var metal = kv.Key;
                    if (metal == null) continue;
                    if (!avail.TryGetValue(metal, out float a) || a <= 0f) continue;

                    float v = Mathf.Clamp(kv.Value, 0f, a);
                    v = Snap(step, v);
                    if (v > 0f) result[metal] = v;
                }
            }

            // 2) Seed empty selection if nothing requested: take from the largest pools
            float sum = result.Values.Sum();
            if (sum <= 0f)
            {
                // Greedy fill from largest availability
                foreach (var kv in avail.OrderByDescending(p => p.Value))
                {
                    float remain = kTargetPourLiters - sum;
                    if (remain <= kEpsilon) break;

                    float add = Mathf.Min(remain, kv.Value);
                    add = SnapDown(step, add); // avoid overshooting
                    if (add > 0f)
                    {
                        result[kv.Key] = add;
                        sum += add;
                    }
                }
                sum = result.Values.Sum();
            }

            // Early exit if already perfect
            sum = Snap(step, sum);
            if (Mathf.Abs(sum - kTargetPourLiters) <= kEpsilon)
                return NormalizeToStep(result, step);

            // 3) If under target → add proportionally into headroom
            if (sum < kTargetPourLiters - kEpsilon)
            {
                float remaining = kTargetPourLiters - sum;

                // Headroom per selected metal; if none, consider any available metals
                var candidateMetals = avail.Keys.ToList();

                // Prefer already-selected metals first, then others with availability
                var order = result.Keys
                    .Concat(candidateMetals.Except(result.Keys))
                    .ToList();

                int guard = 1000; // avoid infinite loops due to floating error
                while (remaining > kEpsilon && guard-- > 0)
                {
                    bool progressed = false;

                    foreach (var m in order)
                    {
                        avail.TryGetValue(m, out float a);
                        result.TryGetValue(m, out float cur);
                        float headroom = Mathf.Max(0f, a - cur);
                        float stepAdd = Mathf.Min(step, remaining, headroom);
                        stepAdd = Snap(step, stepAdd);

                        if (stepAdd > 0f)
                        {
                            result[m] = cur + stepAdd;
                            remaining -= stepAdd;
                            progressed = true;
                            if (remaining <= kEpsilon) break;
                        }
                    }

                    if (!progressed) break; // nowhere to add more
                }
            }
            // 4) If over target → trim proportionally from current selection
            else
            {
                float excess = sum - kTargetPourLiters;

                // Sort by largest current first for cleaner UX
                var order = result.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();

                int guard = 1000;
                while (excess > kEpsilon && guard-- > 0)
                {
                    bool progressed = false;
                    foreach (var m in order)
                    {
                        float cur = result[m];
                        float stepDec = Mathf.Min(step, cur, excess);
                        stepDec = Snap(step, stepDec);
                        if (stepDec > 0f)
                        {
                            float nv = cur - stepDec;
                            if (nv <= kEpsilon) { result.Remove(m); }
                            else { result[m] = nv; }
                            excess -= stepDec;
                            progressed = true;
                            if (excess <= kEpsilon) break;
                        }
                    }
                    if (!progressed) break;
                }
            }

            return NormalizeToStep(ClampToAvailability(result, avail), UIStepLiters);
        }

        /// <summary>
        /// Executes a 1.0 L pour if valid. Drains melted entries and returns true if successful.
        /// </summary>
        public bool PourExactlyOneLiter(Dictionary<Metal, float> selection)
        {
            if (selection == null || selection.Count == 0) return false;

            // 1) Validate sum and availability
            float sum = selection.Values.Sum();
            if (Mathf.Abs(sum - kTargetPourLiters) > kEpsilon) return false;

            var avail = GetMeltedAvailability();
            foreach (var kv in selection)
            {
                if (!avail.TryGetValue(kv.Key, out float a)) return false;
                if (kv.Value < -kEpsilon || kv.Value > a + kEpsilon) return false;
            }

            // 2) Drain from melted entries by metal (FIFO over entries)
            foreach (var kv in selection)
            {
                Metal metal = kv.Key;
                float drainLeft = kv.Value;

                // Iterate over melted entries of this metal
                for (int i = 0; i < Contents.Count && drainLeft > kEpsilon; i++)
                {
                    var e = Contents[i];
                    if (e == null || !e.IsMelted || e.Metal != metal) continue;

                    float take = Mathf.Min(e.Liters, drainLeft);
                    e.Liters -= take;
                    drainLeft -= take;

                    if (e.Liters <= kEpsilon)
                    {
                        e.Liters = 0f;
                        // If this entry had an Obj (solid), it's already hidden. Safe to remove it.
                        Contents[i] = null; // mark null; compact later
                    }
                }

                if (drainLeft > kEpsilon)
                {
                    // Should not happen due to availability check; abort to be safe.
                    return false;
                }
            }

            // 3) Compact nulls out of Contents
            CompactContents();

            // 4) Create Alloy and hand off to inventory pipeline (optional here)
            // The UI preview already computed; if you need the authoritative object now:
            // var alloy = AlloyMath.MakeAlloy(selection);
            // TODO: Inventory.AddIngot(alloy);

            return true;
        }

        // -----------------------
        // Trigger helper (optional)
        // -----------------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            var ore = other.GetComponent<OreMB>();
            if (ore != null && !ore.AddedToCrucible)
            {
                ore.InCrucibleZone = true;
                ore.PendingCrucible = this;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var ore = other.GetComponent<OreMB>();
            if (ore != null && !ore.AddedToCrucible && ore.PendingCrucible == this)
            {
                ore.InCrucibleZone = false;
                ore.PendingCrucible = null;
            }
        }

        // -----------------------
        // Internal utilities
        // -----------------------
        float Snap(float step, float v) => (step <= 0f) ? v : Mathf.Round(v / step) * step;
        float SnapDown(float step, float v) => (step <= 0f) ? v : Mathf.Floor(v / step) * step;

        Dictionary<Metal, float> ClampToAvailability(Dictionary<Metal, float> src, Dictionary<Metal, float> avail)
        {
            var dst = new Dictionary<Metal, float>();
            foreach (var kv in src)
            {
                if (kv.Key == null) continue;
                if (!avail.TryGetValue(kv.Key, out float a) || a <= 0f) continue;
                dst[kv.Key] = Mathf.Clamp(kv.Value, 0f, a);
            }
            return dst;
        }

        Dictionary<Metal, float> NormalizeToStep(Dictionary<Metal, float> src, float step)
        {
            var dst = new Dictionary<Metal, float>();
            foreach (var kv in src)
            {
                float v = Snap(step, kv.Value);
                if (v > kEpsilon) dst[kv.Key] = v;
            }

            // Best-effort final tweak to hit target exactly: adjust largest value by residual
            float sum = dst.Values.Sum();
            float residual = kTargetPourLiters - sum;

            if (Mathf.Abs(residual) <= kEpsilon) return dst;

            if (dst.Count > 0)
            {
                var biggest = dst.OrderByDescending(k => k.Value).First().Key;
                float nv = Mathf.Max(0f, dst[biggest] + residual);
                dst[biggest] = Snap(step, nv);
            }
            return dst;
        }

        void CompactContents()
        {
            if (Contents == null) return;
            for (int i = Contents.Count - 1; i >= 0; i--)
            {
                var e = Contents[i];
                if (e == null || e.Liters <= kEpsilon)
                {
                    Contents.RemoveAt(i);
                }
            }
        }
    }
}
