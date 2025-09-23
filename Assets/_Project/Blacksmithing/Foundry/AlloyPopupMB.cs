// AlloyPopupMB.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Project.Blacksmithing.Foundry
{
    public class AlloyPopupMB : MonoBehaviour
    {
        [Header("Refs")]
        public FoundaryMB foundry;
        public GameObject panelRoot;
        public Transform rowsParent;
        public AlloyRowMB rowPrefab;

        [Header("Controls")]
        public Button btnClose;
        public Button btnPour;
        public Button btnAutoBalance;
        public Button btnClear;

        [Header("Preview")]
        public TextMeshProUGUI totalLitersText;
        public TextMeshProUGUI previewNameText;
        public TextMeshProUGUI previewTierText;
        public TextMeshProUGUI previewStatsText;

        const float TargetLiters = 1.0f;
        const float Epsilon = 0.0005f;

        readonly Dictionary<Metal, AlloyRowMB> _rows = new();

        void Awake()
        {
            if (btnClose) btnClose.onClick.AddListener(Close);
            if (btnPour) btnPour.onClick.AddListener(OnPour);
            if (btnAutoBalance) btnAutoBalance.onClick.AddListener(OnAutoBalance);
            if (btnClear) btnClear.onClick.AddListener(OnClear);

            Hide();
        }

        public void Open()
        {
            if (!foundry) { Hide(); return; }

            BuildRows();
            panelRoot?.SetActive(true);
            UpdateTotalsAndPreview();
        }

        public void Hide() => panelRoot?.SetActive(false);
        void Close() => Hide();

        void BuildRows()
        {
            // Clear
            if (rowsParent)
            {
                for (int i = rowsParent.childCount - 1; i >= 0; i--)
                    Destroy(rowsParent.GetChild(i).gameObject);
            }
            _rows.Clear();

            var avail = foundry.GetMeltedAvailability(); // Metal→liters (melted only)
            if (avail == null || avail.Count == 0)
            {
                // No melted metals → disable pour/auto
                SetTotals(0f);
                if (btnPour) btnPour.interactable = false;
                if (btnAutoBalance) btnAutoBalance.interactable = false;
                return;
            }

            float step = Mathf.Max(0.0001f, foundry.UIStepLiters);

            // Sort by density (heavier first) for parity with future visual stacking
            foreach (var kv in avail.OrderByDescending(k => k.Key.Stats.Density))
            {
                var row = Instantiate(rowPrefab, rowsParent);
                row.Init(kv.Key, kv.Value, step, OnRowChanged);
                _rows[kv.Key] = row;
            }

            if (btnAutoBalance) btnAutoBalance.interactable = true;
        }

        void OnRowChanged() => UpdateTotalsAndPreview();

        void UpdateTotalsAndPreview()
        {
            float step = Mathf.Max(0.0001f, foundry ? foundry.UIStepLiters : 0.1f);

            // Gather selection
            var selLiters = new Dictionary<Metal, float>();
            float total = 0f;

            foreach (var kv in _rows)
            {
                float v = Snap(step, kv.Value.CurrentLiters);
                if (v <= 0f) continue;
                selLiters[kv.Key] = v;
                total += v;
            }

            SetTotals(total);

            // Enable pour only at exactly 1.0 L
            if (btnPour) btnPour.interactable = Mathf.Abs(total - TargetLiters) <= Epsilon;

            // Live preview
            if (selLiters.Count > 0)
            {
                var preview = AlloyMath.MakeAlloy(selLiters);
                if (previewNameText) previewNameText.text = string.IsNullOrEmpty(preview.Name) ? "—" : preview.Name;
                if (previewTierText) previewTierText.text = $"Tier: {preview.Tier}";
                if (previewStatsText) previewStatsText.text =
                    $"W {preview.Stats.Workability:0.#} | T {preview.Stats.Toughness:0.#} | S {preview.Stats.Sharpenability:0.#} | D {preview.Stats.Density:0.###} | A {preview.Stats.Arcana:0.#}";
            }
            else
            {
                if (previewNameText) previewNameText.text = "—";
                if (previewTierText) previewTierText.text = "—";
                if (previewStatsText) previewStatsText.text = "—";
            }
        }

        void SetTotals(float total)
        {
            if (totalLitersText) totalLitersText.text = $"Total: {total:0.0} / {TargetLiters:0.0} L";
        }

        void OnPour()
        {
            // Build selection again to be authoritative
            float step = Mathf.Max(0.0001f, foundry ? foundry.UIStepLiters : 0.1f);
            var selection = new Dictionary<Metal, float>();
            float sum = 0f;

            foreach (var kv in _rows)
            {
                float v = Snap(step, kv.Value.CurrentLiters);
                if (v <= 0f) continue;
                selection[kv.Key] = v;
                sum += v;
            }

            if (Mathf.Abs(sum - TargetLiters) > Epsilon) return;
            if (!foundry) return;

            bool ok = foundry.PourExactlyOneLiter(selection);
            if (ok)
            {
                Close();
            }
            else
            {
                // If race condition (availability changed), rebuild UI
                BuildRows();
                UpdateTotalsAndPreview();
            }
        }

        void OnAutoBalance()
        {
            if (!foundry) return;

            float step = Mathf.Max(0.0001f, foundry.UIStepLiters);
            var request = new Dictionary<Metal, float>();
            foreach (var kv in _rows)
            {
                float v = Snap(step, kv.Value.CurrentLiters);
                if (v > 0f) request[kv.Key] = v;
            }

            var balanced = foundry.TryBalanceToOneLiter(request);
            foreach (var kv in _rows)
            {
                balanced.TryGetValue(kv.Key, out float v);
                // Clamp locally to the row’s availability and snap
                v = Mathf.Min(v, kv.Value.AvailableLiters);
                kv.Value.SetCurrentLiters(Snap(step, v), invoke:false);
            }
            UpdateTotalsAndPreview();
        }

        void OnClear()
        {
            foreach (var r in _rows.Values)
                r.SetCurrentLiters(0f, invoke:false);
            UpdateTotalsAndPreview();
        }

        static float Snap(float step, float v) => (step <= 0f) ? v : Mathf.Round(v / step) * step;
    }
}
