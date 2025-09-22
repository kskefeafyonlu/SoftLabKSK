using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Quests
{
    public class QuestRegistryMB : MonoBehaviour
    {
        [Header("Runtime Lists")]
        [SerializeField] private List<QuestInstance> available = new();
        [SerializeField] private List<QuestInstance> active = new();
        [SerializeField] private List<QuestInstance> finished = new();
        [SerializeField] private List<QuestInstance> failed = new();

        [Header("Clock")]
        [SerializeField] private bool useUnscaledTime = false;

        // Events for UI
        public event Action<QuestInstance> OnQuestAdded;
        public event Action<QuestInstance> OnQuestAccepted;
        public event Action<QuestInstance> OnQuestUpdated;
        public event Action<QuestInstance, QuestOutcome> OnQuestClosed;

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f || active.Count == 0) return;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var q = active[i];

                if (q.secondsRemaining > 0f)      q.secondsRemaining -= dt;
                else if (q.graceSecondsRemaining > 0f) q.graceSecondsRemaining -= dt;

                active[i] = q;
                OnQuestUpdated?.Invoke(q);

                if (q.IsOverdue)
                {
                    q.status = QuestStatus.Expired;
                    active.RemoveAt(i);
                    failed.Add(q);
                    OnQuestClosed?.Invoke(q, QuestOutcome.Expired);
                }
            }
        }

        // Readonly access
        public IReadOnlyList<QuestInstance> Available => available;
        public IReadOnlyList<QuestInstance> Active => active;
        public IReadOnlyList<QuestInstance> Finished => finished;
        public IReadOnlyList<QuestInstance> Failed => failed;

        // API
        public QuestInstance AddAvailable(QuestDefinitionSO def)
        {
            var qi = new QuestInstance
            {
                runtimeId = Guid.NewGuid().ToString("N"),
                def = def,
                status = QuestStatus.Available,
                secondsTotal = Mathf.Max(60f, def.deadlineHours * 3600f),
                secondsRemaining = Mathf.Max(60f, def.deadlineHours * 3600f),
                graceSecondsRemaining = 0f
            };
            available.Add(qi);
            OnQuestAdded?.Invoke(qi);
            return qi;
        }

        public bool Accept(string runtimeId)
        {
            int idx = available.FindIndex(q => q.runtimeId == runtimeId);
            if (idx < 0) return false;
            var q = available[idx];
            available.RemoveAt(idx);
            q.status = QuestStatus.Active;
            q.acceptedAtGameTime = Time.time;
            active.Add(q);
            OnQuestAccepted?.Invoke(q);
            return true;
        }

        public bool Deliver(string runtimeId, float qualityScore, out RewardBundle rewardsOut, out bool earlyBonusApplied)
        {
            rewardsOut = default;
            earlyBonusApplied = false;
            int idx = active.FindIndex(q => q.runtimeId == runtimeId);
            if (idx < 0) return false;

            var q = active[idx];
            q.qualityScore = Mathf.Clamp(qualityScore, 0f, 100f);
            q.status = QuestStatus.Finished;

            rewardsOut = ComputeRewards(q, out earlyBonusApplied);

            active.RemoveAt(idx);
            finished.Add(q);
            OnQuestClosed?.Invoke(q, QuestOutcome.Delivered);
            return true;
        }

        public bool Fail(string runtimeId, QuestOutcome outcome = QuestOutcome.Failed)
        {
            int idxA = active.FindIndex(q => q.runtimeId == runtimeId);
            if (idxA >= 0)
            {
                var q = active[idxA];
                q.status = outcome == QuestOutcome.Expired ? QuestStatus.Expired : QuestStatus.Failed;
                active.RemoveAt(idxA);
                failed.Add(q);
                OnQuestClosed?.Invoke(q, outcome);
                return true;
            }
            int idxV = available.FindIndex(q => q.runtimeId == runtimeId);
            if (idxV >= 0)
            {
                var q = available[idxV];
                q.status = QuestStatus.Failed;
                available.RemoveAt(idxV);
                failed.Add(q);
                OnQuestClosed?.Invoke(q, QuestOutcome.Failed);
                return true;
            }
            return false;
        }

        // Angry NPC returns; grant grace based on relationship (seconds)
        public bool GrantGrace(string runtimeId, float graceSeconds)
        {
            int idx = active.FindIndex(q => q.runtimeId == runtimeId);
            if (idx < 0) return false;
            var q = active[idx];
            if (q.secondsRemaining > 0f) return false; // only if overdue
            q.graceSecondsRemaining = Mathf.Max(q.graceSecondsRemaining, graceSeconds);
            active[idx] = q;
            OnQuestUpdated?.Invoke(q);
            return true;
        }

        private RewardBundle ComputeRewards(QuestInstance q, out bool earlyBonusApplied)
        {
            earlyBonusApplied = false;
            var r = q.def.baseRewards;

            // Quality multiplier (0.75x .. 1.25x)
            float qualityMul = Mathf.Lerp(0.75f, 1.25f, q.qualityScore / 100f);

            // Early delivery bonus (based on remaining ratio)
            float remainRatio = q.TimeRatio; // 0..1 of deadline left (ignores grace)
            if (remainRatio > 0.15f && r.earlyBonusMultiplier > 0f)
            {
                earlyBonusApplied = true;
                qualityMul *= (1f + r.earlyBonusMultiplier);
            }

            r.gold = Mathf.RoundToInt(r.gold * qualityMul);
            r.reputation = Mathf.RoundToInt(r.reputation * qualityMul);

            return r;
        }
    }
}
