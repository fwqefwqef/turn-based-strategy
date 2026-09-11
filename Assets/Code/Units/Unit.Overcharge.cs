using System;
using System.Linq;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Grid;
using UnityEngine;

namespace Windy.Srpg.Game.Units
{
    public enum OverchargeState { Unavailable, Ready, PendingActivation, Active, Spent }

    public partial class Unit
    {
        public const string BastionOverchargePassiveId = "fortress";
        public const string BastionUltimateSkillId = "break";
        private const int OverchargeDurationTurns = 3;

        [NonSerialized] private OverchargeState overchargeState;
        [NonSerialized] private int overchargeTurnsRemaining;
        [NonSerialized] private Passive overchargePassive;
        [NonSerialized] private SpriteRenderer overchargeSpriteRenderer;
        [NonSerialized] private Vector3 overchargeBaseSpriteScale = Vector3.one;
        [NonSerialized] private bool overchargeBaseSpriteScaleCaptured;

        public OverchargeState CurrentOverchargeState => overchargeState;
        public int OverchargeTurnsRemaining => overchargeTurnsRemaining;
        public bool IsOverchargeActive => overchargeState == OverchargeState.PendingActivation
            || overchargeState == OverchargeState.Active;
        public bool CanActivateOvercharge => PlayerNumber == 0 && overchargeState == OverchargeState.Ready;

        private bool IsBastionOverchargeUnit()
        {
            return PlayerNumber == 0
                && (string.Equals(UnitId, "bastion", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(AssignedPreset?.PresetId, "bastion", StringComparison.OrdinalIgnoreCase));
        }

        internal void ResetOverchargeForBattle()
        {
            EndOverchargeInternal(markSpent: false);
            overchargeState = IsBastionOverchargeUnit() ? OverchargeState.Ready : OverchargeState.Unavailable;
            RefreshOverchargeVisual();
        }

        public bool TryActivateOvercharge()
        {
            if (!CanActivateOvercharge) return false;

            EnsurePassiveList();
            overchargePassive = PassiveList.AddPassiveByIdFirst(BastionOverchargePassiveId);
            if (overchargePassive == null) return false;

            overchargeState = OverchargeState.PendingActivation;
            overchargeTurnsRemaining = OverchargeDurationTurns;
            SkillList?.SetOverchargeGrantedSkill(BastionUltimateSkillId);
            RefreshOverchargeVisual();
            BattleLog.Log("Action", $"{unitName} prepares Overcharge: Fortress.");
            RaiseBuffsChanged();
            return true;
        }

        public bool CancelPendingOvercharge()
        {
            if (overchargeState != OverchargeState.PendingActivation) return false;
            EndOverchargeInternal(markSpent: false);
            overchargeState = OverchargeState.Ready;
            RefreshOverchargeVisual();
            BattleLog.Log("Action", $"{unitName} cancels Overcharge.");
            RaiseBuffsChanged();
            return true;
        }

        internal void CommitPendingOvercharge()
        {
            if (overchargeState != OverchargeState.PendingActivation) return;
            overchargeState = OverchargeState.Active;
            BattleLog.Log("Action", $"{unitName} activates Overcharge: Fortress.");
            RefreshOverchargeVisual();
        }

        internal void NotifySkillCommitted(Skills.Skill skill)
        {
            if (IsOverchargeActive && string.Equals(skill?.SkillId, BastionUltimateSkillId, StringComparison.OrdinalIgnoreCase))
            {
                EndOverchargeInternal(markSpent: true);
            }
        }

        private void AdvanceOverchargeTurn()
        {
            if (!IsOverchargeActive) return;
            overchargeTurnsRemaining = Math.Max(0, overchargeTurnsRemaining - 1);
            if (overchargeTurnsRemaining == 0) EndOverchargeInternal(markSpent: true);
        }

        private void EndOverchargeInternal(bool markSpent)
        {
            SkillList?.SetOverchargeGrantedSkill(null);
            if (overchargePassive != null) PassiveList?.RemovePassive(overchargePassive);
            overchargePassive = null;
            overchargeTurnsRemaining = 0;
            if (markSpent) overchargeState = OverchargeState.Spent;
            RefreshOverchargeVisual();
        }

        internal void RefreshOverchargeVisual()
        {
            SpriteRenderer renderer = ResolveUnitSpriteRenderer();
            if (renderer == null) return;

            if (IsOverchargeActive)
            {
                if (!overchargeBaseSpriteScaleCaptured || overchargeSpriteRenderer != renderer)
                {
                    overchargeSpriteRenderer = renderer;
                    overchargeBaseSpriteScale = renderer.transform.localScale;
                    overchargeBaseSpriteScaleCaptured = true;
                }

                const float scaleMultiplier = 1.1f;
                renderer.transform.localScale = overchargeBaseSpriteScale * scaleMultiplier;
                return;
            }

            if (overchargeBaseSpriteScaleCaptured && overchargeSpriteRenderer != null)
            {
                overchargeSpriteRenderer.transform.localScale = overchargeBaseSpriteScale;
            }
            overchargeSpriteRenderer = null;
            overchargeBaseSpriteScaleCaptured = false;
        }

        internal bool HasPassiveTurnStartHealthEffects()
        {
            return PassiveList != null && PassiveList.GetActiveEffects().Any(effect => effect is IP_TurnStartHealthEffect);
        }

        internal int ConsumePassiveTurnStartHealthDelta()
        {
            if (PassiveList == null) return 0;
            int totalDelta = 0;
            foreach (IP_PassiveEffect effect in PassiveList.GetActiveEffects())
            {
                if (effect is IP_TurnStartHealthEffect healthEffect)
                {
                    totalDelta += ResolveTurnStartHealthDelta(healthEffect.GetTurnStartHealthDelta(this));
                }
            }
            return totalDelta;
        }

        internal int ResolveTurnStartHealthDelta(int delta)
        {
            if (delta >= 0 || PassiveList == null) return delta;
            int damage = Math.Max(0, -delta);
            foreach (IP_PassiveEffect effect in PassiveList.GetActiveEffects())
            {
                if (effect is IP_PainDamageChange modifier)
                {
                    damage = Math.Max(0, modifier.ModifyPainDamage(this, damage));
                }
            }
            return -damage;
        }
    }
}
