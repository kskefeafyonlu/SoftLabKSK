using System;
using UnityEngine;

namespace _Project.Quests
{
    [Serializable]
    public class QuestInstance
    {
        public string runtimeId;
        public QuestDefinitionSO def;
        public QuestStatus status;
        public float secondsTotal;
        public float secondsRemaining;
        public float graceSecondsRemaining;
        public float acceptedAtGameTime;
        public float qualityScore;

        public bool IsOverdue => secondsRemaining <= 0f && graceSecondsRemaining <= 0f;
        public bool InGrace => secondsRemaining <= 0f && graceSecondsRemaining > 0f;

        public float TimeRatio => Mathf.Clamp01(secondsRemaining / Mathf.Max(1f, secondsTotal));
        public float DeadlineHours => secondsTotal / 3600f;
        public float HoursRemaining => Mathf.Max(0f, secondsRemaining) / 3600f;
    }
}