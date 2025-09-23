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

        [Tooltip("Sprite used for each liquid band. If null, a 1x1 white sprite is generated.")]
        public Sprite liquidSprite;

        [Header("Rendering")]
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;
        [Range(0f, 1f)] public float bandAlpha = 0.9f;

        [Header("Behavior")]
        [Range(0.01f, 0.5f)] public float updateInterval = 0.08f;
        [Range(0.05f, 1f)]   public float surfaceLerp = 0.35f;

        [Header("Mask (optional)")]
        [Tooltip("If true and a SpriteMask exists in parents, bands will be VisibleInsideMask.")]
        public bool useParentSpriteMask = true;

        [Header("VFX (optional)")]
        public ParticleSystem surfaceBubbles;
        public bool enableBubblesWhenHot = true;

        [Header("Debug")]
        public bool debugLogs = true;
        public Color gizmoColor = new Color(0, 1, 1, 0.35f);

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
            // keep rendering sane even in editor
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
            AnimateHeights(); // harmless in editor
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
                _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                _fallbackSprite.name = "[Generated] White1x1";
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
            float total = 0f; foreach (var v in comp.Values) total += v;

            // quick-change hash
            float hash = total;
            foreach (var kv in comp) hash += kv.Key.Stats.Density * 0.001f + kv.Value * 7.77f;

            // layout rect
            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float width   = Mathf.Abs(tr.x - bl.x);
            float height  = Mathf.Abs(tr.y - bl.y);
            float xLeft   = Mathf.Min(bl.x, tr.x);
            float yBottom = Mathf.Min(bl.y, tr.y);

            if (width <= 0.0001f || height <= 0.0001f)
            {
                if (debugLogs) Debug.LogWarning($"[CrucibleLiquidView] Area bounds are zero. Check AreaBottomLeft/AreaTopRight on {name}.", this);
                SetBandsActive(false);
                UpdateBubbles(0);
                return;
            }

            if (!float.IsNaN(_prevHash) && Mathf.Abs(hash - _prevHash) < 0.0001f)
            {
                AnimateHeights();
                UpdateBubbles(total);
                return;
            }
            _prevHash = hash;

            if (total <= 0f)
            {
                // No melted metal yet: hide bands but keep bubbles off
                if (debugLogs && Application.isPlaying)
                    Debug.Log($"[CrucibleLiquidView] No melted liters (yet) on {name}. Raise temperature or wait MeltTimeSeconds.", this);
                SetBandsActive(false);
                UpdateBubbles(0);
                return;
            }

            EnsureLiquidSprite();
            EnsurePoolSize(comp.Count);

            // density-desc so heavy metals sit at bottom
            var ordered = comp.OrderByDescending(k => k.Key.Stats.Density).ToList();

            float yCursor = yBottom;
            int i = 0;
            foreach (var kv in ordered)
            {
                float fraction   = Mathf.Clamp01(kv.Value / total);
                float bandHeight = height * fraction;

                var band = _pool[i++];
                band.metal         = kv.Key;
                band.targetHeight  = bandHeight;
                band.currentHeight = Mathf.Clamp(band.currentHeight, 0f, height);

                LayoutBand(band, xLeft, yCursor, width, band.currentHeight);
                ApplyColorAndSort(band, kv.Key, i - 1);

                yCursor += bandHeight;
                band.go.SetActive(true);
            }
            for (; i < _pool.Count; i++) _pool[i].go.SetActive(false);

            UpdateBubbles(total);
        }

        void AnimateHeights()
        {
            if (!Application.isPlaying) return;

            if (areaBottomLeft == null || areaTopRight == null) return;

            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float width   = Mathf.Abs(tr.x - bl.x);
            float height  = Mathf.Abs(tr.y - bl.y);
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
            t.localScale    = new Vector3(width, Mathf.Max(0.0001f, h), 1f);
        }

        void ApplyColorAndSort(Band band, Metal metal, int index)
        {
            if (!band.sr) return;

            var c = metal != null ? (Color)metal.BaseColor : Color.white;
            c.a = bandAlpha;
            band.sr.color = c;

            band.sr.sprite = liquidSprite;
            band.sr.sortingLayerName = sortingLayerName;
            band.sr.sortingOrder = sortingOrderBase + index; // stable depth front-to-back

            if (_hasParentMask) band.sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            else                band.sr.maskInteraction = SpriteMaskInteraction.None;
        }

        void EnsurePoolSize(int need)
        {
            while (_pool.Count < need)
            {
                var go = new GameObject("LiquidBand");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.drawMode = SpriteDrawMode.Simple;
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

            // place at surface height
            Vector3 bl = ToLocal(areaBottomLeft.position);
            Vector3 tr = ToLocal(areaTopRight.position);
            float height  = Mathf.Abs(tr.y - bl.y);
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
