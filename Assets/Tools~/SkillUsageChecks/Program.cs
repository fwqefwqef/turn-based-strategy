using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new Exception(description);
    }

    checks++;
}

SkillData freeSkillData = new()
{
    Id = "free_skill",
    EndsTurn = false,
    OncePerTurn = false,
    MpCost = 2
};
Unit owner = new();
UnitSkillList skills = new(owner);
Skill freeSkill = skills.AddSkill(freeSkillData);

Check(freeSkillData.HasOncePerTurnUsageLimit, "A skill that does not end the action must be limited once per turn");
Check(skills.CanUse(freeSkill), "An unused action-preserving skill is usable");
Check(skills.MarkUsed(freeSkill), "An action-preserving skill can be used once");
Check(owner.CurrentManaPoints == 18, "Using the skill spends mana exactly once");
Check(!skills.CanUse(freeSkill), "An action-preserving skill cannot be reused in the same turn");
Check(!skills.MarkUsed(freeSkill) && owner.CurrentManaPoints == 18, "A rejected repeat does not spend mana");

skills.ResetTurnUsage();
Check(skills.CanUse(freeSkill), "The action-preserving skill becomes usable on the next turn");

SkillData repeatableActionSkillData = new()
{
    Id = "repeatable_action_skill",
    EndsTurn = true,
    OncePerTurn = false,
    MpCost = 0
};
Skill repeatableActionSkill = skills.AddSkill(repeatableActionSkillData);
Check(!repeatableActionSkillData.HasOncePerTurnUsageLimit, "An action-ending skill may explicitly opt out of the usage ledger");
Check(skills.MarkUsed(repeatableActionSkill) && skills.CanUse(repeatableActionSkill), "OncePerTurn false remains supported for action-ending skills");

SkillData equipmentSkillData = new()
{
    Id = "equipment_free_skill",
    EndsTurn = false,
    OncePerTurn = false,
    MpCost = 0
};
SkillRegistry.Register(equipmentSkillData);
owner.Inventory.EquippedWeapon = new Item
{
    GrantedSkillIds = new List<string> { equipmentSkillData.Id }
};
skills.RefreshEquipmentGrantedSkills();
Skill equipmentSkill = skills.Entries.Single(entry => entry.SkillId == equipmentSkillData.Id);
Check(skills.MarkUsed(equipmentSkill), "An equipment-granted action-preserving skill can be used once");

owner.Inventory.EquippedWeapon = null;
skills.RefreshEquipmentGrantedSkills();
owner.Inventory.EquippedWeapon = new Item
{
    GrantedSkillIds = new List<string> { equipmentSkillData.Id }
};
skills.RefreshEquipmentGrantedSkills();
Skill rebuiltEquipmentSkill = skills.Entries.Single(entry => entry.SkillId == equipmentSkillData.Id);
Check(!skills.CanUse(rebuiltEquipmentSkill), "Re-equipping cannot clear same-turn usage");

skills.ResetTurnUsage();
Check(skills.CanUse(rebuiltEquipmentSkill), "Equipment-granted usage resets on the next turn");

Console.WriteLine($"Skill usage checks passed: {checks}");
