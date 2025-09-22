using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Quests
{
    [RequireComponent(typeof(RectTransform))]
    public class SideQuestUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform panel;     // The sliding panel (width ~400)
        [SerializeField] private Button toggleButton;     // Button lives inside the panel

        [Header("Slide Settings")]
        [SerializeField] private float expandedX = 0f;        // Visible anchored X
        [SerializeField] private float collapsedX = 400f;     // Off-screen to the right (panel width)
        [SerializeField] private float animDuration = 0.25f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool startOpen = true;

        private bool _isOpen;
        private Coroutine _animCR;

        void Reset()
        {
            panel = GetComponent<RectTransform>();
            toggleButton = GetComponentInChildren<Button>();
        }

        void Awake()
        {
            if (panel == null) panel = GetComponent<RectTransform>();
            _isOpen = startOpen;
            SetXImmediate(_isOpen ? expandedX : collapsedX);
            if (toggleButton) toggleButton.onClick.AddListener(Toggle);
        }

        public void Toggle() => SetOpen(!_isOpen);
        public void Open()   => SetOpen(true);
        public void Close()  => SetOpen(false);

        public void SetOpen(bool open)
        {
            if (_isOpen == open && _animCR == null) return;
            _isOpen = open;
            if (_animCR != null) StopCoroutine(_animCR);
            _animCR = StartCoroutine(AnimateX(open ? expandedX : collapsedX));
        }

        private IEnumerator AnimateX(float targetX)
        {
            float start = panel.anchoredPosition.x;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / animDuration;
                float k = curve.Evaluate(Mathf.Clamp01(t));
                SetXImmediate(Mathf.Lerp(start, targetX, k));
                yield return null;
            }
            SetXImmediate(targetX);
            _animCR = null;
        }

        private void SetXImmediate(float x)
        {
            var pos = panel.anchoredPosition;
            pos.x = x;
            panel.anchoredPosition = pos;
        }
    }
}
