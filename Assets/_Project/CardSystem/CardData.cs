using System.Collections.Generic;
using UnityEngine;

namespace _Project.CardSystem
{
    [System.Serializable]
    public sealed class CardData
    {
        public string name;
        public Color32 color;
        public readonly Dictionary<string, float> Vars = new();

        public CardData(string name, Color32 color) { this.name = name; this.color = color; }

        #region Variable Methods

        public float Get(string key, float defaultValue = 0f) => Vars.TryGetValue(key, out var value) ? value : defaultValue;
        public void Set(string key, float value) => Vars[key] = value;
        
        #endregion
        
        /// Abi buralara yeni özellikler ekleyebilirsin :):):)
    }
}