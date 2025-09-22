// AlloyMath.cs

using System.Collections.Generic;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    public static class AlloyMath
    {
        // ---------------------------
        // 1) Weights (W2), sum = 1.0
        // ---------------------------
        public const float W_Workability    = 0.28f;
        public const float W_Toughness      = 0.28f;
        public const float W_Sharpenability = 0.24f;
        public const float W_Density        = 0.10f;
        public const float W_Arcana         = 0.10f;

        // -----------------------------------------
        // 2) Score → Tier (rescaled, skewed ranges)
        //    (Numbers are hidden from the player)
        // -----------------------------------------
        public static QualityTier ScoreToTier(float score)
        {
            if (score < 10f) return QualityTier.Scrap;
            if (score < 25f) return QualityTier.Poor;
            if (score < 40f) return QualityTier.Common;
            if (score < 55f) return QualityTier.Decent;
            if (score < 68f) return QualityTier.Fine;
            if (score < 78f) return QualityTier.Superior;
            if (score < 86f) return QualityTier.Refined;
            if (score < 93f) return QualityTier.Exalted;
            return QualityTier.Mythic; // 93..100
        }

        // --------------------------------------
        // 3) Compute weighted score (0..100 W2)
        // --------------------------------------
        public static float ComputeScore(MetalStats s)
        {
            float score =
                s.Workability    * W_Workability    +
                s.Toughness      * W_Toughness      +
                s.Sharpenability * W_Sharpenability +
                s.Density        * W_Density        +
                s.Arcana         * W_Arcana;

            return Mathf.Clamp(score, 0f, 100f);
        }

        // --------------------------------------------------------
        // 4) Normalize liters → proportions (0..1, sum ≈ 1.0)
        //    Input: a dictionary of Metal → liters (e.g., 3, 2, 0)
        //    Output: Metal → proportion (0..1)
        // --------------------------------------------------------
        public static Dictionary<Metal, float> Normalize(Dictionary<Metal, float> liters)
        {
            var result = new Dictionary<Metal, float>();
            float total = 0f;

            if (liters == null) return result;

            foreach (var kv in liters)
            {
                if (kv.Key == null) continue;
                if (kv.Value <= 0f) continue;
                total += kv.Value;
            }

            if (total <= 0f) return result;

            foreach (var kv in liters)
            {
                if (kv.Key == null) continue;
                if (kv.Value <= 0f) continue;
                result[kv.Key] = kv.Value / total;
            }

            return result;
        }

        // ----------------------------------------------------------------
        // 5) Combine stats by weighted average using normalized proportions
        // ----------------------------------------------------------------
        public static MetalStats CombineStats(Dictionary<Metal, float> normalized)
        {
            MetalStats sum = new MetalStats(); // defaults to 0s
            if (normalized == null) return sum;

            foreach (var kv in normalized)
            {
                Metal metal = kv.Key;
                float w = kv.Value; // 0..1
                sum = MetalStats.WeightedAdd(sum, metal.Stats, w);
            }

            // Clamp each field for safety
            sum.Workability    = Mathf.Clamp(sum.Workability,    0f, 100f);
            sum.Sharpenability = Mathf.Clamp(sum.Sharpenability, 0f, 100f);
            sum.Toughness      = Mathf.Clamp(sum.Toughness,      0f, 100f);
            sum.Density        = Mathf.Clamp(sum.Density,        0f, 100f);
            sum.Arcana         = Mathf.Clamp(sum.Arcana,         0f, 100f);
            return sum;
        }

        // ---------------------------------------------------------
        // 6) AutoName by rule:
        //    - If one metal ≥ 70%: "{Metal} Alloy"
        //    - Else if two metals ≥ 30%: "{A}-{B} Alloy"
        //    - Else: "Mixed Alloy"
        // ---------------------------------------------------------
        public static string AutoName(Dictionary<Metal, float> normalized)
        {
            if (normalized == null || normalized.Count == 0)
                return "Mixed Alloy";

            // Find top two proportions
            Metal topA = null, topB = null;
            float a = -1f, b = -1f;

            foreach (var kv in normalized)
            {
                float v = kv.Value;
                if (v > a)
                {
                    b = a; topB = topA;
                    a = v; topA = kv.Key;
                }
                else if (v > b)
                {
                    b = v; topB = kv.Key;
                }
            }

            if (topA != null && a >= 0.70f)
                return $"{topA.Name} Alloy";

            if (topA != null && topB != null && a >= 0.30f && b >= 0.30f)
                return $"{topA.Name}-{topB.Name} Alloy";

            return "Mixed Alloy";
        }

        // --------------------------------------------------------------
        // 7) One-shot pipeline:
        //    liters (Metal→amount) → normalize → combine stats → score →
        //    tier → name → Alloy instance
        // --------------------------------------------------------------
        public static Alloy MakeAlloy(Dictionary<Metal, float> inputLiters)
        {
            // 1) liters → proportions
            var normalized = Normalize(inputLiters);

            // 2) mix stats by proportions
            var stats = CombineStats(normalized);

            // 3) compute score and tier
            float score = ComputeScore(stats);
            QualityTier tier = ScoreToTier(score);

            // 4) auto-name per rule
            string name = AutoName(normalized);

            // 5) build result object
            return new Alloy(name, stats, score, tier, normalized);
        }
    }
}
