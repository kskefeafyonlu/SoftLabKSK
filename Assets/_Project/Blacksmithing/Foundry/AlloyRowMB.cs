// AlloyRowMB.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Project.Blacksmithing.Foundry
{
    public class AlloyRowMB : MonoBehaviour
    {
        [Header("UI")]
        public TextMeshProUGUI metalNameText;
        public TextMeshProUGUI availableText;
        public TextMeshProUGUI currentText;
        public Button btnMinus;
        public Button btnPlus;
        public Image colorChip; // optional; safe if null

        public Metal Metal { get; private set; }
        public float AvailableLiters { get; private set; }
        public float CurrentLiters { get; private set; }

        float _step;
        System.Action _onChanged;

        public void Init(Metal metal, float availableLiters, float stepLiters, System.Action onChanged)
        {
            Metal = metal;
            AvailableLiters = Mathf.Max(0f, availableLiters);
            _step = Mathf.Max(0.0001f, stepLiters);
            _onChanged = onChanged;

            if (metalNameText) metalNameText.text = metal != null ? metal.Name : "—";
            if (availableText) availableText.text = $"{AvailableLiters:0.0} L";
            if (colorChip) colorChip.color = (metal != null) ? metal.BaseColor : Color.white;

            SetCurrentLiters(0f, invoke:false);

            if (btnMinus) btnMinus.onClick.AddListener(Dec);
            if (btnPlus)  btnPlus.onClick.AddListener(Inc);
        }

        public void SetCurrentLiters(float v, bool invoke = true)
        {
            CurrentLiters = Mathf.Clamp(v, 0f, AvailableLiters);
            if (currentText) currentText.text = $"{CurrentLiters:0.0} L";
            if (invoke) _onChanged?.Invoke();
        }

        void Inc()
        {
            float v = CurrentLiters + _step;
            if (v > AvailableLiters + 1e-6f) v = AvailableLiters;
            SetCurrentLiters(v);
        }

        void Dec()
        {
            float v = CurrentLiters - _step;
            if (v < 0f) v = 0f;
            SetCurrentLiters(v);
        }
    }
}
