﻿using System;
using System.Collections.Immutable;
using System.Xml.Linq;
#if CLIENT
using Microsoft.Xna.Framework;
#endif

namespace Barotrauma
{
    class TalentPrefab : PrefabWithUintIdentifier
    {
        public string OriginalName => Identifier.Value;

        public LocalizedString DisplayName { get; private set; }

        public LocalizedString Description { get; private set; }

        /// <summary>
        /// When set to false the AbilityEffects of multiple of the same talent will not be checked and only the first one.
        /// </summary>
        public bool AbilityEffectsStackWithSameTalent;

        public readonly Sprite Icon;

        /// <summary>
        /// When set to true, this talent will not be visible in the "Extra Talents" panel if it is not part of the character's job talent tree.
        /// </summary>
        public readonly bool IsHiddenExtraTalent;

        /// <summary>
        /// When set to a value the talent tooltip will display a text showing the current value of the stat and the max value.
        /// For example "Progress: 37/100".
        /// </summary>
        public readonly Option<(Identifier PermanentStatIdentifier, int Max)> TrackedStat;

#if CLIENT
        public readonly Option<Color> ColorOverride;
#endif

        public static readonly PrefabCollection<TalentPrefab> TalentPrefabs = new PrefabCollection<TalentPrefab>();

        public readonly ImmutableHashSet<TalentMigration> Migrations;

        public ContentXElement ConfigElement
        {
            get;
            private set;
        }

        public TalentPrefab(ContentXElement element, TalentsFile file) : base(file, element.GetAttributeIdentifier("identifier", Identifier.Empty))
        {
            ConfigElement = element;

            DisplayName = TextManager.Get($"talentname.{Identifier}").Fallback(Identifier.Value);

            AbilityEffectsStackWithSameTalent = element.GetAttributeBool("abilityeffectsstackwithsametalent", true);

            var trackedStat = element.GetAttributeIdentifier("trackedstat", Identifier.Empty);
            var trackedMax = element.GetAttributeInt("trackedmax", 100);
            TrackedStat = !trackedStat.IsEmpty 
                ? Option.Some((trackedStat, trackedMax))
                : Option.None;

            Identifier nameIdentifier = element.GetAttributeIdentifier("nameidentifier", Identifier.Empty);
            if (!nameIdentifier.IsEmpty)
            {
                DisplayName = TextManager.Get(nameIdentifier).Fallback(Identifier.Value);
            }

            IsHiddenExtraTalent = element.GetAttributeBool("ishiddenextratalent", false);

            Description = string.Empty;

#if CLIENT
            Color colorOverride = element.GetAttributeColor("coloroverride", Color.TransparentBlack);

            ColorOverride = colorOverride != Color.TransparentBlack
                ? Option<Color>.Some(colorOverride)
                : Option<Color>.None();
#endif

            var migrations = ImmutableHashSet.CreateBuilder<TalentMigration>();

            foreach (var subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "icon":
                        Icon = new Sprite(subElement);
                        break;
                    case "description":
                        var tempDescription = Description;
                        TextManager.ConstructDescription(ref tempDescription, subElement);
                        Description = tempDescription;
                        break;
                    case "migrations":
                        foreach (var migrationElement in subElement.Elements())
                        {
                            try
                            {
                                var migration = TalentMigration.FromXML(migrationElement);
                                migrations.Add(migration);
                            }
                            catch (Exception e)
                            {
                                DebugConsole.ThrowError($"Error while loading talent migration for talent \"{Identifier}\".", e,
                                    element?.ContentPackage);
                            }
                        }
                        break;
                }
            }

            Migrations = migrations.ToImmutable();

            if (element.GetAttribute("description") != null)
            {
                string description = element.GetAttributeString("description", string.Empty);
                Description = Description.Fallback(TextManager.Get(description)).Fallback(description);
            }
            else
            {
                Description = Description.Fallback(TextManager.Get($"talentdescription.{Identifier}")).Fallback(string.Empty);
            }
        }

        public override void Dispose() { }
    }
}
