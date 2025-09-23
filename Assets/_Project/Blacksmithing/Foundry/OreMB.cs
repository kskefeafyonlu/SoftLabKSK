using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.Blacksmithing.Foundry
{
    /// <summary>
    /// World ore that the player can drag and drop into a FoundaryMB.
    /// Position is preserved on commit; FoundaryMB handles per-ore heating & melting.
    /// </summary>
    public class OreMB : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Identity")]
        public MetalId MetalId;
        public float Liters = 1f;

        [Header("Drag Settings")]
        public Camera DragCamera;
        public bool LockZ = true;

        [Header("State (runtime)")]
        [HideInInspector] public bool AddedToCrucible = false;
        [HideInInspector] public bool InCrucibleZone = false;
        [HideInInspector] public FoundaryMB PendingCrucible = null;
        [HideInInspector] public FoundaryMB CurrentCrucible = null; // set when committed

        // Internals (2D physics)
        private Rigidbody2D _rb2D;
        private bool _hadRigidbody2D;
        private RigidbodyType2D _wasBodyType;
        private float _wasGravityScale;

        // Drag math
        private float _dragDepth;
        private float _initialZ;
        private Vector3 _grabOffset;

        // Visual identity only (no heat tint yet)
        private SpriteRenderer _sr;
        private Color _baseColor;

        private void Awake()
        {
            _rb2D = GetComponent<Rigidbody2D>();
            _hadRigidbody2D = (_rb2D != null);

            if (DragCamera == null)
                DragCamera = Camera.main;

            // Set visual to metal base color for identity (no heat/tint yet)
            _sr = GetComponent<SpriteRenderer>();
            var metal = MetalsUtil.FromId(MetalId);
            _baseColor = (metal != null) ? metal.BaseColor : Color.white;
            if (_sr != null) _sr.color = _baseColor;
        }

        public Color GetBaseColor() => _baseColor;

        // ----------------
        // Drag interface
        // ----------------
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (AddedToCrucible) return;

            if (_hadRigidbody2D)
            {
                _wasBodyType     = _rb2D.bodyType;
                _wasGravityScale = _rb2D.gravityScale;

                // Make it easy to drag
                _rb2D.bodyType = RigidbodyType2D.Kinematic;
                _rb2D.gravityScale = 0f;
                _rb2D.linearVelocity = Vector2.zero;
                _rb2D.angularVelocity = 0f;
            }

            _initialZ = transform.position.z;
            _dragDepth = (DragCamera != null)
                ? DragCamera.WorldToScreenPoint(transform.position).z
                : 10f;

            Vector3 worldUnderCursor = ScreenToWorld(eventData.position);
            _grabOffset = transform.position - worldUnderCursor;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (AddedToCrucible) return;

            Vector3 worldUnderCursor = ScreenToWorld(eventData.position);
            Vector3 target = worldUnderCursor + _grabOffset;
            if (LockZ) target.z = _initialZ;
            transform.position = target;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (AddedToCrucible) return;

            // If released inside a crucible trigger, ask that crucible to take ownership.
            if (InCrucibleZone && PendingCrucible != null)
            {
                bool ok = PendingCrucible.CommitOreAtAutoPosition(this);
                if (ok)
                {
                    // Crucible took ownership (parented, position preserved).
                    // FoundaryMB created a CrucibleEntry with CurrentTempC set to Ambient.
                    CurrentCrucible = PendingCrucible;
                    return; // do not restore physics; we now belong to the crucible
                }
            }

            // Otherwise, restore physics if not committed.
            if (_hadRigidbody2D)
            {
                _rb2D.bodyType = _wasBodyType;
                _rb2D.gravityScale = _wasGravityScale;
            }
        }

        // ----------------
        // Helpers
        // ----------------
        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (DragCamera == null)
                return transform.position;

            Vector3 sp = new Vector3(screenPos.x, screenPos.y, _dragDepth);
            return DragCamera.ScreenToWorldPoint(sp);
        }
    }
}
