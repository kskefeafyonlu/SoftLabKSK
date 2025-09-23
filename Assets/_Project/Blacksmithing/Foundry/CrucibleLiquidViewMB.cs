using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [ExecuteAlways]
    public class CrucibleLiquidViewMB : MonoBehaviour
    {
        [Header("Data")]
        public FoundaryMB foundry;

        [Header("Layout (local space of this object)")]
        public Transform areaBottomLeft;
        public Transform areaTopRight;

        [Tooltip("Sprite used for each liquid band (1x1 white square works).")]
        public Sprite liquidSprite;

        [Tooltip("Sorting layer name for bands.")]
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;

        [Header("Behavior")]
        [Range(0.01f, 0.5f)] public float updateInterval = 0.08f;
        [Range(0.1f, 1f)]   public float surfaceLerp = 0.35f;
        [Range(0f, 1f)]     public float bandAlpha   = 0.9f;

        [Header("Optional VFX")]
        public ParticleSystem surfaceBubbles;
        public bool enableBubblesWhenHot = true;

        // --- internals ---
        class Band
        {
            public GameObject go;
            public SpriteRenderer sr;
            public Metal metal;
            public float targetHeight;
            public float currentHeight;
        }

        readonly List<Band> _pool = new();
        float _tAccum;
        float _prevHash = float.NaN; // change detector

        void Reset()
        {
            if (areaBottomLeft == null || areaTopRight == null)
            {
                var bl = new GameObject("AreaBottomLeft").transform;
                var tr = new GameObject("AreaTopRight").transform;
                bl.SetParent(transform); tr.SetParent(transform);
                bl.localPosition = new Vector3(-0.5f, 0f, 0f);
                tr.localPosition = new Vector3( 0.5f, 1f, 0f);
                areaBottomLeft = bl;
                areaTopRight   = tr;
            }
        }

        void OnEnable()
        {
            ForceRebuild();
        }

        void OnDisable()
        {
            SetBandsActive(false);
        }

        /// <summary>
        /// Public API: force the view to refresh next Update (editor or play mode).
        /// Call this after wiring refs or changing bounds.
        /// </summary>
        public void ForceRebuild()
        {
            _prevHash = float.NaN;     // guarantee mismatch
            _tAccum   = updateInterval; // trigger immediate update pass
            AnimateHeights();           // ensure graceful state in editor
        }

        void Update()
        {
            if (!foundry || areaBottomLeft == null || areaTopRight == null) return;

            _tAccum += Application.isPlaying ? Time.deltaTime : 0.1f;
            if (_tAccum < updateInterval && Application.isPlaying)
            {
                AnimateHeights();
                return;
            }
            _tAccum = 0f;

            var comp = foundry.GetMeltedComposition();
            float total = 0f; foreach (var v in comp.Values) total += v;

            // simple hash of composition to detect changes
            float hash = total;
            foreach (var kv in comp) hash += kv.Key.Stats.Density * 0.001f + kv.Value * 7.77f;

            if (!float.IsNaN(_prevHash) && Mathf.Abs(hash - _prevHash) < 0.0001f)
            {
                AnimateHeights();
                UpdateBubbles(total);
                return;
            }
            _prevHash = hash;

            // layout rect in local space
            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float width   = Mathf.Abs(tr.x - bl.x);
            float height  = Mathf.Max(0.0001f, Mathf.Abs(tr.y - bl.y));
            float xLeft   = Mathf.Min(bl.x, tr.x);
            float yBottom = Mathf.Min(bl.y, tr.y);

            EnsurePoolSize(comp.Count);

            // density-desc (heaviest bottom)
            var ordered = comp.OrderByDescending(k => k.Key.Stats.Density).ToList();

            float yCursor = yBottom;
            int i = 0;
            foreach (var kv in ordered)
            {
                float proportion  = (total <= 0f) ? 0f : Mathf.Clamp01(kv.Value / total);
                float bandHeight  = height * proportion;

                var band = _pool[i++];
                band.metal        = kv.Key;
                band.targetHeight = bandHeight;
                band.currentHeight = Mathf.Clamp(band.currentHeight, 0f, height);

                LayoutBand(band, xLeft, yCursor, width, band.currentHeight);
                ApplyColor(band, kv.Key);

                yCursor += bandHeight;
                band.go.SetActive(true);
            }
            for (; i < _pool.Count; i++) _pool[i].go.SetActive(false);

            UpdateBubbles(total);
        }

        void AnimateHeights()
        {
            if (!Application.isPlaying) return;

            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float width   = Mathf.Abs(tr.x - bl.x);
            float height  = Mathf.Max(0.0001f, Mathf.Abs(tr.y - bl.y));
            float xLeft   = Mathf.Min(bl.x, tr.x);
            float yBottom = Mathf.Min(bl.y, tr.y);

            float yCursor = yBottom;
            foreach (var band in _pool)
            {
                if (!band.go.activeSelf) continue;
                band.currentHeight = Mathf.Lerp(band.currentHeight, band.targetHeight, surfaceLerp);
                LayoutBand(band, xLeft, yCursor, width, band.currentHeight);
                yCursor += band.currentHeight;
            }
        }

        void LayoutBand(Band band, float xLeft, float yBottom, float width, float h)
        {
            var t = band.go.transform;
            t.localPosition = new Vector3(xLeft + width * 0.5f, yBottom + h * 0.5f, 0f);
            t.localScale    = new Vector3(width, h, 1f);
        }

        void ApplyColor(Band band, Metal metal)
        {
            if (!band.sr) return;
            var c = metal != null ? (Color)metal.BaseColor : Color.white;
            c.a = bandAlpha;
            band.sr.color = c;
            if (!string.IsNullOrEmpty(sortingLayerName))
                band.sr.sortingLayerName = sortingLayerName;
            band.sr.sortingOrder = sortingOrderBase;
        }

        void EnsurePoolSize(int need)
        {
            while (_pool.Count < need)
            {
                var go = new GameObject("LiquidBand");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = liquidSprite;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; // plays nice with Sprite Mask
                _pool.Add(new Band { go = go, sr = sr });
                go.SetActive(false);
            }
        }

        void SetBandsActive(bool state)
        {
            foreach (var b in _pool) if (b?.go) b.go.SetActive(state);
        }

        void UpdateBubbles(float totalMelted)
        {
            if (!surfaceBubbles) return;

            bool hot = foundry && foundry.CurrentTempC >= MinMeltPoint();
            bool should = enableBubblesWhenHot && hot && totalMelted > 0.0001f;

            var em = surfaceBubbles.emission;
            em.enabled = should;

            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float height  = Mathf.Max(0.0001f, Mathf.Abs(tr.y - bl.y));
            float yBottom = Mathf.Min(bl.y, tr.y);

            float cap = Mathf.Max(0.0001f, foundry.CapacityLiters);
            float totalH = Mathf.Clamp01(totalMelted / cap) * height;

            var psT = surfaceBubbles.transform;
            var pos = psT.localPosition; pos.y = yBottom + totalH;
            psT.localPosition = pos;
        }

        int MinMeltPoint()
        {
            int min = int.MaxValue;
            var comp = foundry.GetMeltedComposition();
            foreach (var kv in comp) if (kv.Key != null) min = Mathf.Min(min, kv.Key.MeltingPointC);
            return (min == int.MaxValue) ? foundry.MinTempC : min;
        }

        Vector3 ToLocal(Vector3 world) => transform.InverseTransformPoint(world);
    }
}
