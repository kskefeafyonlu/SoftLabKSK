using UnityEngine;

namespace _Project.Blacksmithing.Foundry
{
    [RequireComponent(typeof(OreMB))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class OreHeatVisualMB : MonoBehaviour
    {
        public Color HotColor = new Color(1f, 0.35f, 0.2f, 1f);
        public float PreheatWindowC = 150f;

        private OreMB _ore;
        private SpriteRenderer _sr;
        private FoundaryMB _crucible;
        private Metal _metal;
        private Color _base; // now sourced from Metal.BaseColor

        private void Awake()
        {
            _ore = GetComponent<OreMB>();
            _sr = GetComponent<SpriteRenderer>();

            _metal = MetalsUtil.FromId(_ore.MetalId);
            _base  = (_metal != null) ? _metal.BaseColor : Color.white;

            // ensure starting color = metal base (even if OreMB.Awake hasn't run yet)
            if (_sr != null) _sr.color = _base;
        }

        private void Update()
        {
            // Not committed: keep base color, no heat effect yet
            if (!_ore.AddedToCrucible)
            {
                if (_sr != null) _sr.color = _base;
                _crucible = null;
                return;
            }

            if (_crucible == null) _crucible = GetComponentInParent<FoundaryMB>();
            if (_crucible == null || _metal == null || _sr == null) return;

            float startC = Mathf.Max(0f, _metal.MeltingPointC - PreheatWindowC);
            float endC   = _metal.MeltingPointC;

            float t = Mathf.InverseLerp(startC, endC, _crucible.CurrentTempC);
            t = Mathf.Clamp01(t);

            _sr.color = Color.Lerp(_base, HotColor, t);
        }
    }
}