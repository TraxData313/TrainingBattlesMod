using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TrainingBattles
{
    /// <summary>
    /// WHO answers for WHAT — the single authority mapping a party's duties to its officers,
    /// on land and at sea (Anton's officers update, 2026.07.25: "make those roles come alive").
    ///
    ///   Duty          Land                            Sea (War Sails)
    ///   drill XP      Quartermaster — Leadership      First Mate — Boatswain
    ///   the ground    Scout — Scouting                Navigator — Shipmaster
    ///   the fallen    Surgeon — Medicine              Surgeon — Medicine (same at sea)
    ///   (future)      Engineer — Engineering, for the castle drills TASKS_TODO plans.
    ///
    /// Every Effective* role falls back to the party leader when unassigned — vanilla's own
    /// rule, verified in the decompiled MobileParty. The naval skills (Shipmaster, Boatswain)
    /// are War Sails objects looked up BY STRING ID through the object manager, so this
    /// assembly never references the DLC: without War Sails the lookup misses and the duty
    /// falls back to the land officer, honest label and all.
    /// </summary>
    internal static class Officers
    {
        /// <summary>One resolved duty: who answers, at what skill, and the honest names for
        /// menu lines and logs ("Quartermaster Ansif, Leadership 140").</summary>
        public readonly struct Officer
        {
            public Officer(Hero? hero, int skill, string role, string skillName)
            {
                Hero = hero;
                Skill = skill;
                Role = role;
                SkillName = skillName;
            }

            public Hero? Hero { get; }
            public int Skill { get; }
            public string Role { get; }
            public string SkillName { get; }

            /// <summary>"Quartermaster Ansif (Leadership 140)" — or the role alone when the
            /// party somehow has nobody (a leaderless temp party).</summary>
            public string Describe() => Hero != null
                ? Role + " " + Hero.Name + " (" + SkillName + " " + Skill + ")"
                : "no " + Role.ToLowerInvariant();
        }

        /// <summary>The officer whose skill sets the drill's XP-kept percent.</summary>
        public static Officer XpOfficer(MobileParty? party, bool atSea)
        {
            if (atSea)
            {
                var boatswain = NavalSkill("Boatswain");
                if (boatswain != null)
                    return Resolve(party?.EffectiveFirstMate, boatswain, "First Mate", "Boatswain");
            }
            return Resolve(party?.EffectiveQuartermaster, DefaultSkills.Leadership, "Quartermaster", "Leadership");
        }

        /// <summary>The officer whose skill fights the ground duel (battlefield choice and the
        /// exotic battle hours on a real encounter).</summary>
        public static Officer GroundOfficer(MobileParty? party, bool atSea)
        {
            if (atSea)
            {
                var shipmaster = NavalSkill("Shipmaster");
                if (shipmaster != null)
                    return Resolve(party?.EffectiveNavigator, shipmaster, "Navigator", "Shipmaster");
            }
            return Resolve(party?.EffectiveScout, DefaultSkills.Scouting, "Scout", "Scouting");
        }

        /// <summary>The officer whose Medicine runs the three casualty bands — the same doctor
        /// ashore and afloat.</summary>
        public static Officer SurgeonOfficer(MobileParty? party) =>
            Resolve(party?.EffectiveSurgeon, DefaultSkills.Medicine, "Surgeon", "Medicine");

        private static Officer Resolve(Hero? hero, SkillObject skill, string role, string skillName)
        {
            var value = 0;
            try { if (hero != null) value = hero.GetSkillValue(skill); }
            catch { }
            return new Officer(hero, value, role, skillName);
        }

        /// <summary>A War Sails skill by its registered string id ("Shipmaster", "Boatswain" —
        /// the ids NavalSkills.Create registers). Null without the DLC.</summary>
        private static SkillObject? NavalSkill(string stringId)
        {
            try { return MBObjectManager.Instance?.GetObject<SkillObject>(stringId); }
            catch { return null; }
        }
    }
}
