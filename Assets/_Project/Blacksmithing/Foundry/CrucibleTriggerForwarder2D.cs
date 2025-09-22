using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [RequireComponent(typeof(Collider2D))]
    public class CrucibleTriggerForwarder2D : MonoBehaviour
    {
        public FoundaryMB Parent;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Parent == null) return;

            var ore = other.GetComponent<OreMB>();
            if (ore == null) return;
            if (ore.AddedToCrucible) return;

            ore.InCrucibleZone = true;
            ore.PendingCrucible = Parent;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var ore = other.GetComponent<OreMB>();
            if (ore == null) return;
            if (ore.AddedToCrucible) return;

            ore.InCrucibleZone = false;
            ore.PendingCrucible = null;
        }
    }
}