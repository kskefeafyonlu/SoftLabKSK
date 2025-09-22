using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Quests
{
    [Serializable]
    public struct MaterialDrop { public string id; public int qty; }

    [Serializable]
    public struct RewardBundle
    {
        public int gold;
        public int reputation;
        public List<MaterialDrop> materials;     // optional
        public List<string> techUnlockKeys;      // e.g., "quench.oil.tier2"
        public List<string> shopUpgradeKeys;     // e.g., "station.grind.slot2"
        public List<string> hiddenKeys;          // rare stuff
        public float earlyBonusMultiplier;       // e.g., 0.15f => +15% if early
    }
}