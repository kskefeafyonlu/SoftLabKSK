using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    public static class OreFactory
    {
        /// <summary>
        /// Spawns an ore prefab with the specified attributes.
        /// - Assigns MetalId, Liters, and base color (from Metals registry).
        /// - Optionally parents under a transform.
        /// - Returns the created OreMB.
        /// </summary>
        public static OreMB CreateOre(GameObject orePrefab, MetalId metalId, float liters, Vector3 worldPos, Transform parent = null)
        {
            if (orePrefab == null)
            {
                Debug.LogError("[OreFactory] orePrefab is null.");
                return null;
            }

            var go = Object.Instantiate(orePrefab, worldPos, Quaternion.identity, parent);
            var ore = go.GetComponent<OreMB>();
            if (ore == null)
            {
                Debug.LogError("[OreFactory] Prefab does not contain OreMB.");
                Object.Destroy(go);
                return null;
            }

            ore.MetalId = metalId;
            ore.Liters = Mathf.Max(0f, liters);

            // Ensure visuals match the metal’s base color (no heat tint logic yet)
            var sr = go.GetComponent<SpriteRenderer>();
            var metal = MetalsUtil.FromId(metalId);
            if (sr != null && metal != null)
                sr.color = metal.BaseColor;

            // Ensure there’s a Rigidbody2D and Collider2D for physics
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f; // no gravity for crucible workspace
            rb.linearDamping = 2f;
            rb.angularDamping = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (go.GetComponent<Collider2D>() == null)
            {
                var cc = go.AddComponent<CircleCollider2D>();
                cc.radius = 0.2f;

                // OLD (deprecated):
                // cc.usedByComposite = false;

                // NEW:
                cc.compositeOperation = Collider2D.CompositeOperation.None;
            }


            return ore;
        }
    }
}
