using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    /// <summary>
    /// Scales a bottom-aligned SpriteRenderer to show a simple "liquid fill".
    /// Color is blended from melted metals' BaseColors.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LiquidFillVisualMB : MonoBehaviour
    {
        [Header("References")]
        public FoundaryMB Crucible;            // assign your FoundaryMB
        public Transform BottomAnchor;         // optional: where the liquid starts (usually your PlacementOrigin). If null, uses own transform.

        [Header("Sizing")]
        public float MaxFillHeight = 1.0f;     // how tall the fill is at 100%
        public bool SpritePivotIsBottom = true;// set false if your sprite pivot is centered

        private SpriteRenderer _sr;
        private Vector3 _baseLocalPos;         // initial local position of this visual
        private Vector3 _baseLocalScale;       // initial local scale (X/Z kept as-is)

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseLocalPos = transform.localPosition;
            _baseLocalScale = transform.localScale;

            if (Crucible == null)
            {
                Crucible = GetComponentInParent<FoundaryMB>();
            }
        }

        private void LateUpdate()
        {
            if (Crucible == null || _sr == null) return;

            // 1) Fill amount
            float fill01 = Crucible.GetLiquidFill01();
            float targetHeight = Mathf.Clamp01(fill01) * MaxFillHeight;

            // 2) Scale sprite vertically to represent fill
            Vector3 s = _baseLocalScale;
            s.y = Mathf.Max(0.0001f, targetHeight); // avoid zero scale glitches
            transform.localScale = s;

            // 3) Keep bottom anchored at BottomAnchor or at current local position
            if (!SpritePivotIsBottom)
            {
                // If the pivot is center, push the sprite up by half the new height
                Vector3 pos = (BottomAnchor != null ? transform.parent.InverseTransformPoint(BottomAnchor.position) : _baseLocalPos);
                pos.y = (BottomAnchor != null ? pos.y : _baseLocalPos.y) + (s.y * 0.5f);
                transform.localPosition = pos;
            }
            else
            {
                // Pivot already at bottom → just snap to BottomAnchor if provided
                if (BottomAnchor != null)
                    transform.position = BottomAnchor.position;
                else
                    transform.localPosition = _baseLocalPos;
            }

            // 4) Color blend
            Color mix = Crucible.GetLiquidMixedColor();

            // If empty, fade out. If not, fully opaque (or tweak alpha here if you want translucency).
            if (fill01 <= 0.0001f) mix.a = 0f; else mix.a = 1f;

            _sr.color = mix;
        }
    }
}
