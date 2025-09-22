// Alloy.cs

using System.Collections.Generic;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    public class Alloy
    {
        // ---- Identity ----
        public string Name;                 // Auto-generated per naming rules

        // ---- Final stats (engine uses floats 0–100) ----
        public MetalStats Stats;            // Mixed, clamped 0..100

        // ---- Quality ----
        public float Score;                 // 0..100 weighted score (hidden from player)
        public QualityTier Tier;            // Label shown to player

        // ---- Composition ----
        // Each entry is a known Metal with its normalized proportion in [0..1].
        public Dictionary<Metal, float> Composition = new Dictionary<Metal, float>();

        // ---- UI helpers (rounded integers the player sees) ----
        public int WorkabilityInt    => Mathf.RoundToInt(Stats.Workability);
        public int SharpenabilityInt => Mathf.RoundToInt(Stats.Sharpenability);
        public int ToughnessInt      => Mathf.RoundToInt(Stats.Toughness);
        public int DensityInt        => Mathf.RoundToInt(Stats.Density);
        public int ArcanaInt         => Mathf.RoundToInt(Stats.Arcana);

        // ---- Constructors (kept simple) ----
        public Alloy() { } // empty if you want to fill fields later

        public Alloy(string name, MetalStats stats, float score, QualityTier tier,
            Dictionary<Metal, float> composition)
        {
            Name = name;
            Stats = stats;
            Score = score;
            Tier = tier;
            Composition = composition;
        }
    }
}