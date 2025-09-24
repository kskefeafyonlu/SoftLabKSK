using System.Collections.Generic;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    /// <summary>
    /// Extremely simple inventory sink.
    /// Accepts ingots by parenting them here and disabling physics; stores a list.
    /// </summary>
    [DisallowMultipleComponent]
    public class SimpleInventoryMB : MonoBehaviour, IInventorySink
    {
        public List<AlloyIngotMB> Items = new List<AlloyIngotMB>();

        public bool TryAcceptIngot(AlloyIngotMB ingot)
        {
            if (ingot == null) return false;

            // Parent under inventory
            ingot.transform.SetParent(transform, true);

            // Disable physics & collisions
            var rb = ingot.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Static; // parked in inventory
                rb.gravityScale = 0f;
            }

            var col = ingot.GetComponent<Collider2D>();
            if (col) col.enabled = false;

            if (!Items.Contains(ingot))
                Items.Add(ingot);

            return true;
        }

        /// <summary>
        /// Removes an ingot from the inventory and drops it to the world at the given position.
        /// </summary>
        public bool Drop(AlloyIngotMB ingot, Vector3 worldPos)
        {
            if (ingot == null) return false;
            int idx = Items.IndexOf(ingot);
            if (idx < 0) return false;

            Items.RemoveAt(idx);

            // Re-enable physics & collisions
            var col = ingot.GetComponent<Collider2D>();
            if (col) col.enabled = true;

            var rb = ingot.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.linearDamping = 2f;
                rb.angularDamping = 2f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            ingot.transform.SetParent(null, true);
            ingot.transform.position = worldPos;
            return true;
        }
    }
}
