using System.Collections.Generic;
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
    ///   the engines   Engineer — Engineering          (castle drills happen ashore)
    ///   the clock     Quartermaster — Steward         Quartermaster — Steward (same at sea:
    ///                                                 camp or hold, someone runs the stores)
    ///   instructing   the best-fighting companions, by their best weapon skill (no single
    ///                 officer — see <see cref="Instructors"/>; Anton's polish, 2026.07.26)
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

        /// <summary>The officer whose Steward speeds the drill cooldown (Anton's polish,
        /// 2026.07.26): a sharp quartermaster turns the camp around faster. The same role and
        /// skill ashore and afloat — whoever runs the stores runs the clock.</summary>
        public static Officer CooldownOfficer(MobileParty? party) =>
            Resolve(party?.EffectiveQuartermaster, DefaultSkills.Steward, "Quartermaster", "Steward");

        /// <summary>The drill INSTRUCTORS (Anton's polish, 2026.07.26): the party's
        /// best-fighting companions, each judged by the best of the six weapon skills — good
        /// fighters teach, so their presence raises the kept-XP rate (the arithmetic is
        /// <see cref="Core.AftermathMath.InstructorBonusPercent"/>). The party leader is
        /// excluded (the player drills, not lectures), the list comes sorted best-first and
        /// cut to <paramref name="maxCount"/>. Role "Instructor", skill name = the weapon
        /// that made the grade.</summary>
        public static List<Officer> Instructors(MobileParty? party, int maxCount)
        {
            var list = new List<Officer>();
            if (party?.MemberRoster == null || maxCount <= 0) return list;
            try
            {
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    var hero = element.Character?.HeroObject;
                    if (hero == null || hero == party.LeaderHero) continue;
                    SkillObject? bestSkill = null;
                    var best = -1;
                    foreach (var skill in FighterSkills)
                    {
                        var value = 0;
                        try { value = hero.GetSkillValue(skill); }
                        catch { }
                        if (value > best) { best = value; bestSkill = skill; }
                    }
                    if (bestSkill != null)
                        list.Add(new Officer(hero, best, "Instructor",
                            bestSkill.Name?.ToString() ?? bestSkill.StringId));
                }
            }
            catch { }
            list.Sort((a, b) => b.Skill.CompareTo(a.Skill));
            if (list.Count > maxCount) list.RemoveRange(maxCount, list.Count - maxCount);
            return list;
        }

        /// <summary>What makes a fighter: the six weapon skills (movement and command skills
        /// deliberately excluded — an instructor teaches arms, not marching).</summary>
        private static readonly SkillObject[] FighterSkills =
        {
            DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm,
            DefaultSkills.Bow, DefaultSkills.Crossbow, DefaultSkills.Throwing,
        };

        /// <summary>The officer whose Engineering unlocks the castle drill's siege equipment
        /// tiers (the castle update, 2026.07.25).</summary>
        public static Officer EngineerOfficer(MobileParty? party) =>
            Resolve(party?.EffectiveEngineer, DefaultSkills.Engineering, "Engineer", "Engineering");

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
