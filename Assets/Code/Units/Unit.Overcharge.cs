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
        public const string ProtagonistOverchargePassiveId = "accelerated_movement";
        public const string ProtagonistUltimateSkillId = "mass_accelerate";
        public const string ThunderOverchargePassiveId = "wrath";
        public const string ThunderUltimateSkillId = "atrocity";
        public const string FlameOverchargePassiveId = "joy_of_burning";
        public const string FlameUltimateSkillId = "flame_last_stand";
        public const string DarknessOverchargePassiveId = "soul_stealer";
        public const string DarknessUltimateSkillId = "dark_sanctuary";
        public const string ShopkeepOverchargePassiveId = "penetrate";
        public const string ShopkeepUltimateSkillId = "shopkeep_disintegrate";
        public const string NurseOverchargePassiveId = "apotheosis";
        public const string NurseUltimateSkillId = "shining_pillar";
        private const int OverchargeDurationTurns = 3;

        private readonly struct OverchargeProfile
        {
            public readonly string PassiveId;
            public readonly string UltimateSkillId;
            public readonly string DisplayName;
            public readonly bool GrantsPostActionMovement;

            public OverchargeProfile(string passiveId, string ultimateSkillId, string displayName, bool grantsPostActionMovement = false)
            {
                PassiveId = passiveId;
                UltimateSkillId = ultimateSkillId;
                DisplayName = displayName;
                GrantsPostActionMovement = grantsPostActionMovement;
            }
        }

        [NonSerialized] private OverchargeState overchargeState;
        [NonSerialized] private int overchargeTurnsRemaining;
        [NonSerialized] private Passive overchargePassive;
        [NonSerialized] private string activeOverchargeUltimateSkillId;
        [NonSerialized] private string activeOverchargeDisplayName;
        [NonSerialized] private bool overchargeGrantsPostActionMovement;
        [NonSerialized] private bool postActionMovementActive;
        [NonSerialized] private SpriteRenderer overchargeSpriteRenderer;
        [NonSerialized] private Vector3 overchargeBaseSpriteScale = Vector3.one;
        [NonSerialized] private bool overchargeBaseSpriteScaleCaptured;

        public OverchargeState CurrentOverchargeState => overchargeState;
        public int OverchargeTurnsRemaining => overchargeTurnsRemaining;
        public bool IsOverchargeActive => overchargeState == OverchargeState.PendingActivation
            || overchargeState == OverchargeState.Active;
        public bool CanActivateOvercharge => PlayerNumber == 0 && overchargeState == OverchargeState.Ready;
        public bool IsPostActionMovementActive => postActionMovementActive;

        private bool TryGetOverchargeProfile(out OverchargeProfile profile)
        {
            // Overcharge profiles remain implemented, but presets must opt in explicitly.
            // Existing character presets intentionally leave this disabled for now.
            if (AssignedPreset == null || !AssignedPreset.EnableOvercharge)
            {
                profile = default;
                return false;
            }

            string identity = !string.IsNullOrWhiteSpace(AssignedPreset?.PresetId)
                ? AssignedPreset.PresetId
                : UnitId;
            if (PlayerNumber == 0 && string.Equals(identity, "bastion", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(BastionOverchargePassiveId, BastionUltimateSkillId, "Fortress");
                return true;
            }
            if (PlayerNumber == 0 && string.Equals(identity, "protagonist", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(ProtagonistOverchargePassiveId, ProtagonistUltimateSkillId, "Accelerated Movement", grantsPostActionMovement: true);
                return true;
            }
            if (PlayerNumber == 0 && string.Equals(identity, "thunder", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(ThunderOverchargePassiveId, ThunderUltimateSkillId, "Wrath");
                return true;
            }
            if (PlayerNumber == 0 && string.Equals(identity, "flame", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(FlameOverchargePassiveId, FlameUltimateSkillId, "Joy of Burning");
                return true;
            }
            if (PlayerNumber == 0 && string.Equals(identity, "darkness", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(DarknessOverchargePassiveId, DarknessUltimateSkillId, "Soul Stealer");
                return true;
            }
            if (PlayerNumber == 0 && string.Equals(identity, "shopkeep", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(ShopkeepOverchargePassiveId, ShopkeepUltimateSkillId, "Penetrate");
                return true;
            }
            if (PlayerNumber == 0 && string.Equals(identity, "nurse", StringComparison.OrdinalIgnoreCase))
            {
                profile = new OverchargeProfile(NurseOverchargePassiveId, NurseUltimateSkillId, "Apotheosis");
                return true;
            }

            profile = default;
            return false;
        }

        internal void ResetOverchargeForBattle()
        {
            EndOverchargeInternal(markSpent: false);
            overchargeState = TryGetOverchargeProfile(out _) ? OverchargeState.Ready : OverchargeState.Unavailable;
            RefreshOverchargeVisual();
        }

        public bool TryActivateOvercharge()
        {
            if (!CanActivateOvercharge || !TryGetOverchargeProfile(out OverchargeProfile profile)) return false;

            EnsurePassiveList();
            overchargePassive = PassiveList.AddPassiveByIdFirst(profile.PassiveId);
            if (overchargePassive == null) return false;

            overchargeState = OverchargeState.PendingActivation;
            overchargeTurnsRemaining = OverchargeDurationTurns;
            activeOverchargeUltimateSkillId = profile.UltimateSkillId;
            activeOverchargeDisplayName = profile.DisplayName;
            overchargeGrantsPostActionMovement = profile.GrantsPostActionMovement;
            SkillList?.SetOverchargeGrantedSkill(profile.UltimateSkillId);
            RecordPendingOperation(PendingUnitOperation.Overcharge);
            RefreshOverchargeVisual();
            BattleLog.Log("Action", $"{unitName} prepares Overcharge: {profile.DisplayName}.");
            RaiseBuffsChanged();
            return true;
        }

        public bool CancelPendingOvercharge()
        {
            if (overchargeState != OverchargeState.PendingActivation) return false;
            string displayName = activeOverchargeDisplayName;
            EndOverchargeInternal(markSpent: false);
            overchargeState = OverchargeState.Ready;
            RemovePendingOperation(PendingUnitOperation.Overcharge);
            RefreshOverchargeVisual();
            BattleLog.Log("Action", $"{unitName} cancels Overcharge: {displayName}.");
            RaiseBuffsChanged();
            return true;
        }

        internal void CommitPendingOvercharge()
        {
            if (overchargeState != OverchargeState.PendingActivation) return;
            overchargeState = OverchargeState.Active;
            BattleLog.Log("Action", $"{unitName} activates Overcharge: {activeOverchargeDisplayName}.");
            RefreshOverchargeVisual();
        }

        internal void NotifySkillCommitted(Skills.Skill skill)
        {
            if (IsOverchargeActive && string.Equals(skill?.SkillId, activeOverchargeUltimateSkillId, StringComparison.OrdinalIgnoreCase))
            {
                EndOverchargeInternal(markSpent: true);
            }
        }

        internal bool TryBeginPostActionMovement()
        {
            float movementPoints = PassiveList?.GetPostActionMovementPoints() ?? 0f;
            if (IsOverchargeActive && overchargeGrantsPostActionMovement)
            {
                movementPoints = Math.Max(movementPoints, 3f);
            }

            if (movementPoints <= 0f || postActionMovementActive
                || !IsAliveForBattle || IsActionBlocked || !IsFinishedForTurn)
            {
                return false;
            }

            postActionMovementActive = true;
            cachedPaths = null;
            MovementPoints = movementPoints;
            SetTurnStateKind(UnitTurnStateKind.Friendly);
            return true;
        }

        internal void FinishPostActionMovement()
        {
            postActionMovementActive = false;
            EndTurnForUnit();
        }

        private void AdvanceOverchargeTurn()
        {
            if (!IsOverchargeActive) return;
            overchargeTurnsRemaining = Math.Max(0, overchargeTurnsRemaining - 1);
            if (overchargeTurnsRemaining == 0) EndOverchargeInternal(markSpent: true);
        }

        private void EndOverchargeInternal(bool markSpent)
        {
            RemovePendingOperation(PendingUnitOperation.Overcharge);
            SkillList?.SetOverchargeGrantedSkill(null);
            if (overchargePassive != null) PassiveList?.RemovePassive(overchargePassive);
            overchargePassive = null;
            activeOverchargeUltimateSkillId = null;
            activeOverchargeDisplayName = null;
            overchargeGrantsPostActionMovement = false;
            postActionMovementActive = false;
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
                    totalDelta += ResolveTurnStartHealthDelta(healthEffect.GetTurnStartHealthDelta());
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
