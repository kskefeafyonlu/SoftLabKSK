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

        // ---- Contents (solid items; some will become IsMelted) ----
        public List<CrucibleEntry> Contents = new List<CrucibleEntry>();

        // ---- Furnace Temperature (°C) ----
        public int AmbientTempC = 20;   // used for new entries
        public int MinTempC = 200;
        public int MaxTempC = 1000;
        public int CurrentTempC = 200;

        // ---- Heating Model ----
        [Tooltip("Baseline heat transfer factor (per second). Higher heats ores faster.")]
        public float HeatRateBase = 0.25f;

        // ---- Melt Timing Scale ----
        [Tooltip("Global melt time scale (1=normal).")]
        public float GlobalMeltTimeScale = 1f;

        // ---- Loop State Flags ----
        public bool IsHeating = false;
        public bool IsSmelting = false; // reserved

        // ---- Stability & Impurities (future) ----
        public float StabilityMax = 100f;
        public float Stability = 100f;
        public float Impurities = 0f;
        public float ImpuritiesMax = 100f;

        // (Deprecated) auto-placement fields kept to avoid prefab breakage
        [Header("Auto Placement (deprecated; kept for prefab compat)")]
        public Transform PlacementOrigin;
        public Vector2 GridSize = new Vector2(4, 3);
        public Vector2 CellSpacing = new Vector2(0.2f, 0.2f);

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

        /// <summary>
        /// Commit an ore the player dropped inside the crucible:
        /// - Parents under crucible (keeps world position; no grid).
        /// - Freezes physics (Kinematic, no gravity).
        /// - Adds a CrucibleEntry initialized at AmbientTempC.
        /// </summary>
        public bool CommitOreAtAutoPosition(OreMB ore)
        {
            if (ore == null) return false;
            if (ore.AddedToCrucible) return false;
            if (ore.Liters <= 0f) return false;
            if (!ore.InCrucibleZone || ore.PendingCrucible != this) return false;

            // Capacity check
            if (FillLiters + ore.Liters > CapacityLiters) return false;

            // Map enum -> Metal
            Metal metal = MetalsUtil.FromId(ore.MetalId);

            // Freeze physics and mark as added (keep current position)
            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            ore.AddedToCrucible = true;

            // Parent to crucible, keep position
            Vector3 worldPos = ore.transform.position;
            ore.transform.SetParent(transform, worldPositionStays: true);
            ore.transform.position = worldPos;

            // Add entry (keeps GameObject for later hiding when melted)
            var entry = new CrucibleEntry(metal, ore.Liters, ore.gameObject);
            entry.CurrentTempC = AmbientTempC; // start at ambient
            Contents.Add(entry);

            // Let OreMB know its crucible (useful later for tinting, etc.)
            ore.CurrentCrucible = this;

            if (!IsHeating) IsHeating = true; // passive-start rule
            ore.PendingCrucible = null;
            ore.InCrucibleZone = false;

            return true;
        }

        /// <summary>
        /// Add directly (bypassing a world object).
        /// </summary>
        public bool TryAddOre(Metal metal, float liters)
        {
            if (metal == null || liters <= 0f) return false;
            if (FillLiters + liters > CapacityLiters) return false;

            var e = new CrucibleEntry(metal, liters, null);
            e.CurrentTempC = AmbientTempC;
            Contents.Add(e);
            if (!IsHeating) IsHeating = true;
            return true;
        }

        // ---- HEATING + MELTING LOOP ----
        private void Update()
        {
            if (Contents == null || Contents.Count == 0) return;

            float dt = Time.deltaTime;

            for (int i = 0; i < Contents.Count; i++)
            {
                var e = Contents[i];
                if (e == null) continue;
                if (e.IsMelted) continue;

                // 1) Heat transfer: approach crucible temperature using a per-metal rate
                //    dT = k * (CrucibleTemp - OreTemp) * dt
                //    k = HeatRateBase * Metal.HeatSensitivity
                float k = Mathf.Max(0f, HeatRateBase) * Mathf.Max(0.01f, e.Metal.HeatSensitivity);
                float delta = CurrentTempC - e.CurrentTempC;
                e.CurrentTempC += k * delta * dt;

                // Clamp to reasonable range
                float minClamp = Mathf.Min(AmbientTempC, CurrentTempC);
                float maxClamp = Mathf.Max(AmbientTempC, CurrentTempC);
                e.CurrentTempC = Mathf.Clamp(e.CurrentTempC, minClamp, maxClamp);

                // 2) Melt progress only when the ore itself is hot enough
                if (e.CurrentTempC >= e.Metal.MeltingPointC)
                {
                    float required = Mathf.Max(0.01f, e.Metal.MeltTimeSeconds * GlobalMeltTimeScale);
                    e.MeltProgressSeconds += dt;

                    if (e.MeltProgressSeconds >= required)
                    {
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
                    }
                }
            }
        }

        // ---- Data helpers ----

        public float GetMeltedLiters()
        {
            float sum = 0f;
            if (Contents == null) return sum;
            foreach (var e in Contents)
                if (e != null && e.IsMelted) sum += e.Liters;
            return sum;
        }

        public float GetSolidLiters()
        {
            float sum = 0f;
            if (Contents == null) return sum;
            foreach (var e in Contents)
                if (e != null && !e.IsMelted) sum += e.Liters;
            return sum;
        }

        /// <summary> Metal->liters map of the melted portion only. </summary>
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

        // ---- UI Alloy support (same surface as before) ----
        public Dictionary<Metal, float> GetMeltedAvailability() => GetMeltedComposition();

        public const float kTargetPourLiters = 1.0f;
        const float kEpsilon = 0.0005f;
        [Tooltip("Step size used by UI balancing and snapping.")] public float UIStepLiters = 0.1f;

        public Dictionary<Metal, float> TryBalanceToOneLiter(Dictionary<Metal, float> request)
        {
            var avail = GetMeltedAvailability();
            var result = new Dictionary<Metal, float>();
            if (avail.Count == 0) return result;

            float step = Mathf.Max(0.0001f, UIStepLiters);

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

            float sum = result.Values.Sum();
            if (sum <= 0f)
            {
                foreach (var kv in avail.OrderByDescending(p => p.Value))
                {
                    float remain = kTargetPourLiters - sum;
                    if (remain <= kEpsilon) break;

                    float add = Mathf.Min(remain, kv.Value);
                    add = SnapDown(step, add);
                    if (add > 0f)
                    {
                        result[kv.Key] = add;
                        sum += add;
                    }
                }
                sum = result.Values.Sum();
            }

            sum = Snap(step, sum);
            if (Mathf.Abs(sum - kTargetPourLiters) <= kEpsilon)
                return NormalizeToStep(result, step);

            if (sum < kTargetPourLiters - kEpsilon)
            {
                float remaining = kTargetPourLiters - sum;
                var candidateMetals = avail.Keys.ToList();
                var order = result.Keys.Concat(candidateMetals.Except(result.Keys)).ToList();

                int guard = 1000;
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
                    if (!progressed) break;
                }
            }
            else
            {
                float excess = sum - kTargetPourLiters;
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

        public bool PourExactlyOneLiter(Dictionary<Metal, float> selection)
        {
            if (selection == null || selection.Count == 0) return false;

            float sum = selection.Values.Sum();
            if (Mathf.Abs(sum - kTargetPourLiters) > kEpsilon) return false;

            var avail = GetMeltedAvailability();
            foreach (var kv in selection)
            {
                if (!avail.TryGetValue(kv.Key, out float a)) return false;
                if (kv.Value < -kEpsilon || kv.Value > a + kEpsilon) return false;
            }

            // Drain from melted entries by metal (FIFO over entries)
            foreach (var kv in selection)
            {
                Metal metal = kv.Key;
                float drainLeft = kv.Value;

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
                        Contents[i] = null; // mark; compact later
                    }
                }
                if (drainLeft > kEpsilon) return false; // safety
            }

            CompactContents();
            return true;
        }

        // -----------------------
        // Trigger helpers
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
