using System.Collections.Generic;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    public class FoundaryMB : MonoBehaviour
    {
        // ---- Capacity ----
        public int CapacityLiters = 5;

        // ---- Contents ----
        public List<CrucibleEntry> Contents = new List<CrucibleEntry>();

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

        // ---- Furnace Temperature (°C) ----
        public int MinTempC = 200;
        public int MaxTempC = 1000;
        public int CurrentTempC = 200;

        // ---- Smelt Timing ----
        public float RequiredHoldSeconds = 8f;
        public float HoldTimer = 0f;

        // inside FoundaryMB (just new fields shown)
        public float LiquidLiters = 0f; // total molten volume in the crucible
        public float GlobalMeltTimeScale = 1f; // 1 = normal; <1 faster; >1 slower

        // ---- Loop State Flags ----
        public bool IsHeating = false; // becomes true when anything is inside
        public bool IsSmelting = false; // true while actively smelting

        // ---- Stability & Impurities ----
        public float StabilityMax = 100f;
        public float Stability = 100f; // collapse → Scrap
        public float Impurities = 0f; // 0..100
        public float ImpuritiesMax = 100f;

        // Simple option: remove ore world object after adding
        public bool DestroyOreOnAdd = true;

        [Header("Liquid Pool (invisible)")] public List<LiquidChunk> Liquid = new List<LiquidChunk>();

// Total molten liters (derived helper; keep your old LiquidLiters if you prefer)
        public float TotalLiquidLiters
        {
            get
            {
                float sum = 0f;
                if (Liquid != null)
                {
                    for (int i = 0; i < Liquid.Count; i++)
                    {
                        var c = Liquid[i];
                        if (c != null) sum += c.Liters;
                    }
                }

                return sum;
            }
        }

        // FoundaryMB.cs (add to the class)
        private void Update()
        {
            if (Contents == null || Contents.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < Contents.Count; i++)
            {
                var e = Contents[i];
                if (e == null) continue;
                if (e.IsMelted) continue;

                // Only melt if hot enough
                if (CurrentTempC >= e.Metal.MeltingPointC)
                {
                    float required = Mathf.Max(0.01f, e.Metal.MeltTimeSeconds * GlobalMeltTimeScale);
                    e.MeltProgressSeconds += dt;

                    if (e.MeltProgressSeconds >= required)
                    {
                        e.IsMelted = true;

                        // Hide/disable the solid object
                        if (e.Obj != null)
                        {
                            var renderer = e.Obj.GetComponent<Renderer>();
                            if (renderer) renderer.enabled = false;
                            var col2d = e.Obj.GetComponent<Collider2D>();
                            if (col2d) col2d.enabled = false;
                            var oreMB = e.Obj.GetComponent<OreMB>();
                            if (oreMB) oreMB.enabled = false;
                        }

                        // NEW: create a melted block and store it
                        Liquid.Add(new LiquidChunk(e.Metal, e.Liters, CurrentTempC));

                        // (Optional) If you still keep a simple float meter, you can update it too:
                        // LiquidLiters += e.Liters;
                    }
                }
                else
                {
                    // Not hot enough: no progress (kept basic; no cooling rollback here)
                }
            }
        }

        [Header("Auto Placement (local space)")]
        public Transform
            PlacementOrigin; // anchor Transform at crucible bottom-center (make this a child of the crucible)

        public Vector2 GridSize = new Vector2(4, 3); // columns (x), rows (y) to fill before wrapping
        public Vector2 CellSpacing = new Vector2(0.2f, 0.2f); // spacing between placed items (units)

        // Helper: computes local position for index n (fills left→right, bottom→up)
        private Vector3 GetLocalGridPosition(int n)
        {
            if (GridSize.x < 1f) GridSize.x = 1f;
            if (GridSize.y < 1f) GridSize.y = 1f;

            int cols = Mathf.Max(1, Mathf.RoundToInt(GridSize.x));
            // row/col from index
            int row = n / cols;
            int col = n % cols;

            // center the grid around origin (0,0) in local space
            float totalW = (cols - 1) * CellSpacing.x;
            float totalH = (Mathf.Max(1, Mathf.RoundToInt(GridSize.y)) - 1) * CellSpacing.y;

            float x = -totalW * 0.5f + col * CellSpacing.x;
            float y = row * CellSpacing.y; // going "up" (positive Y) from bottom

            return new Vector3(x, y, 0f);
        }

        public bool CommitOreAtAutoPosition(OreMB ore)
        {
            if (ore == null) return false;
            if (ore.AddedToCrucible) return false;
            if (ore.Liters <= 0f) return false;

            // Must be inside a crucible trigger (flag set by forwarder)
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

            // Decide placement index = existing count of solids (melted items keep their entries; we still just use count for a simple fill)
            int index = 0;
            if (Contents != null) index = Contents.Count;

            // Compute local position under PlacementOrigin
            Vector3 localPos = Vector3.zero;
            if (PlacementOrigin != null)
            {
                localPos = PlacementOrigin.TransformPoint(GetLocalGridPosition(index));
            }
            else
            {
                // Fallback: use crucible’s own local space
                localPos = transform.TransformPoint(GetLocalGridPosition(index));
            }

            // Move ore to chosen spot (world position)
            ore.transform.position = localPos;

            // Optional: slight random Z to avoid perfect overlap in 2D sort (can remove if you sort by order in layer)
            // var p = ore.transform.position; p.z += 0.001f * index; ore.transform.position = p;

            // Add to contents (keeps the GameObject reference for melting later)
            Contents.Add(new CrucibleEntry(metal, ore.Liters, ore.gameObject));

            if (!IsHeating) IsHeating = true; // passive start

            // Clear pending flags
            ore.PendingCrucible = null;
            ore.InCrucibleZone = false;

            return true;
        }

// FoundaryMB.cs (inside your class, add these fields)
        public float CommitDistance = 0.25f; // how far inside the trigger the ore must travel to "commit"
        public Vector2 CommitDirection = Vector2.down; // direction considered "deeper into crucible" (2D)

// Call this when an ore should finally be added (after commit distance is met)
        public bool CommitOreFromZone(OreMB ore)
        {
            if (ore == null) return false;
            if (ore.AddedToCrucible) return false;
            if (ore.Liters <= 0f) return false;

            Metal metal = MetalsUtil.FromId(ore.MetalId);

            // Capacity check
            if (FillLiters + ore.Liters > CapacityLiters) return false;

            // Park visually inside crucible (no destroy)
            ore.AddedToCrucible = true;

            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.isKinematic = true;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            ore.transform.SetParent(transform, true);

            // Add entry
            Contents.Add(new CrucibleEntry(metal, ore.Liters, ore.gameObject));

            if (!IsHeating) IsHeating = true; // passive start rule

            // clear pending references on ore
            ore.PendingCrucible = null;
            ore.InCrucibleZone = false;

            return true;
        }

        // Add an ore into the crucible (basic guard on capacity)
        public bool TryAddOre(Metal metal, float liters)
        {
            if (metal == null || liters <= 0f) return false;

            if (FillLiters + liters > CapacityLiters)
            {
                Debug.LogWarning("Crucible is full! Cannot add more ore.");
                return false;
            }

            Contents.Add(new CrucibleEntry(metal, liters));

            if (!IsHeating) IsHeating = true; // passive start

            return true;
        }

        public float GetLiquidFill01()
        {
            float cap = Mathf.Max(0.0001f, CapacityLiters);
            return Mathf.Clamp01(TotalLiquidLiters / cap);
        }

        public Color GetLiquidMixedColor()
        {
            if (Liquid == null || Liquid.Count == 0) return new Color(0, 0, 0, 0);

            float r = 0f, g = 0f, b = 0f, a = 0f;
            float total = 0f;

            for (int i = 0; i < Liquid.Count; i++)
            {
                var c = Liquid[i];
                if (c == null || c.Metal == null || c.Liters <= 0f) continue;

                Color bc = c.Metal.BaseColor;
                r += bc.r * c.Liters;
                g += bc.g * c.Liters;
                b += bc.b * c.Liters;
                a += 1f * c.Liters; // keep alpha opaque if anything is present
                total += c.Liters;
            }

            if (total <= 0f) return new Color(0, 0, 0, 0);

            return new Color(r / total, g / total, b / total, a / total);
        }

        // Called by child trigger forwarders (keeps handling centralized here)
// Called by child 2D trigger forwarders
        // FoundaryMB.cs (add/replace this method)
        public void HandleChildTriggerEnter2D(Collider2D other)
        {
            var ore = other.GetComponent<OreMB>();
            if (ore == null) return;
            if (ore.AddedToCrucible) return;
            if (ore.Liters <= 0f) return;

            Metal metal = MetalsUtil.FromId(ore.MetalId);

            // Capacity guard
            if (FillLiters + ore.Liters > CapacityLiters) return;

            // Park the object visually inside the crucible (no destroy)
            ore.AddedToCrucible = true;

            var rb = ore.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.isKinematic = true;
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // Parent to the crucible so it "belongs" here. You can snap position if you like.
            ore.transform.SetParent(transform, true);

            // Record as a crucible entry (note: we pass the GameObject reference)
            Contents.Add(new CrucibleEntry(metal, ore.Liters, ore.gameObject));

            // Passive start once something is inside
            if (!IsHeating) IsHeating = true;
        }
    }
}