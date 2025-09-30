using System.Collections.Generic;
using UnityEngine;

namespace _Project.CardSystem
{

    [ExecuteAlways]
    public sealed class HandMB : MonoBehaviour
    {
        [Header("Area & Prefab")]
        public RectTransform bottomArea;
        public CardMB cardPrefab;

        [Header("Card")]
        public Vector2 cardSize = new(150f, 200f);

        [Header("Arc Layout")]
        public float radius = 700f;                    // büyük = daha düz yay
        [Range(5f, 180f)] public float maxFanAngle = 40f;
        [Range(0f, 0.99f)] public float overlap = 0.20f; // 0=no overlap, 0.5=50% overlap kartların üst üste binmesi
        [Range(0f, 2f)] public float tiltScale = 0.8f; //açı * tiltScale = eğim
        public float yOffset = 0f;
        public Vector2 centerOffset = Vector2.zero;
        public bool arcDownwards = true;              // alt elde aşağı baksın
        public bool invertTilt = true;                // oyuncuya doğru eğim

        [Header("Neighbor Reveal")]
        public bool revealNeighbors = true;
        [Range(0f, 500f)] public float neighborPush = 28f; // 0=no push
        [Range(0, 8)] public int neighborRange = 2;
        [Range(0f, 1f)] public float neighborFalloff = 0.6f; // 1=no falloff

        [Header("Generation")]
        [Min(0)] public int generateCount = 5;

        private readonly List<CardData> _data = new();
        private readonly List<CardMB> _views = new();
        private System.Random _rng = new();

        public IReadOnlyList<CardData> Data => _data;

        // --- Inspector Buttons ---
        public void Cmd_GenerateSet()
        {
            var list = new List<CardData>(generateCount);
            for (int i = 0; i < generateCount; i++) list.Add(RandomCard(i + 1));
            SetCards(list);
        }
        public void Cmd_AddRandom()    => AddCard(RandomCard(_data.Count + 1));
        public void Cmd_RemoveRandom() => RemoveRandomCard(_rng);
        public void Cmd_Regenerate()   => Rebuild();

        // --- Public API ---
        public void SetCards(IEnumerable<CardData> src)
        {
            _data.Clear();
            _data.AddRange(src);
            Rebuild();
        }

        public void AddCard(CardData d)
        {
            _data.Add(d);
            var parent = bottomArea ? bottomArea : transform as RectTransform;
            var v = Instantiate(cardPrefab, parent);
            v.Rt.sizeDelta = cardSize;
            v.Bind(d);
            _views.Add(v);
            LayoutArc();
        }

        public bool RemoveRandomCard(System.Random r = null)
        {
            if (_data.Count == 0) return false;
            r ??= new System.Random();
            RemoveAt(r.Next(0, _data.Count));
            return true;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _data.Count) return;

            _data.RemoveAt(index);
            if (Application.isPlaying) Destroy(_views[index].gameObject);
            else DestroyImmediate(_views[index].gameObject);
            _views.RemoveAt(index);

            LayoutArc();
        }

        // --- Internals ---
        void Rebuild()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (Application.isPlaying) Destroy(_views[i].gameObject);
                else DestroyImmediate(_views[i].gameObject);
            }
            _views.Clear();

            var parent = bottomArea ? bottomArea : transform as RectTransform;
            foreach (var d in _data)
            {
                var v = Instantiate(cardPrefab, parent);
                v.Rt.sizeDelta = cardSize;
                v.Bind(d);
                _views.Add(v);
            }
            LayoutArc();
        }

        void LayoutArc()
        {
            var parent = bottomArea ? bottomArea : transform as RectTransform;
            if (!parent || _views.Count == 0) return;

            int n = _views.Count;

            // chord -> angular step (θ = 2 asin(chord/2R))
            float chord = Mathf.Max(1f, cardSize.x * (1f - overlap));
            float stepDegFromChord = Mathf.Rad2Deg * 2f * Mathf.Asin(Mathf.Clamp(chord / (2f * Mathf.Max(1f, radius)), 0f, 1f));
            float naturalSpread = stepDegFromChord * (n - 1);
            float spread = Mathf.Min(maxFanAngle, naturalSpread);
            float stepDeg = (n <= 1) ? 0f : spread / (n - 1);
            float startDeg = -spread * 0.5f;

            float dir = arcDownwards ? 1f : -1f;
            float tiltSign = invertTilt ? -1f : 1f;

            // first hovered/pressed
            int hoveredIdx = -1;
            for (int i = 0; i < n; i++)
                if (_views[i].IsHoveredOrPressed) { hoveredIdx = i; break; }

            for (int i = 0; i < n; i++)
            {
                float angDeg = startDeg + stepDeg * i;
                float angRad = angDeg * Mathf.Deg2Rad;

                float x = radius * Mathf.Sin(angRad);
                float y = dir * (-radius + radius * Mathf.Cos(angRad));

                var view = _views[i];
                var rt = view.Rt;

                rt.sizeDelta = cardSize;
                rt.anchoredPosition = new Vector2(x, y + yOffset) + centerOffset;
                rt.localRotation = Quaternion.Euler(0f, 0f, tiltSign * angDeg * tiltScale);
                rt.localScale = Vector3.one;
                rt.SetSiblingIndex(i);
                view.SetBaseSorting(i);

                // neighbor reveal (visual only; collider kalır)
                Vector2 visOffset = Vector2.zero;
                if (revealNeighbors && hoveredIdx >= 0 && hoveredIdx != i)
                {
                    int d = Mathf.Abs(i - hoveredIdx);
                    if (d <= neighborRange && d > 0)
                    {
                        float fall = Mathf.Pow(Mathf.Clamp01(neighborFalloff), d - 1);
                        float sign = Mathf.Sign(i - hoveredIdx); // left=-1, right=+1
                        visOffset = new Vector2(sign * neighborPush * fall, 0f);
                    }
                }
                view.SetNeighborOffset(visOffset);
            }
        }

        CardData RandomCard(int idx)
        {
            string[] roots = { "Ozan","Bozan","Cozan","Dozan","Kozan","Sozan","Uzan","Nigger" };
            string[] tags  = { "Bolulu","Colulu","Zululu","Nigger","Solulu","Dolulu" };
            var cardName = $"{roots[_rng.Next(roots.Length)]} {tags[_rng.Next(tags.Length)]} #{idx}";
            var color = new Color32((byte)_rng.Next(24,232),(byte)_rng.Next(24,232),(byte)_rng.Next(24,232),255);
            var c = new CardData(cardName, color);
            c.Set("power", (float)(1 + _rng.NextDouble() * 9));
            c.Set("weight", (float)(0.2 + _rng.NextDouble() * 2.3));
            return c;
        }

        void OnEnable()
        {
            if (!Application.isPlaying) LayoutArc();
        }

        void OnValidate()
        {
            overlap = Mathf.Clamp(overlap, 0f, 0.95f);
            radius = Mathf.Max(1f, radius);
            neighborRange = Mathf.Max(0, neighborRange);
            if (!Application.isPlaying) LayoutArc();
        }

        void Update()
        {
            if (Application.isPlaying) LayoutArc(); // hover state hızlı tepki
        }
    }
}
