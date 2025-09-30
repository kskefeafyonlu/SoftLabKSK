using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// NEW: casting registry/icons (kept SO-free, atlas-backed)
using _Project.Blacksmithing.Casting;

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

        // NEW: Mold picker UI
        [Header("Mold Picker (Top Scroll Row)")]
        [Tooltip("Content transform inside your horizontal ScrollView (Grid/HorizontalLayout ok).")]
        public Transform moldButtonsParent;
        [Tooltip("Prefab with Button + Image (icon) + TextMeshProUGUI (label).")]
        public Button moldButtonPrefab;

        // Runtime state
        private readonly Dictionary<Metal, AlloyRowMB> _rows = new();
        private readonly Dictionary<CastId, Button> _moldButtons = new();

        // Selected mold (default = Ingot to preserve old 1.0 L behavior)
        private CastId _currentMold = CastId.Ingot;

        // Constants
        const float Epsilon = 0.0005f;

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

            // Build mold row (icons + labels)
            BuildMoldButtons();

            // Build metal rows
            BuildRows();

            panelRoot?.SetActive(true);
            UpdateTotalsAndPreview();
        }

        public void Hide() => panelRoot?.SetActive(false);
        void Close() => Hide();

        // -----------------------------
        // Mold Picker (top scroll row)
        // -----------------------------
        void BuildMoldButtons()
        {
            if (moldButtonsParent == null || moldButtonPrefab == null)
                return;

            // Clear old
            for (int i = moldButtonsParent.childCount - 1; i >= 0; i--)
                Destroy(moldButtonsParent.GetChild(i).gameObject);
            _moldButtons.Clear();

            // Build from hardcoded defs
            foreach (var kv in CastDefs.All)
            {
                var id = kv.Key;
                var def = kv.Value;

                var btn = Instantiate(moldButtonPrefab, moldButtonsParent);
                btn.name = $"MoldButton_{id}";

                // Icon
                var img = btn.GetComponentInChildren<Image>();
                if (img != null) img.sprite = CastIconProvider.Get(id);

                // Label
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = def.DisplayName;

                btn.onClick.AddListener(() => OnSelectMold(id));

                _moldButtons[id] = btn;
            }

            // Ensure a valid default
            if (!_moldButtons.ContainsKey(_currentMold))
                _currentMold = CastId.Ingot;

            RefreshMoldSelectionVisuals();
        }

        void OnSelectMold(CastId id)
        {
            _currentMold = id;
            RefreshMoldSelectionVisuals();
            UpdateTotalsAndPreview(); // updates target liters + pour button state
        }

        void RefreshMoldSelectionVisuals()
        {
            foreach (var kv in _moldButtons)
            {
                bool selected = (kv.Key == _currentMold);
                var colors = kv.Value.colors;
                // Subtle highlight: boost selected normal color alpha/brightness a bit
                colors.normalColor = selected ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.75f);
                kv.Value.colors = colors;
            }
        }

        float GetTargetLitersForCurrentMold()
        {
            // Hardcoded registry value per mold (Ingot=1.0, Blade=1.6, etc.)
            return CastDefs.Get(_currentMold).VolumeL;
        }

        // -----------------------------
        // Metal rows (unchanged logic)
        // -----------------------------
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
                SetTotals(0f, GetTargetLitersForCurrentMold());
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
            float targetLiters = GetTargetLitersForCurrentMold();

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

            SetTotals(total, targetLiters);

            // Enable pour only when total == target
            bool hasExact = Mathf.Abs(total - targetLiters) <= Epsilon;

            // TEMP RULE: until non-ingot pouring is implemented, enable Pour only for Ingot mold
            bool allowPourThisStep = (_currentMold == CastId.Ingot);

            if (btnPour) btnPour.interactable = hasExact && allowPourThisStep;

            // Live preview of resulting alloy (independent of mold)
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

        void SetTotals(float total, float target)
        {
            if (totalLitersText) totalLitersText.text = $"Total: {total:0.0} / {target:0.0} L";
        }

        void OnPour()
        {
            // Build authoritative selection
            float step = Mathf.Max(0.0001f, foundry ? foundry.UIStepLiters : 0.1f);
            float target = GetTargetLitersForCurrentMold();

            var selection = new Dictionary<Metal, float>();
            float sum = 0f;

            foreach (var kv in _rows)
            {
                float v = Snap(step, kv.Value.CurrentLiters);
                if (v <= 0f) continue;
                selection[kv.Key] = v;
                sum += v;
            }

            if (!foundry) return;
            if (Mathf.Abs(sum - target) > Epsilon) return;

            // TEMP: only Ingot is wired to pour right now (1.0 L path).
            if (_currentMold != CastId.Ingot)
            {
                // Optional: insert your toast here, e.g., "That mold isn't implemented yet."
                Debug.Log("[AlloyPopup] Non-ingot molds will be enabled in the next step.");
                return;
            }

            // Uses the existing ingot pour API (drains exactly 1.0 L internally)
            var ingot = foundry.PourAndCreateIngot(selection);
            if (ingot != null)
            {
                Close();
            }
            else
            {
                // If availability changed mid-click, rebuild UI
                BuildRows();
                UpdateTotalsAndPreview();
            }
        }

        void OnAutoBalance()
        {
            if (!foundry) return;

            float step = Mathf.Max(0.0001f, foundry.UIStepLiters);
            float target = GetTargetLitersForCurrentMold();

            // Start from the user's current picks
            var request = new Dictionary<Metal, float>();
            foreach (var kv in _rows)
            {
                float v = Snap(step, kv.Value.CurrentLiters);
                if (v > 0f) request[kv.Key] = v;
            }

            // Reuse foundry's stepper to fill to 1.0 L; if target != 1.0, we’ll proportionally adjust below
            var balanced = foundry.TryBalanceToOneLiter(request);

            // If current mold isn't exactly 1.0 L, scale the result to the target volume
            float sum1 = balanced.Values.Sum();
            if (sum1 > Epsilon && Mathf.Abs(target - 1.0f) > Epsilon)
            {
                float scale = target / sum1;
                var keys = balanced.Keys.ToList();
                foreach (var m in keys) balanced[m] = balanced[m] * scale;
            }

            // Apply to rows with local clamping & snapping
            foreach (var kv in _rows)
            {
                balanced.TryGetValue(kv.Key, out float v);
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
