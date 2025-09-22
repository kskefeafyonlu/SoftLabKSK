using UnityEngine;

namespace _Project.Quests
{
    [CreateAssetMenu(fileName = "QuestDefinition", menuName = "LabKSK/Quests/Quest Definition")]
    public class QuestDefinitionSO : ScriptableObject
    {
        public string questId;
        public string displayName;
        public string clientId;          // NPC or faction
        public string requestItemKey;    // e.g., "weapon.sword.greatsword"
        public bool urgent;
        [Min(0.1f)] public float deadlineHours = 24f;
        public RewardBundle baseRewards;
    }
}