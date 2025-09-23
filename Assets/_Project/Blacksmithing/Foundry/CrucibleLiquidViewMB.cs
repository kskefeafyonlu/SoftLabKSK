// CrucibleLiquidViewMB.cs — stack height scales by meltedLiters / CapacityLiters
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

        [Header("Layout (local space)")]
        public Transform areaBottomLeft;
        public Transform areaTopRight;

        [Tooltip("Sprite for each liquid band. If null, a 1x1 white sprite is generated (PPU=1).")]
        public Sprite liquidSprite;

        [Header("Rendering")]
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;
        [Range(0f, 1f)] public float bandAlpha = 0.9f;

        [Header("Behavior")]
        [Range(0.01f, 0.5f)] public float updateInterval = 0.08f;
        [Range(0.05f, 1f)]   public float surfaceLerp = 0.35f;

        [Header("Visual Scaling (optional)")]
        public bool useFixedVisualHeight = false;
        [Tooltip("Overrides marker distance if enabled (world units).")]
        public float fixedVisualHeight = 1.5f;
        [Tooltip("Ensures very small bands remain visible (world units). Re-normalized within the filled height.")]
        [Range(0f, 0.1f)] public float minBandHeight = 0.01f;

        [Header("Mask (optional)")]
        [Tooltip("If true and a SpriteMask exists in parents, bands will be VisibleInsideMask.")]
        public bool useParentSpriteMask = true;

        [Header("VFX (optional)")]
        public ParticleSystem surfaceBubbles;
        public bool enableBubblesWhenHot = true;

        [Header("Debug")]
        public bool debugLogs = false;
        public Color gizmoColor = new Color(0, 1, 1, 0.35f);

        class Band
        {
            public GameObject go;
            public SpriteRenderer sr;
            public Metal metal;
            public float targetHeight;   // target visual height in world units (within effectiveHeight)
            public float currentHeight;  // smoothed visual height
        }

        readonly List<Band> _pool = new();
        float _tAccum;
        float _prevHash = float.NaN;
        Sprite _fallbackSprite;
        bool _hasParentMask;

        void Reset()
        {
            if (areaBottomLeft == null || areaTopRight == null)
            {
                var bl = new GameObject("AreaBottomLeft").transform;
                var tr = new GameObject("AreaTopRight").transform;
                bl.SetParent(transform); tr.SetParent(transform);
                bl.localPosition = new Vector3(-0.5f, 0f, 0f);
                tr.localPosition = new Vector3( 0.5f, 1.0f, 0f);
                areaBottomLeft = bl; areaTopRight = tr;
            }
        }

        void OnEnable()
        {
            DetectParentMask();
            ForceRebuild();
        }

        void OnDisable()
        {
            SetBandsActive(false);
        }

        void OnValidate()
        {
            DetectParentMask();
            if (!Application.isPlaying) ForceRebuild();
        }

        void DetectParentMask()
        {
            _hasParentMask = useParentSpriteMask && GetComponentInParent<SpriteMask>() != null;
        }

        [ContextMenu("Rebuild Now")]
        public void ForceRebuild()
        {
            _prevHash = float.NaN;
            _tAccum = updateInterval;
            EnsureLiquidSprite();
            AnimateHeights();
        }

        void EnsureLiquidSprite()
        {
            if (liquidSprite != null) return;

            if (_fallbackSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.name = "[Generated] White1x1";
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f); // PPU=1
                _fallbackSprite.name = "[Generated] White1x1 (PPU=1)";
            }
            liquidSprite = _fallbackSprite;
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
            float totalMelted = 0f; foreach (var v in comp.Values) totalMelted += v;

            // change hash (composition + volumes)
            float hash = totalMelted;
            foreach (var kv in comp) hash += kv.Key.Stats.Density * 0.001f + kv.Value * 7.77f;

            // layout rect
            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float nativeW = Mathf.Abs(tr.x - bl.x);
            float nativeH = Mathf.Abs(tr.y - bl.y);
            float containerWidth  = Mathf.Max(0.0001f, nativeW);
            float containerHeight = useFixedVisualHeight ? Mathf.Max(0.0001f, fixedVisualHeight) : Mathf.Max(0.0001f, nativeH);
            float xLeft   = Mathf.Min(bl.x, tr.x);
            float yBottom = Mathf.Min(bl.y, tr.y);

            if (containerWidth <= 0.0001f || containerHeight <= 0.0001f)
            {
                if (debugLogs) Debug.LogWarning($"[CrucibleLiquidView] Area bounds are zero on {name}.", this);
                SetBandsActive(false);
                UpdateBubbles(0);
                return;
            }

            if (!float.IsNaN(_prevHash) && Mathf.Abs(hash - _prevHash) < 0.0001f)
            {
                AnimateHeights();
                UpdateBubbles(totalMelted);
                return;
            }
            _prevHash = hash;

            if (totalMelted <= 0f)
            {
                if (debugLogs && Application.isPlaying)
                    Debug.Log($"[CrucibleLiquidView] No melted liters yet on {name}.", this);
                SetBandsActive(false);
                UpdateBubbles(0);
                return;
            }

            // --- KEY CHANGE: only fill a fraction of the container based on capacity ---
            float cap = Mathf.Max(0.0001f, foundry.CapacityLiters);
            float fillFraction  = Mathf.Clamp01(totalMelted / cap);
            float effectiveHeight = containerHeight * fillFraction; // the stack’s total height

            EnsureLiquidSprite();
            EnsurePoolSize(comp.Count);

            // order by density (heaviest bottom)
            var ordered = comp.OrderByDescending(k => k.Key.Stats.Density).ToList();

            // provisional per-band heights within the effective height
            var rawHeights = new List<float>(ordered.Count);
            foreach (var kv in ordered)
            {
                float frac = Mathf.Clamp01(kv.Value / totalMelted); // relative proportions
                rawHeights.Add(effectiveHeight * frac);
            }

            // enforce min visible thickness then re-normalize to preserve effectiveHeight
            if (minBandHeight > 0f && rawHeights.Count > 0)
            {
                for (int i = 0; i < rawHeights.Count; i++)
                    if (rawHeights[i] > 0f) rawHeights[i] = Mathf.Max(rawHeights[i], minBandHeight);

                float sumCapped = 0f; foreach (var h in rawHeights) sumCapped += h;
                if (sumCapped > 0.0001f)
                {
                    float scaleBack = effectiveHeight / sumCapped;
                    for (int i = 0; i < rawHeights.Count; i++) rawHeights[i] *= scaleBack;
                }
            }

            // layout bands from the bottom up (leaving empty space above if not full)
            float yCursor = yBottom;
            int idx = 0;
            foreach (var kv in ordered)
            {
                float bandHeight = rawHeights[idx];
                var band = _pool[idx++];
                band.metal         = kv.Key;
                band.targetHeight  = bandHeight;
                band.currentHeight = Mathf.Clamp(band.currentHeight, 0f, effectiveHeight);

                LayoutBandWorldSized(band, xLeft, yCursor, containerWidth, band.currentHeight);
                ApplyColorAndSort(band, kv.Key, idx - 1);

                yCursor += bandHeight;
                band.go.SetActive(true);
            }
            for (; idx < _pool.Count; idx++) _pool[idx].go.SetActive(false);

            UpdateBubbles(totalMelted);
        }

        void AnimateHeights()
        {
            if (!Application.isPlaying) return;
            if (areaBottomLeft == null || areaTopRight == null || foundry == null) return;

            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float nativeW = Mathf.Abs(tr.x - bl.x);
            float nativeH = Mathf.Abs(tr.y - bl.y);
            float containerWidth  = Mathf.Max(0.0001f, nativeW);
            float containerHeight = useFixedVisualHeight ? Mathf.Max(0.0001f, fixedVisualHeight) : Mathf.Max(0.0001f, nativeH);
            float xLeft   = Mathf.Min(bl.x, tr.x);
            float yBottom = Mathf.Min(bl.y, tr.y);

            // recompute effective height for smooth animation too
            float totalMelted = 0f; foreach (var v in foundry.GetMeltedComposition().Values) totalMelted += v;
            float cap = Mathf.Max(0.0001f, foundry.CapacityLiters);
            float fillFraction  = Mathf.Clamp01(totalMelted / cap);
            float effectiveHeight = containerHeight * fillFraction;

            float yCursor = yBottom;
            foreach (var band in _pool)
            {
                if (!band.go.activeSelf) continue;
                band.currentHeight = Mathf.Lerp(band.currentHeight, band.targetHeight, surfaceLerp);
                band.currentHeight = Mathf.Clamp(band.currentHeight, 0f, effectiveHeight);
                LayoutBandWorldSized(band, xLeft, yCursor, containerWidth, band.currentHeight);
                yCursor += band.currentHeight;
            }
        }

        // Size bands in world units regardless of sprite PPU
        void LayoutBandWorldSized(Band band, float xLeft, float yBottom, float width, float height)
        {
            var t = band.go.transform;
            t.localPosition = new Vector3(xLeft + width * 0.5f, yBottom + height * 0.5f, 0f);

            // Reset scale first
            t.localScale = Vector3.one;

            Sprite s = band.sr.sprite != null ? band.sr.sprite : liquidSprite;
            if (s == null)
            {
                t.localScale = new Vector3(width, Mathf.Max(0.0001f, height), 1f);
                return;
            }

            Vector2 spriteWorldSize = s.bounds.size; // world units for scale=1
            float sx = (spriteWorldSize.x <= 0.000001f) ? 1f : width  / spriteWorldSize.x;
            float sy = (spriteWorldSize.y <= 0.000001f) ? 1f : height / spriteWorldSize.y;
            t.localScale = new Vector3(sx, sy, 1f);
        }

        void ApplyColorAndSort(Band band, Metal metal, int index)
        {
            if (!band.sr) return;

            var c = metal != null ? (Color)metal.BaseColor : Color.white;
            c.a = bandAlpha;
            band.sr.color = c;

            band.sr.sprite = liquidSprite;
            band.sr.sortingLayerName = sortingLayerName;
            band.sr.sortingOrder = sortingOrderBase + index;

            band.sr.maskInteraction = _hasParentMask
                ? SpriteMaskInteraction.VisibleInsideMask
                : SpriteMaskInteraction.None;
        }

        void EnsurePoolSize(int need)
        {
            while (_pool.Count < need)
            {
                var go = new GameObject("LiquidBand");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.drawMode = SpriteDrawMode.Simple; // scale via transform
                sr.sprite = liquidSprite ? liquidSprite : _fallbackSprite;

                _pool.Add(new Band { go = go, sr = sr, targetHeight = 0f, currentHeight = 0f });
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
            float containerHeight = useFixedVisualHeight
                ? Mathf.Max(0.0001f, fixedVisualHeight)
                : Mathf.Max(0.0001f, Mathf.Abs(tr.y - bl.y));
            float yBottom = Mathf.Min(bl.y, tr.y);

            float cap = Mathf.Max(0.0001f, foundry.CapacityLiters);
            float fillFraction = Mathf.Clamp01(totalMelted / cap);
            float surfaceY = yBottom + containerHeight * fillFraction;

            var psT = surfaceBubbles.transform;
            var pos = psT.localPosition; pos.y = surfaceY;
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

        void OnDrawGizmosSelected()
        {
            if (areaBottomLeft == null || areaTopRight == null) return;
            Gizmos.color = gizmoColor;

            Vector3 bl = areaBottomLeft.position;
            Vector3 tr = areaTopRight.position;
            Vector3 br = new Vector3(tr.x, bl.y, bl.z);
            Vector3 tl = new Vector3(bl.x, tr.y, bl.z);

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}
