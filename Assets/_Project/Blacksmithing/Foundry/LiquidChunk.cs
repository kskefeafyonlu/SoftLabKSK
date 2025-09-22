using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    /// <summary>
    /// A simple record of melted metal sitting in the crucible (no GameObject).
    /// </summary>
    [System.Serializable]
    public class LiquidChunk
    {
        public Metal Metal;     // which metal this liquid is
        public float Liters;    // volume in liters
        public float AtTempC;   // crucible temp when it melted (optional info)
        public float TimeStamp; // Time.time when created (optional info)

        public LiquidChunk(Metal metal, float liters, float atTempC)
        {
            Metal = metal;
            Liters = liters;
            AtTempC = atTempC;
            TimeStamp = Time.time;
        }
    }
}