using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

namespace _Project.Blacksmithing.Casting
{
[DisallowMultipleComponent]
    public class CastIconProvider : MonoBehaviour
    {
        [Header("Assign the atlas with cast_* sprites")]
        public SpriteAtlas Atlas;

        private static CastIconProvider _active;
        private static readonly Dictionary<CastId, Sprite> _cache = new();
        private static readonly HashSet<CastId> _warned = new();

        // 1x1 placeholder sprite (lazy)
        private static Sprite _placeholder;

        private void OnEnable()
        {
            // Set the active provider (last enabled wins; fine for single-scene usage).
            _active = this;
            RebuildCache();
        }

        private void OnDisable()
        {
            if (_active == this) _active = null;
        }

        public void RebuildCache()
        {
            _cache.Clear();
            _warned.Clear();

            if (Atlas == null) return;

            foreach (var kv in CastDefs.All)
            {
                var id = kv.Key;
                var def = kv.Value;
                var s = Atlas.GetSprite(def.IconSpriteName);
                if (s != null) _cache[id] = s;
            }
        }

        /// <summary>
        /// Get the icon sprite for the given cast id. Returns a neutral placeholder if missing.
        /// Make sure a CastIconProvider with an assigned atlas exists in the scene.
        /// </summary>
        public static Sprite Get(CastId id)
        {
            if (_cache.TryGetValue(id, out var s) && s != null)
                return s;

            // Try late-load if an atlas exists but cache is empty/outdated
            if (_active != null && _active.Atlas != null)
            {
                var def = CastDefs.Get(id);
                var s2 = _active.Atlas.GetSprite(def.IconSpriteName);
                if (s2 != null)
                {
                    _cache[id] = s2;
                    return s2;
                }
            }

            if (!_warned.Contains(id))
            {
                Debug.LogWarning($"[CastIconProvider] Missing sprite for {id} (atlas or name mismatch). Using placeholder.");
                _warned.Add(id);
            }

            return GetPlaceholder();
        }

        public static bool TryGet(CastId id, out Sprite sprite)
        {
            sprite = Get(id);
            return sprite != null;
        }

        private static Sprite GetPlaceholder()
        {
            if (_placeholder != null) return _placeholder;

            // Build a tiny neutral texture at runtime (light gray)
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, new Color(0.85f, 0.85f, 0.85f, 1f));
            tex.SetPixel(1, 0, new Color(0.85f, 0.85f, 0.85f, 1f));
            tex.SetPixel(0, 1, new Color(0.85f, 0.85f, 0.85f, 1f));
            tex.SetPixel(1, 1, new Color(0.85f, 0.85f, 0.85f, 1f));
            tex.Apply(false, true);

            _placeholder = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _placeholder.name = "cast_placeholder";
            return _placeholder;
        }
    }
}