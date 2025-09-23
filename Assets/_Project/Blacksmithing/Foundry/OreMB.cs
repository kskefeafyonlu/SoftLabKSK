using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.Blacksmithing.Foundry
{
    public class OreMB : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Identity")]
        public MetalId MetalId;
        public float Liters = 1f;

        [Header("Drag Settings")]
        public Camera DragCamera;
        public bool LockZ = true;

        [Header("State")]
        [HideInInspector] public bool AddedToCrucible = false;
        [HideInInspector] public bool InCrucibleZone = false;
        [HideInInspector] public FoundaryMB PendingCrucible = null;

        // Internals (2D physics)
        private Rigidbody2D _rb2D;
        private bool _hadRigidbody2D;
        private RigidbodyType2D _wasBodyType;
        private float _wasGravityScale;

        // Drag math
        private float _dragDepth;
        private float _initialZ;
        private Vector3 _grabOffset;

        private SpriteRenderer _sr;
        private Color _baseColor;

        private void Awake()
        {
            _rb2D = GetComponent<Rigidbody2D>();
            _hadRigidbody2D = (_rb2D != null);

            if (DragCamera == null)
                DragCamera = Camera.main;

            // Color from metal registry
            _sr = GetComponent<SpriteRenderer>();
            var metal = MetalsUtil.FromId(MetalId);
            _baseColor = (metal != null) ? metal.BaseColor : Color.white;
            if (_sr != null) _sr.color = _baseColor;
        }

        public Color GetBaseColor() => _baseColor;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (AddedToCrucible) return;

            if (_hadRigidbody2D)
            {
                // Store previous physics state
                _wasBodyType    = _rb2D.bodyType;
                _wasGravityScale = _rb2D.gravityScale;

                // Switch to kinematic for drag
                _rb2D.bodyType = RigidbodyType2D.Kinematic;
                _rb2D.gravityScale = 0f;
                _rb2D.linearVelocity = Vector2.zero;
                _rb2D.angularVelocity = 0f;
            }

            _initialZ = transform.position.z;
            _dragDepth = (DragCamera != null) ? DragCamera.WorldToScreenPoint(transform.position).z : 10f;

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

            // If released while inside a crucible trigger, ask that crucible to auto-place us.
            if (InCrucibleZone && PendingCrucible != null)
            {
                bool ok = PendingCrucible.CommitOreAtAutoPosition(this);
                if (ok)
                {
                    // Crucible took ownership (parented, positioned, AddedToCrucible=true).
                    // Do not restore physics; we're now owned by the crucible.
                    return;
                }
            }

            // Otherwise, restore physics if not committed.
            if (_hadRigidbody2D)
            {
                _rb2D.bodyType = _wasBodyType;
                _rb2D.gravityScale = _wasGravityScale;
            }
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (DragCamera == null)
                return transform.position;

            Vector3 sp = new Vector3(screenPos.x, screenPos.y, _dragDepth);
            return DragCamera.ScreenToWorldPoint(sp);
        }
    }
}
