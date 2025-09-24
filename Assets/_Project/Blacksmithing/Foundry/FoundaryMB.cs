using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    public class FoundaryMB : MonoBehaviour
    {
        // ---- Capacity ----
        public int CapacityLiters = 5;

        // ---- Contents ----
        public List<CrucibleEntry> Contents = new List<CrucibleEntry>();

        // Fast lookups to avoid fragile searches
        private readonly Dictionary<string, int> _idxByGuid = new Dictionary<string, int>(64);
        private readonly Dictionary<GameObject, string> _guidByObj = new Dictionary<GameObject, string>(64);

        // ---- Temperatures ----
        public int AmbientTempC = 20;
        public int MinTempC = 200;
        public int MaxTempC = 1000;
        public int CurrentTempC = 200;

        // ---- Heating model ----
        [Tooltip("Baseline heat transfer factor (per second). Higher heats/cools faster.")]
        public float HeatRateBase = 0.25f;

        // ---- Phase thresholds (hysteresis) ----
        [Header("Phase Change")]
        [Tooltip("Extra °C above MeltingPoint needed to melt (prevents flicker).")]
        public float MeltHysteresisC = 10f;
        [Tooltip("Extra °C below MeltingPoint needed to solidify.")]
        public float SolidifyHysteresisC = 10f;

        [Tooltip("Multiply melt/solidify time globally (1 = as defined on Metal).")]
        public float GlobalMeltTimeScale = 1f;
        [Tooltip("If true, solidifying uses the same time as melting; otherwise scales by SolidifyTimeFactor.")]
        public bool SolidifyUsesSameTime = true;
        public float SolidifyTimeFactor = 1.0f;

        // ---- Object handling for phase change ----
        public enum MeltedObjPolicy { HideDisable, Destroy }

        [Header("Object Handling")]
        public MeltedObjPolicy OnMelt = MeltedObjPolicy.Destroy;
        public GameObject OrePrefab;
        public float DestroyDelay = 0.05f;

        // ---- Spawn/placement inside crucible ----
        [Header("Spawn Area (optional)")]
        public BoxCollider2D SpawnArea;
        public LayerMask OreLayerMask = ~0;
        public float SpawnPadding = 0.05f;
        public int SpawnMaxTries = 20;
        public float SettleFreezeDelay = 0.25f;
        public float SpawnJitterImpulse = 0.5f;

        // ---- Area binding / exit behavior ----
        [Header("Area Binding")]
        [Tooltip("Assign the crucible trigger collider (2D). We auto-uncommit solids that leave this area.")]
        public Collider2D CrucibleArea;
        [Tooltip("If true, any solid ore that goes outside CrucibleArea is automatically uncommitted.")]
        public bool AutoUncommitWhenOutsideArea = true;

        [Header("Pickup/Exit Behavior")]
        public bool AutoUncommitOnExit = true;

        // ---- Loop flags ----
        public bool IsHeating = false;
        public bool IsSmelting = false;

        // (Deprecated) auto-placement (kept for prefab compat)
        [Header("Auto Placement (deprecated)")]
        public Transform PlacementOrigin;
        public Vector2 GridSize = new Vector2(4, 3);
        public Vector2 CellSpacing = new Vector2(0.2f, 0.2f);

        // ---- Debug ----
        public bool DebugLogs = false;
        void DLog(string msg) { if (DebugLogs) Debug.Log($"[FoundaryMB] {msg}"); }

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

        // ----------------
        // Bind helpers
        // ----------------
        void RegisterEntry(CrucibleEntry e, int index)
        {
            _idxByGuid[e.Guid] = index;
            if (e.Obj != null)
                _guidByObj[e.Obj] = e.Guid;
        }

        void ReindexDictionaries()
        {
            _idxByGuid.Clear();
            _guidByObj.Clear();
            for (int i = 0; i < Contents.Count; i++)
            {
                var e = Contents[i];
                if (e == null) continue;
                _idxByGuid[e.Guid] = i;
                if (e.Obj != null) _guidByObj[e.Obj] = e.Guid;
            }
        }

        int FindIndexByGuid(string guid)
        {
            if (!string.IsNullOrEmpty(guid) && _idxByGuid.TryGetValue(guid, out var idx))
            {
                if (idx >= 0 && idx < Contents.Count && Contents[idx] != null && Contents[idx].Guid == guid)
                    return idx;
            }
            return -1;
        }

        int FindIndexByObj(GameObject obj)
        {
            if (obj != null && _guidByObj.TryGetValue(obj, out var guid))
                return FindIndexByGuid(guid);

            // Fallback slow search
            for (int i = 0; i < Contents.Count; i++)
            {
                var e = Contents[i];
                if (e != null && e.Obj == obj) return i;
            }
            return -1;
        }

        // ----------------
        // Optional: spawn inside crucible
        // ----------------
        public OreMB SpawnOreInCrucible(GameObject orePrefab, MetalId id, float liters)
        {
            Vector3 pos = transform.position;
            if (SpawnArea != null && !TryFindSpawnPoint(out pos))
                pos = SpawnArea.bounds.center;

            var ore = OreFactory.CreateOre(orePrefab, id, liters, pos);
            if (ore == null) return null;

            ore.InCrucibleZone = true;
            ore.PendingCrucible = this;

            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb) rb.AddForce(Random.insideUnitCircle.normalized * SpawnJitterImpulse, ForceMode2D.Impulse);

            bool ok = CommitOreAtAutoPosition(ore, SettleFreezeDelay);
            if (!ok) { Destroy(ore.gameObject); return null; }
            return ore;
        }

        bool TryFindSpawnPoint(out Vector3 pos)
        {
            if (SpawnArea == null) { pos = transform.position; return true; }

            var b = SpawnArea.bounds;
            for (int i = 0; i < SpawnMaxTries; i++)
            {
                float rx = Random.Range(-0.5f, 0.5f);
                float ry = Random.Range(-0.5f, 0.5f);
                Vector3 p = new Vector3(
                    Mathf.Lerp(b.min.x, b.max.x, 0.5f + rx * 0.9f),
                    Mathf.Lerp(b.min.y, b.max.y, 0.5f + ry * 0.9f),
                    transform.position.z
                );
                var hit = Physics2D.OverlapCircle((Vector2)p, SpawnPadding, OreLayerMask);
                if (hit == null) { pos = p; return true; }
            }
            pos = b.center; return false;
        }

        // ----------------
        // Commit (with optional settle)
        // ----------------
        public bool CommitOreAtAutoPosition(OreMB ore, float settleSeconds)
        {
            if (!CommitOreAtAutoPosition(ore)) return false;

            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.linearDamping = 2f;
                rb.angularDamping = 2f;
                StartCoroutine(FreezeAfterDelay(rb, settleSeconds));
            }
            return true;
        }

        IEnumerator FreezeAfterDelay(Rigidbody2D rb, float seconds)
        {
            float t = Mathf.Max(0f, seconds);
            while (t > 0f) { t -= Time.deltaTime; yield return null; }
            if (rb == null) yield break;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        /// <summary>
        /// Commit an ore: create entry, bind GUIDs, then parent and freeze.
        /// Restores saved thermal state if the ore had one (so progress continues).
        /// </summary>
        public bool CommitOreAtAutoPosition(OreMB ore)
        {
            if (ore == null) return false;
            if (ore.AddedToCrucible) return false;
            if (ore.Liters <= 0f) return false;
            if (!ore.InCrucibleZone || ore.PendingCrucible != this) return false;
            if (CrucibleArea != null && !CrucibleArea.bounds.Contains(ore.transform.position)) return false; // must actually be inside
            if (FillLiters + ore.Liters > CapacityLiters) return false;

            var metal = MetalsUtil.FromId(ore.MetalId);
            if (metal == null) return false;

            var entry = new CrucibleEntry(metal, ore.Liters, ore.gameObject);
            if (ore.ConsumeThermalState(out float savedT, out float savedMelt, out float savedSolid))
            {
                entry.CurrentTempC = savedT;
                entry.MeltProgressSeconds = savedMelt;
                entry.SolidifyProgressSeconds = savedSolid;
            }
            else
            {
                entry.CurrentTempC = AmbientTempC;
            }

            ore.BoundEntryGuid = entry.Guid;
            Contents.Add(entry);
            RegisterEntry(entry, Contents.Count - 1);

            ore.AddedToCrucible = true;
            ore.CurrentCrucible = this;
            ore.PendingCrucible = null;
            ore.InCrucibleZone = false;

            Vector3 worldPos = ore.transform.position;
            ore.transform.SetParent(transform, worldPositionStays: true);
            ore.transform.position = worldPos;

            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (!IsHeating) IsHeating = true;
            DLog($"Committed ore -> entry {entry.Guid} ({metal.Name}, {ore.Liters} L)");
            return true;
        }

        public bool TryAddOre(Metal metal, float liters)
        {
            if (metal == null || liters <= 0f) return false;
            if (FillLiters + liters > CapacityLiters) return false;

            var e = new CrucibleEntry(metal, liters, null) { CurrentTempC = AmbientTempC };
            Contents.Add(e);
            RegisterEntry(e, Contents.Count - 1);
            if (!IsHeating) IsHeating = true;
            return true;
        }

        // ----------------
        // UNCOMMIT (pickup)
        // ----------------
        public bool TryUncommitOre(OreMB ore)
        {
            if (ore == null) return false;
            if (!ore.AddedToCrucible) return false;
            if (ore.CurrentCrucible != this) return false;

            int idx = FindIndexByGuid(ore.BoundEntryGuid);
            if (idx < 0) idx = FindIndexByObj(ore.gameObject);
            if (idx < 0) return false;

            return AutoUncommitEntry(idx, detachTransform:true);
        }

        // --- NEW: hard guard — auto uncommit if an entry’s object is outside area
        bool AutoUncommitIfOutsideArea(int index)
        {
            if (!AutoUncommitWhenOutsideArea) return false;
            if (CrucibleArea == null) return false;
            if (index < 0 || index >= Contents.Count) return false;

            var e = Contents[index];
            if (e == null || e.IsMelted) return false; // only solids
            if (e.Obj == null) return false;

            if (!CrucibleArea.bounds.Contains(e.Obj.transform.position))
            {
                DLog($"Entry {e.Guid} is outside area → auto-uncommit.");
                return AutoUncommitEntry(index, detachTransform:false); // we'll leave parenting as-is to respect user's scene
            }
            return false;
        }

        // Core uncommit logic (used by manual pickup and area-based auto-uncommit)
        bool AutoUncommitEntry(int index, bool detachTransform)
        {
            if (index < 0 || index >= Contents.Count) return false;
            var e = Contents[index];
            if (e == null) return false;
            if (e.IsMelted) return false;

            var ore = (e.Obj != null) ? e.Obj.GetComponent<OreMB>() : null;

            if (ore != null)
            {
                // Save thermal back to ore
                ore.SaveThermalState(e.CurrentTempC, e.MeltProgressSeconds, e.SolidifyProgressSeconds);

                // Clear ore ownership
                ore.AddedToCrucible = false;
                ore.CurrentCrucible = null;
                ore.InCrucibleZone = false;
                ore.PendingCrucible = null;
                ore.BoundEntryGuid = null;

                if (detachTransform)
                    ore.transform.SetParent(null, true);
            }

            // Remove entry & reindex
            Contents.RemoveAt(index);
            ReindexDictionaries();

            return true;
        }

        // ----------------
        // Heating + Phase loop
        // ----------------
        private void Update()
        {
            if (Contents == null || Contents.Count == 0)
                return;

            float dt = Time.deltaTime;

            for (int i = 0; i < Contents.Count; i++)
            {
                var e = Contents[i];
                if (e == null) continue;

                // If the linked object was destroyed and the entry isn't liquid, drop the entry.
                if (e.Obj == null && !e.IsMelted)
                {
                    DLog($"Entry {e.Guid} removed (Obj null while solid).");
                    Contents[i] = null;
                    continue;
                }

                // NEW: If outside area and still solid → auto-uncommit so it can't keep heating/liquifying
                if (AutoUncommitIfOutsideArea(i))
                {
                    // Entry list changed; adjust index and continue.
                    i--;
                    continue;
                }

                // --- Heat transfer
                float k = Mathf.Max(0f, HeatRateBase) * Mathf.Max(0.01f, e.Metal.HeatSensitivity);
                float delta = CurrentTempC - e.CurrentTempC;
                e.CurrentTempC += k * delta * dt;

                float minClamp = Mathf.Min(AmbientTempC, CurrentTempC);
                float maxClamp = Mathf.Max(AmbientTempC, CurrentTempC);
                e.CurrentTempC = Mathf.Clamp(e.CurrentTempC, minClamp, maxClamp);

                // --- Phase change
                float meltStart     = e.Metal.MeltingPointC + Mathf.Max(0f, MeltHysteresisC);
                float solidifyStart = e.Metal.MeltingPointC - Mathf.Max(0f, SolidifyHysteresisC);

                float meltRequired = Mathf.Max(0.01f, e.Metal.MeltTimeSeconds * GlobalMeltTimeScale);
                float solidifyRequired = SolidifyUsesSameTime
                    ? meltRequired
                    : Mathf.Max(0.01f, e.Metal.MeltTimeSeconds * SolidifyTimeFactor * GlobalMeltTimeScale);

                if (!e.IsMelted)
                {
                    if (e.CurrentTempC >= meltStart)
                    {
                        e.MeltProgressSeconds += dt;
                        e.SolidifyProgressSeconds = 0f;

                        if (e.MeltProgressSeconds >= meltRequired)
                        {
                            e.IsMelted = true;
                            e.MeltProgressSeconds = 0f;
                            HandleMeltedObject(e);
                            DLog($"Entry {e.Guid} melted.");
                        }
                    }
                    else
                    {
                        e.MeltProgressSeconds = Mathf.Max(0f, e.MeltProgressSeconds - dt * 0.2f);
                        e.SolidifyProgressSeconds = 0f;
                    }
                }
                else
                {
                    if (e.CurrentTempC <= solidifyStart)
                    {
                        e.SolidifyProgressSeconds += dt;
                        e.MeltProgressSeconds = 0f;

                        if (e.SolidifyProgressSeconds >= solidifyRequired)
                        {
                            e.IsMelted = false;
                            e.SolidifyProgressSeconds = 0f;

                            if (OnMelt == MeltedObjPolicy.HideDisable)
                                RestoreSolidObject(e);
                            else
                                RespawnSolidObject(e);

                            DLog($"Entry {e.Guid} solidified.");
                        }
                    }
                    else
                    {
                        e.SolidifyProgressSeconds = Mathf.Max(0f, e.SolidifyProgressSeconds - dt * 0.2f);
                        e.MeltProgressSeconds = 0f;
                    }
                }
            }

            CompactNullEntries();
        }

        void CompactNullEntries()
        {
            bool changed = false;
            for (int i = Contents.Count - 1; i >= 0; i--)
            {
                if (Contents[i] == null)
                {
                    Contents.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed) ReindexDictionaries();
        }

        // ---- Melt object handlers ----
        void HandleMeltedObject(CrucibleEntry e)
        {
            if (e.Obj == null) return;

            if (OnMelt == MeltedObjPolicy.Destroy)
            {
                var go = e.Obj;
                e.Obj = null;
                _guidByObj.Remove(go);

                if (DestroyDelay <= 0f) Destroy(go);
                else StartCoroutine(DestroyAfter(go, DestroyDelay));
            }
            else
            {
                var rend = e.Obj.GetComponent<Renderer>(); if (rend) rend.enabled = false;
                var col2d = e.Obj.GetComponent<Collider2D>(); if (col2d) col2d.enabled = false;
            }
        }

        IEnumerator DestroyAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go) Destroy(go);
        }

        void RestoreSolidObject(CrucibleEntry e)
        {
            if (e.Obj == null) return;
            var rend = e.Obj.GetComponent<Renderer>(); if (rend) rend.enabled = true;
            var col2d = e.Obj.GetComponent<Collider2D>(); if (col2d) col2d.enabled = true;

            var rb = e.Obj.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        void RespawnSolidObject(CrucibleEntry e)
        {
            if (OrePrefab == null)
            {
                Debug.LogWarning("[FoundaryMB] RespawnSolidObject: OrePrefab not assigned.");
                return;
            }

            Vector3 pos;
            if (!TryFindSpawnPoint(out pos))
                pos = (SpawnArea != null) ? SpawnArea.bounds.center : transform.position;

            var ore = OreFactory.CreateOre(OrePrefab, ToId(e.Metal), e.Liters, pos);
            if (ore == null) return;

            ore.BoundEntryGuid = e.Guid;
            e.Obj = ore.gameObject;
            _guidByObj[ore.gameObject] = e.Guid;

            ore.AddedToCrucible = true;
            ore.InCrucibleZone = false;
            ore.PendingCrucible = null;
            ore.CurrentCrucible = this;

            ore.transform.SetParent(transform, true);
            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        MetalId ToId(Metal m)
        {
            if (m == Metals.Iron) return MetalId.Iron;
            if (m == Metals.Copper) return MetalId.Copper;
            if (m == Metals.Silver) return MetalId.Silver;
            if (m == Metals.Mithril) return MetalId.Mithril;
            if (m == Metals.Adamantite) return MetalId.Adamantite;
            if (m == Metals.Gold) return MetalId.Gold;
            return MetalId.Iron;
        }

        // ----------------
        // Triggers
        // ----------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            var ore = other.GetComponent<OreMB>();
            if (ore != null && !ore.AddedToCrucible)
            {
                ore.InCrucibleZone = true;
                ore.PendingCrucible = this;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
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
            if (ore == null) return;

            if (!ore.AddedToCrucible && ore.PendingCrucible == this)
            {
                ore.InCrucibleZone = false;
                ore.PendingCrucible = null;
                return;
            }

            if (AutoUncommitOnExit && ore.AddedToCrucible && ore.CurrentCrucible == this)
            {
                TryUncommitOre(ore); // no-op if melted
            }
        }

        // ----------------
        // Alloy helpers (unchanged)
        // ----------------
        public float GetMeltedLiters()
        {
            float sum = 0f;
            if (Contents == null) return sum;
            foreach (var e in Contents)
                if (e != null && e.IsMelted) sum += e.Liters;
            return sum;
        }

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
                        Contents[i] = null;
                    }
                }
                if (drainLeft > kEpsilon) return false;
            }

            CompactNullEntries();
            return true;
        }

        // ----------------
        // Utils
        // ----------------
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
    }
}
