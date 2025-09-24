using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.Blacksmithing.Foundry
{
    /// <summary>
    /// Draggable ore. Can be committed to a FoundaryMB and later picked back up.
    /// When removed, it stores the crucible entry's thermal state and cools toward room temp.
    /// </summary>
    public class OreMB : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Identity")]
        public MetalId MetalId;
        public float Liters = 1f;

        // Bound CrucibleEntry identifier (stable link)
        [HideInInspector] public string BoundEntryGuid;

        [Header("Drag Settings")]
        public Camera DragCamera;
        public bool LockZ = true;

        [Header("State (runtime)")]
        [HideInInspector] public bool AddedToCrucible = false;
        [HideInInspector] public bool InCrucibleZone = false;
        [HideInInspector] public FoundaryMB PendingCrucible = null;
        [HideInInspector] public FoundaryMB CurrentCrucible = null; // set when committed

        // Saved thermal while outside
        [Header("Saved Thermal (outside)")]
        [HideInInspector] public bool HasSavedThermalState = false;
        [HideInInspector] public float SavedTempC = 20f;
        [HideInInspector] public float SavedMeltProgressSeconds = 0f;
        [HideInInspector] public float SavedSolidifyProgressSeconds = 0f;

        [Tooltip("Ambient room temperature when outside any crucible.")]
        public float AmbientTempCOutside = 20f;

        [Tooltip("Cooling approach rate toward ambient when outside (per second).")]
        public float CoolingRateBase = 0.15f;

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

            _sr = GetComponent<SpriteRenderer>();
            var metal = MetalsUtil.FromId(MetalId);
            _baseColor = (metal != null) ? metal.BaseColor : Color.white;
            if (_sr != null) _sr.color = _baseColor;
        }

        private void Update()
        {
            // If outside a crucible and we have a saved temperature, cool toward ambient
            if (CurrentCrucible == null && HasSavedThermalState)
            {
                float delta = AmbientTempCOutside - SavedTempC;
                SavedTempC += CoolingRateBase * delta * Time.deltaTime;
            }
        }

        // -------- Thermal state API (used by FoundaryMB) --------
        public void SaveThermalState(float tempC, float meltProg, float solidifyProg)
        {
            HasSavedThermalState = true;
            SavedTempC = tempC;
            SavedMeltProgressSeconds = meltProg;
            SavedSolidifyProgressSeconds = solidifyProg;
        }

        public bool ConsumeThermalState(out float tempC, out float meltProg, out float solidifyProg)
        {
            if (!HasSavedThermalState)
            {
                tempC = AmbientTempCOutside;
                meltProg = 0f;
                solidifyProg = 0f;
                return false;
            }

            tempC = SavedTempC;
            meltProg = SavedMeltProgressSeconds;
            solidifyProg = SavedSolidifyProgressSeconds;

            HasSavedThermalState = false;
            return true;
        }

        // ---------------- Drag interface ----------------
        public void OnBeginDrag(PointerEventData eventData)
        {
            // If currently owned by a crucible, uncommit first (no pickup if already melted)
            if (AddedToCrucible && CurrentCrucible != null)
            {
                if (!CurrentCrucible.TryUncommitOre(this))
                {
                    // Failsafe: if for any reason it couldn't uncommit, bail out of drag
                    return;
                }
            }

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
            Vector3 worldUnderCursor = ScreenToWorld(eventData.position);
            Vector3 target = worldUnderCursor + _grabOffset;
            if (LockZ) target.z = _initialZ;
            transform.position = target;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // If released inside a crucible trigger, attempt commit
            if (InCrucibleZone && PendingCrucible != null)
            {
                bool ok = PendingCrucible.CommitOreAtAutoPosition(this);
                if (ok)
                {
                    CurrentCrucible = PendingCrucible;
                    return; // crucible owns us now
                }
            }

            // Otherwise, outside → restore free physics (Dynamic)
            if (_hadRigidbody2D)
            {
                _rb2D.bodyType = RigidbodyType2D.Dynamic;
                _rb2D.gravityScale = 0f;
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
