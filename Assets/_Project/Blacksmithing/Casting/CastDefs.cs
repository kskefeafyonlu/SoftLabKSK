using System.Collections.Generic;

namespace _Project.Blacksmithing.Casting
{
    public static class CastDefs
    {
        public struct CastDef
        {
            public string DisplayName;
            public float VolumeL;
            public string IconSpriteName;

            public CastDef(string name, float volumeL, string iconName)
            {
                DisplayName = name;
                VolumeL = volumeL;
                IconSpriteName = iconName;
            }
        }

        // Sprite names follow the convention in your atlas:
        // cast_ingot, cast_blade_medium, cast_guard, cast_pommel
        private static readonly Dictionary<CastId, CastDef> _defs = new()
        {
            { CastId.Ingot,       new CastDef("Ingot",         1.0f, "cast_ingot") },
            { CastId.BladeMedium, new CastDef("Medium Blade",   1.6f, "cast_blade_medium") },
            { CastId.Guard,       new CastDef("Guard",          0.4f, "cast_guard") },
            { CastId.Pommel,      new CastDef("Pommel",         0.3f, "cast_pommel") },
        };

        public static CastDef Get(CastId id) => _defs[id];

        public static IReadOnlyDictionary<CastId, CastDef> All => _defs;
    }
}