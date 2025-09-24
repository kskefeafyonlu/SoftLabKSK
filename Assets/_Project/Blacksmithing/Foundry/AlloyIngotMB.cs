using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    /// <summary>
    /// Runtime component for a poured alloy ingot (always spawned at 1.0 L by our foundry flow).
    /// Keeps the computed Alloy, an approximate mass, and a display tint blended from composition.
    /// </summary>
    [DisallowMultipleComponent]
    public class AlloyIngotMB : MonoBehaviour
    {
        [Header("Ingot Data")]
        public Alloy AlloyData;
        public float Liters = 1.0f;
        public float ApproxMassKg;
        public Color DisplayColor = Color.white;

        // Mass mapping: 0..100 density → 1..20 kg/L (linear). You can tune these later.
        const float MinKgPerLiter = 1f;
        const float MaxKgPerLiter = 20f;

        /// <summary>
        /// Factory: spawn an ingot prefab, assign data, set tint and safe 2D physics defaults.
        /// </summary>
        public static AlloyIngotMB Create(GameObject ingotPrefab, Transform parent, Vector3 worldPos, Alloy alloy, float liters = 1.0f)
        {
            if (ingotPrefab == null || alloy == null)
                return null;

            var go = Object.Instantiate(ingotPrefab, worldPos, Quaternion.identity, parent);

            var ingot = go.GetComponent<AlloyIngotMB>();
            if (ingot == null) ingot = go.AddComponent<AlloyIngotMB>();

            ingot.AlloyData = alloy;
            ingot.Liters = Mathf.Max(0f, liters);

            // Approx mass from blended density (0..100 -> 1..20 kg/L)
            float t = Mathf.Clamp01(alloy.Stats.Density / 100f);
            float kgPerLiter = Mathf.Lerp(MinKgPerLiter, MaxKgPerLiter, t);
            ingot.ApproxMassKg = ingot.Liters * kgPerLiter;

            // Color from composition
            ingot.DisplayColor = ComputeBlendColor(alloy.Composition);

            // Name for hierarchy/debug
            go.name = $"{(string.IsNullOrEmpty(alloy.Name) ? "Alloy" : alloy.Name)} Ingot (Tier: {alloy.Tier})";

            // Sprite tint (if present)
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = ingot.DisplayColor;

            // Ensure 2D physics defaults are sane
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.linearDamping = 2f;
            rb.angularDamping = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Ensure a collider exists
            if (go.GetComponent<Collider2D>() == null)
            {
                var cc = go.AddComponent<CircleCollider2D>();
                cc.radius = 0.22f;
                cc.compositeOperation = Collider2D.CompositeOperation.None;
            }

            return ingot;
        }

        /// <summary>
        /// Linear blend of metals' base colors weighted by composition (no gamma correction).
        /// If composition empty → light gray.
        /// </summary>
        public static Color ComputeBlendColor(Dictionary<Metal, float> composition)
        {
            if (composition == null || composition.Count == 0)
                return new Color(0.85f, 0.85f, 0.85f, 1f);

            // Ensure normalized
            float sum = 0f;
            foreach (var kv in composition) sum += Mathf.Max(0f, kv.Value);
            if (sum <= 0f) return Color.gray;

            float r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in composition)
            {
                if (kv.Key == null) continue;
                float w = Mathf.Max(0f, kv.Value) / sum;
                Color c = kv.Key.BaseColor;
                r += c.r * w;
                g += c.g * w;
                b += c.b * w;
                a += c.a * w;
            }
            return new Color(r, g, b, Mathf.Clamp01(a));
        }
    }
}
