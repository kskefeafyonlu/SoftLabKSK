using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [System.Serializable]
    public class CrucibleEntry
    {
        // Stable identity to bind OreMB <-> CrucibleEntry reliably
        public string Guid;

        // Data
        public Metal Metal;
        public float Liters;
        public GameObject Obj;

        // Thermal & phase state
        public float CurrentTempC;
        public float MeltProgressSeconds;
        public float SolidifyProgressSeconds;
        public bool IsMelted;

        public CrucibleEntry(Metal metal, float liters, GameObject obj = null)
        {
            Guid = System.Guid.NewGuid().ToString("N");

            Metal = metal;
            Liters = liters;
            Obj = obj;

            CurrentTempC = 20f; // set on commit by FoundaryMB
            MeltProgressSeconds = 0f;
            SolidifyProgressSeconds = 0f;
            IsMelted = false;
        }
    }
}