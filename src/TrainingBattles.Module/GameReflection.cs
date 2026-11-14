using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace TrainingBattles
{
    /// <summary>
    /// The mod's reach into the game's PRIVATE members, in one place that can be asked "is it all
    /// still there?" — because a game update moves private fields without a compile error, and a
    /// <c>?.SetValue</c> over a vanished field does nothing, silently (the v1.5.7 find, 2026.11.14:
    /// both siege-drill locks had been dead since the update and nothing said so).
    ///
    /// Two jobs:
    /// (1) THE SIEGE LOCKS. v1.5.7 split MapEvent into per-type <c>MapEventComponent</c>s: the
    /// "siege continues past this battle" flag and the event type left MapEvent
    /// (<c>_keepSiegeEvent</c>, <c>_mapEventType</c>) for <c>SiegeAssaultEventComponent</c>
    /// (<c>_keepSiegeEvent</c>, <c>_eventType</c>), reached through <c>MapEvent.Component</c>.
    /// Same meaning as before: with the flag set, <c>OnBeforeMapEventFinalize</c> skips the whole
    /// SiegeCompleted dispatch (and <c>OnFinalize</c> the AfterSiegeCompleted one). Still no public
    /// setter — <c>FinishBattleAndKeepSiegeEvent</c> sets it but ends the battle on the spot. Both
    /// shapes are resolved BY NAME (new first, old as fallback), so one build serves both game
    /// versions and neither needs the other's types at JIT time.
    /// (2) THE SELF-CHECK. Every by-string lookup the mod makes is listed in <see cref="Probes"/>
    /// and checked once per campaign start; a miss is written to training_battles.log by name. The
    /// members the siege drill cannot run safely without make <see cref="SiegeDrillBlocker"/>
    /// non-null, and the stronghold door and the launch gate refuse on it — a drill that cannot
    /// set its capture lock does not start. KEEP THE TABLE IN STEP with the call sites (grep
    /// GetField/GetMethod) when adding a lookup.
    /// </summary>
    internal static class GameReflection
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string SiegeComponentTypeName = "TaleWorlds.CampaignSystem.MapEvents.SiegeAssaultEventComponent";

        // ------------------------------ the siege locks ------------------------------

        private static bool _siegeResolved;
        private static PropertyInfo? _componentProperty; // MapEvent.Component (v1.5.7+), null on the old shape
        private static Type? _siegeComponentType;        // SiegeAssaultEventComponent (v1.5.7+)
        private static FieldInfo? _keepSiegeField;
        private static FieldInfo? _eventTypeField;
        private static string? _siegeBlocker;

        private static void ResolveSiege()
        {
            if (_siegeResolved) return;
            _siegeResolved = true;
            var missing = new List<string>();
            try
            {
                var mapEvent = typeof(MapEvent);
                _siegeComponentType = mapEvent.Assembly.GetType(SiegeComponentTypeName, throwOnError: false);
                _componentProperty = mapEvent.GetProperty("Component", Any);
                if (_siegeComponentType != null && _componentProperty != null)
                {
                    _keepSiegeField = _siegeComponentType.GetField("_keepSiegeEvent", Any);
                    _eventTypeField = _siegeComponentType.GetField("_eventType", Any);
                    if (_keepSiegeField == null) missing.Add("SiegeAssaultEventComponent._keepSiegeEvent");
                    if (_eventTypeField == null) missing.Add("SiegeAssaultEventComponent._eventType");
                }
                else
                {
                    // The pre-component game (the mod's v1.4.8 birthplace): both live on MapEvent.
                    _siegeComponentType = null;
                    _componentProperty = null;
                    _keepSiegeField = mapEvent.GetField("_keepSiegeEvent", Any);
                    _eventTypeField = mapEvent.GetField("_mapEventType", Any);
                    if (_keepSiegeField == null) missing.Add("MapEvent._keepSiegeEvent (and no SiegeAssaultEventComponent)");
                    if (_eventTypeField == null) missing.Add("MapEvent._mapEventType (and no SiegeAssaultEventComponent)");
                }
                if (_keepSiegeField != null && _keepSiegeField.FieldType != typeof(bool))
                    missing.Add(_keepSiegeField.DeclaringType?.Name + "._keepSiegeEvent is no longer a bool");
                if (_eventTypeField != null && _eventTypeField.FieldType != typeof(MapEvent.BattleTypes))
                    missing.Add(_eventTypeField.DeclaringType?.Name + "." + _eventTypeField.Name + " is no longer a BattleTypes");
            }
            catch (Exception ex)
            {
                missing.Add("siege lock lookup threw " + ex.GetType().Name + ": " + ex.Message);
            }
            foreach (var probe in Probes)
            {
                if (!probe.SiegeCritical) continue;
                try { if (probe.Find() == null) missing.Add(probe.Label); }
                catch (Exception ex) { missing.Add(probe.Label + " (" + ex.GetType().Name + ")"); }
            }
            _siegeBlocker = missing.Count == 0 ? null : string.Join(", ", missing);
            if (_siegeBlocker != null)
                TbLog.Info("reflection", "SIEGE DRILL DISABLED — the game no longer has: " + _siegeBlocker
                    + " | the drill will refuse to start rather than fight without its capture lock");
        }

        /// <summary>Null when every private member the siege drill needs is present; otherwise
        /// the missing members by name (for the log — the player gets a plain sentence).</summary>
        public static string? SiegeDrillBlocker
        {
            get { ResolveSiege(); return _siegeBlocker; }
        }

        /// <summary>The object that carries the siege fields for this event: its
        /// SiegeAssaultEventComponent on v1.5.7+, the event itself before. Throws when the event
        /// wears any other component — that is not a wall assault, and the lock has no home.</summary>
        private static object SiegeFieldHolder(MapEvent mapEvent)
        {
            ResolveSiege();
            if (_componentProperty == null) return mapEvent;
            var component = _componentProperty.GetValue(mapEvent);
            if (component == null || _siegeComponentType == null || !_siegeComponentType.IsInstanceOfType(component))
                throw new InvalidOperationException("the assault event is a "
                    + (component?.GetType().Name ?? "component-less event") + ", not a wall assault");
            return component;
        }

        /// <summary>Sets vanilla's "the siege continues past this battle" flag and READS IT BACK.
        /// Throws when it cannot — the caller's launch then aborts (men home, siege dismantled)
        /// instead of fighting a drill whose end would dispatch SiegeCompleted.</summary>
        public static void KeepSiegeEvent(MapEvent mapEvent)
        {
            ResolveSiege();
            if (_keepSiegeField == null)
                throw new InvalidOperationException("the siege-keep flag is gone from this game version");
            var holder = SiegeFieldHolder(mapEvent);
            _keepSiegeField.SetValue(holder, true);
            if (!(_keepSiegeField.GetValue(holder) is bool kept) || !kept)
                throw new InvalidOperationException("the siege-keep flag did not take");
        }

        /// <summary>Writes the event type back to Siege (a defender seated from outside the walls
        /// flips it to SiegeOutside). False + a reason when it cannot.</summary>
        public static bool RestoreSiegeType(MapEvent mapEvent, out string reason)
        {
            try
            {
                ResolveSiege();
                if (_eventTypeField == null) { reason = "the event-type field is gone from this game version"; return false; }
                _eventTypeField.SetValue(SiegeFieldHolder(mapEvent), MapEvent.BattleTypes.Siege);
                reason = string.Empty;
                return mapEvent.IsSiegeAssault;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        // ------------------------------ the self-check ------------------------------

        private sealed class Probe
        {
            public readonly string Label;
            public readonly Func<MemberInfo?> Find;
            public readonly bool SiegeCritical;
            public readonly string Costs;
            public Probe(string label, Func<MemberInfo?> find, bool siegeCritical, string costs)
            {
                Label = label; Find = find; SiegeCritical = siegeCritical; Costs = costs;
            }
        }

        /// <summary>Every private member the mod reaches for by name, outside the two siege locks
        /// (ResolveSiege owns those — their home depends on the game version). The War Sails rows
        /// name their types by string and are skipped without the DLC: this assembly must not
        /// carry a naval type (NavalBridge's class doc).</summary>
        private static readonly Probe[] Probes =
        {
            new Probe("MobileParty._besiegerCamp", () => typeof(MobileParty).GetField("_besiegerCamp", Any), true, "siege drill (camp swap)"),
            new Probe("BesiegerCamp._besiegerParties", () => typeof(BesiegerCamp).GetField("_besiegerParties", Any), true, "siege drill (camp swap)"),
            new Probe("BesiegerCamp._leaderParty", () => typeof(BesiegerCamp).GetField("_leaderParty", Any), true, "siege drill (camp swap)"),
            new Probe("BesiegerCamp._faction", () => typeof(BesiegerCamp).GetField("_faction", Any), true, "siege drill (camp swap)"),
            new Probe("SiegeEvent.DeactivateBlockade()", () => typeof(SiegeEvent).GetMethod("DeactivateBlockade", Any), false, "cosmetic: a blockade stays up during a port-castle drill"),
            new Probe("Team.<Color>k__BackingField", () => typeof(TaleWorlds.MountAndBlade.Team).GetField("<Color>k__BackingField", Any), false, "cosmetic: enemy team paint"),
            new Probe("Team.<Color2>k__BackingField", () => typeof(TaleWorlds.MountAndBlade.Team).GetField("<Color2>k__BackingField", Any), false, "cosmetic: enemy team paint"),
            new Probe("Team.<Banner>k__BackingField", () => typeof(TaleWorlds.MountAndBlade.Team).GetField("<Banner>k__BackingField", Any), false, "cosmetic: enemy team paint"),
            new Probe("PlayerTrackCompanionBehavior._scatteredCompanions", () => typeof(PlayerTrackCompanionBehavior).GetField("_scatteredCompanions", Any), false, "a false 'rejoined the company' line after a drill"),
            new Probe("NavalDLC DefaultNavalMissionLogic.DeployBattleSide()", () => NavalMethod("NavalDLC.Missions.MissionLogics.DefaultNavalMissionLogic", "DeployBattleSide"), false, "sea scout: flight recorder off"),
            new Probe("NavalDLC DefaultNavalMissionAgentSpawnLogic.AllocateAndDeployInitialTroops()", () => NavalMethod("NavalDLC.Missions.MissionLogics.DefaultNavalMissionAgentSpawnLogic", "AllocateAndDeployInitialTroops"), false, "sea scout: flight recorder off"),
            new Probe("NavalDLC DefaultNavalMissionAgentSpawnLogic.OnSideDeploymentOver()", () => NavalMethod("NavalDLC.Missions.MissionLogics.DefaultNavalMissionAgentSpawnLogic", "OnSideDeploymentOver"), false, "sea scout: flight recorder off"),
        };

        private static readonly MemberInfo SkippedNoDlc = typeof(GameReflection); // "not applicable" marker

        private static MemberInfo? NavalMethod(string typeName, string method)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, "NavalDLC", StringComparison.OrdinalIgnoreCase)) continue;
                return assembly.GetType(typeName, throwOnError: false)?.GetMethod(method, Any);
            }
            return SkippedNoDlc;
        }

        /// <summary>Checks every lookup once and writes the verdict to the log: one line when all
        /// is well, one line PER MISS otherwise. Returns the misses (empty = clean). Never throws.</summary>
        public static List<string> SelfCheck()
        {
            var misses = new List<string>();
            try
            {
                ResolveSiege();
                var checkedCount = 2; // the two siege locks
                if (_keepSiegeField == null) misses.Add("siege keep flag (siege drill disabled)");
                if (_eventTypeField == null) misses.Add("siege event type (siege drill disabled)");
                foreach (var probe in Probes)
                {
                    MemberInfo? found;
                    try { found = probe.Find(); }
                    catch (Exception ex) { found = null; TbLog.Info("reflection", probe.Label + " lookup threw " + ex.GetType().Name + ": " + ex.Message); }
                    if (ReferenceEquals(found, SkippedNoDlc)) continue;
                    checkedCount++;
                    if (found != null) continue;
                    misses.Add(probe.Label);
                    TbLog.Info("reflection", "MISSING " + probe.Label + " — game update? costs: "
                        + (probe.SiegeCritical ? "SIEGE DRILL DISABLED — " : string.Empty) + probe.Costs);
                }
                TbLog.Info("reflection", misses.Count == 0
                    ? "self-check clean | " + checkedCount + " private lookups found | siege locks on "
                        + (_componentProperty != null ? "SiegeAssaultEventComponent (v1.5.7+ shape)" : "MapEvent (pre-component shape)")
                    : "self-check: " + misses.Count + " of " + checkedCount + " private lookups MISSING | siege drill "
                        + (_siegeBlocker == null ? "ok" : "DISABLED"));
            }
            catch (Exception ex)
            {
                TbLog.Info("reflection", "self-check itself failed: " + ex);
            }
            return misses;
        }
    }
}
