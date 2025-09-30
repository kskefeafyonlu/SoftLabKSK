using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace _Project.CardSystem
{
    [DisallowMultipleComponent]
    public sealed class CardMB : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Refs")]
        public RectTransform VisualRoot; // sadece bu hareket/scale olur
        public Image RaycastPad;         // sabit hitbox (raycastTarget = true)
        public Image Background;
        public TMP_Text NameLabel;

        [Header("Hover")]
        public float hoverLift = 40f;
        public float hoverScale = 1.08f;
        public float pressLift = 8f;

        [Header("Anim")]
        public float moveLerp = 16f;
        public float scaleLerp = 16f;

        [Header("Sorting on Hover")]
        public bool liftSortingOnHover = true;
        public int  hoverSortingBoost = 500;

        RectTransform rt;
        Canvas visualCanvas;
        bool hovered, pressed;
        int baseSorting;
        Vector2 neighborOffset; // komşu açma için local offset

        public RectTransform Rt => rt ??= (RectTransform)transform;
        public bool IsHoveredOrPressed => hovered || pressed;

        void Awake()
        {
            if (!VisualRoot) VisualRoot = (RectTransform)transform;
            if (Background) Background.raycastTarget = false;
            if (NameLabel)  NameLabel.raycastTarget  = false;
            if (RaycastPad) RaycastPad.raycastTarget = true;

            visualCanvas = VisualRoot.GetComponent<Canvas>();
            if (!visualCanvas) visualCanvas = VisualRoot.gameObject.AddComponent<Canvas>();
            visualCanvas.overrideSorting = true;

            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas)
            {
                visualCanvas.sortingLayerID = parentCanvas.sortingLayerID;
                visualCanvas.additionalShaderChannels = parentCanvas.additionalShaderChannels;
            }

            var gr = VisualRoot.GetComponent<GraphicRaycaster>();
            if (gr) gr.enabled = false; // sadece görsel, raycast yok
        }

        public void Bind(CardData d)
        {
            if (Background) Background.color = d.color;
            if (NameLabel)  NameLabel.text  = d.name;
        }

        public void SetBaseSorting(int order)
        {
            baseSorting = order;
            ApplySorting();
        }

        public void SetNeighborOffset(Vector2 localOffset)
        {
            neighborOffset = localOffset;
        }

        void Update()
        {
            float lift = hovered ? hoverLift : 0f;
            if (pressed) lift += pressLift;

            var targetPos = new Vector3(neighborOffset.x, lift + neighborOffset.y, 0f);
            var targetScl = Vector3.one * (hovered ? hoverScale : 1f);

            if (VisualRoot)
            {
                VisualRoot.localPosition = Vector3.Lerp(
                    VisualRoot.localPosition,
                    targetPos,
                    1f - Mathf.Exp(-moveLerp * Time.unscaledDeltaTime)
                );
                VisualRoot.localScale = Vector3.Lerp(
                    VisualRoot.localScale,
                    targetScl,
                    1f - Mathf.Exp(-scaleLerp * Time.unscaledDeltaTime)
                );
            }
        }

        void ApplySorting()
        {
            if (!visualCanvas) return;
            int boost = (liftSortingOnHover && (hovered || pressed)) ? hoverSortingBoost : 0;
            visualCanvas.sortingOrder = baseSorting + boost;
        }

        // --- Pointer Events ---
        public void OnPointerEnter(PointerEventData _) { hovered = true;  ApplySorting(); }
        public void OnPointerExit (PointerEventData _) { hovered = false; pressed = false; ApplySorting(); }
        public void OnPointerDown (PointerEventData _) { pressed = true;  ApplySorting(); }
        public void OnPointerUp   (PointerEventData _) { pressed = false; ApplySorting(); }
    }
}
