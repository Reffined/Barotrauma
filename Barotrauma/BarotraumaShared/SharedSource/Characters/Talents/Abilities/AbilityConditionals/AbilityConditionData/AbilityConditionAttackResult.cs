﻿using Barotrauma.Items.Components;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Abilities
{
    class AbilityConditionAttackResult : AbilityConditionData
    {
        private readonly List<TargetType> targetTypes;
        private readonly string[] afflictions;
        public AbilityConditionAttackResult(CharacterTalent characterTalent, XElement conditionElement) : base(characterTalent, conditionElement)
        {
            targetTypes = ParseTargetTypes(conditionElement.GetAttributeStringArray("targettypes", new string[0], convertToLowerInvariant: true));
            afflictions = conditionElement.GetAttributeStringArray("afflictions", new string[0], convertToLowerInvariant: true);
        }

        protected override bool MatchesConditionSpecific(object abilityData)
        {
            if (abilityData is AttackResult attackResult)
            {
                if (!IsViableTarget(targetTypes, attackResult.HitLimb?.character)) { return false; }

                if (afflictions.Any())
                {
                    if (!afflictions.Any(a => attackResult.Afflictions.Select(c => c.Identifier).Contains(a))) { return false; }
                }

                return true;
            }
            else
            {
                LogAbilityConditionError(abilityData, typeof(AbilityAttackData));
                return false;
            }
        }
    }
}
