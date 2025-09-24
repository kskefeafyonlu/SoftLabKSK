using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [System.Serializable]
    public class CrucibleEntry
    {
        public Metal Metal;
        public float Liters;

        // Optional link to the in-world ore object (disabled on melt)
        public GameObject Obj;

        // Heating + melting state
        public float CurrentTempC;
        public float MeltProgressSeconds;
        public float SolidifyProgressSeconds; // if you also want cooling back to solid
        public bool IsMelted;


        public CrucibleEntry(Metal metal, float liters, GameObject obj = null)
        {
            Metal = metal;
            Liters = liters;
            Obj = obj;
            CurrentTempC = 20f;     // will be overwritten by FoundaryMB on commit
            MeltProgressSeconds = 0f;
            IsMelted = false;
        }
    }
}