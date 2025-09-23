using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [System.Serializable]
    public class Metal
    {
        public string Name;
        public MetalStats Stats;
        public int MeltingPointC;
        public int BurnPointC;

        // Visual (kept as-is)
        public Color BaseColor;

        // Time to fully melt (at/above MeltingPointC)
        public float MeltTimeSeconds;

        // NEW: how quickly this metal heats up in the crucible (relative multiplier; 1.0 = normal)
        public float HeatSensitivity;

        public Metal(
            string name,
            MetalStats stats,
            int melt,
            int burn,
            float meltTimeSeconds,
            Color baseColor,
            float heatSensitivity // NEW
        )
        {
            Name = name;
            Stats = stats;
            MeltingPointC = melt;
            BurnPointC = burn;
            MeltTimeSeconds = meltTimeSeconds;
            BaseColor = baseColor;
            HeatSensitivity = heatSensitivity;
        }
    }

    public static class Metals
    {
        public static readonly Metal Iron = new Metal(
            "Iron",
            new MetalStats { Workability = 40, Sharpenability = 30, Toughness = 40, Density = 45, Arcana = 10 },
            melt: 600,
            burn: 900,
            meltTimeSeconds: 4.0f,
            baseColor: new Color32(120, 120, 130, 255),   // cool gray
            heatSensitivity: 1.00f
        );

        public static readonly Metal Copper = new Metal(
            "Copper",
            new MetalStats { Workability = 60, Sharpenability = 15, Toughness = 20, Density = 30, Arcana = 10 },
            melt: 500,
            burn: 800,
            meltTimeSeconds: 3.0f,
            baseColor: new Color32(184, 115, 51, 255),    // copper
            heatSensitivity: 1.20f
        );

        public static readonly Metal Silver = new Metal(
            "Silver",
            new MetalStats { Workability = 50, Sharpenability = 25, Toughness = 15, Density = 40, Arcana = 40 },
            melt: 500,
            burn: 850,
            meltTimeSeconds: 3.0f,
            baseColor: new Color32(200, 200, 210, 255),   // light silver
            heatSensitivity: 1.10f
        );

        public static readonly Metal Mithril = new Metal(
            "Mithril",
            new MetalStats { Workability = 50, Sharpenability = 40, Toughness = 50, Density = 25, Arcana = 55 },
            melt: 700,
            burn: 950,
            meltTimeSeconds: 5.0f,
            baseColor: new Color32(140, 200, 230, 255),   // pale blue-silver
            heatSensitivity: 0.80f
        );

        public static readonly Metal Adamantite = new Metal(
            "Adamantite",
            new MetalStats { Workability = 15, Sharpenability = 30, Toughness = 65, Density = 70, Arcana = 25 },
            melt: 800,
            burn: 1000,
            meltTimeSeconds: 6.0f,
            baseColor: new Color32(60, 50, 80, 255),      // dark violet-gray
            heatSensitivity: 0.70f
        );

        public static readonly Metal Gold = new Metal(
            "Gold",
            new MetalStats { Workability = 65, Sharpenability = 10, Toughness = 10, Density = 60, Arcana = 55 },
            melt: 500,
            burn: 850,
            meltTimeSeconds: 3.0f,
            baseColor: new Color32(212, 175, 55, 255),    // gold
            heatSensitivity: 1.00f
        );
    }

    public enum MetalId
    {
        Iron,
        Copper,
        Silver,
        Mithril,
        Adamantite,
        Gold
    }

    public static class MetalsUtil
    {
        public static Metal FromId(MetalId id)
        {
            switch (id)
            {
                case MetalId.Iron:       return Metals.Iron;
                case MetalId.Copper:     return Metals.Copper;
                case MetalId.Silver:     return Metals.Silver;
                case MetalId.Mithril:    return Metals.Mithril;
                case MetalId.Adamantite: return Metals.Adamantite;
                case MetalId.Gold:       return Metals.Gold;
                default:                 return Metals.Iron;
            }
        }
    }
}
