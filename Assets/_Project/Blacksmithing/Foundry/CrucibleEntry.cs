using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [System.Serializable]
    public class CrucibleEntry
    {
        public Metal Metal;       // which metal
        public float Liters;      // solid volume
        public GameObject Obj;    // the actual world object in the crucible

        // Melting state (very basic)
        public bool IsMelted = false;
        public float MeltProgressSeconds = 0f; // accumulated only while hot enough

        public CrucibleEntry(Metal metal, float liters, GameObject obj = null)
        {
            Metal = metal;
            Liters = liters;
            Obj = obj;
        }
    }
}