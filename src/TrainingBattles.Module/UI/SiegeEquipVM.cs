using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TrainingBattles.Core;

namespace TrainingBattles.UI
{
    /// <summary>
    /// The engineer's bench: the castle drill's siege equipment, both sides of the walls on one
    /// window. The ENGINEER's Engineering skill unlocks engines in tiers (tier 0 — the ram —
    /// always; ballista and onager at tier 1; the siege tower and fire variants at tier 2; the
    /// trebuchet at tier 3 — thresholds MCM-tunable), and every engine built adds its man-day
    /// worth to the drill's bill. Caps mirror the mission's own deployment slots: one ram, two
    /// towers, four ranged engines a side. Confirm with nothing picked clears the equipment —
    /// ladders always exist, so a bare assault is a fair drill too.
    /// </summary>
    public class SiegeEquipVM : ViewModel
    {
        public const int RangedCapPerSide = 4;

        private readonly string _engineerLabel;
        private readonly int _tier;
        private readonly Action<List<KeyValuePair<SiegeEngineType, int>>, List<KeyValuePair<SiegeEngineType, int>>> _onConfirm;
        private readonly Action _onCancel;

        private MBBindingList<SiegeEquipRowVM> _rows = new MBBindingList<SiegeEquipRowVM>();

        /// <param name="engineerLabel">The resolved engineer, spelled out ("Engineer Ansif
        /// (Engineering 80)" — Officers.Officer.Describe(); primitives keep this VM public
        /// while the officers table stays internal).</param>
        public SiegeEquipVM(string engineerLabel, int engineerSkill,
            int tier1Skill, int tier2Skill, int tier3Skill,
            int goldPerManDay,
            List<KeyValuePair<SiegeEngineType, int>>? currentAtk,
            List<KeyValuePair<SiegeEngineType, int>>? currentDef,
            Action<List<KeyValuePair<SiegeEngineType, int>>, List<KeyValuePair<SiegeEngineType, int>>> onConfirm,
            Action onCancel)
        {
            _engineerLabel = engineerLabel;
            _tier = SiegeDrillMath.TierForSkill(engineerSkill, tier1Skill, tier2Skill, tier3Skill);
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            var standingAtk = ToDict(currentAtk);
            var standingDef = ToDict(currentDef);
            var thresholds = new[] { 0, tier1Skill, tier2Skill, tier3Skill };

            // The bench, attack first then the walls — each entry: engine, tier, row cap,
            // whether it shares the side's ranged slots. Mirrors vanilla's own siege catalog
            // (Onager/Trebuchet are the attacker's pieces, Catapult the walls' mangonel).
            AddRow(DefaultSiegeEngineTypes.Ram, attacker: true, tier: 0, rowCap: 1, ranged: false, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.Ballista, attacker: true, tier: 1, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.Onager, attacker: true, tier: 1, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.SiegeTower, attacker: true, tier: 2, rowCap: 2, ranged: false, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.FireBallista, attacker: true, tier: 2, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.FireOnager, attacker: true, tier: 2, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.Trebuchet, attacker: true, tier: 3, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingAtk);
            AddRow(DefaultSiegeEngineTypes.Ballista, attacker: false, tier: 1, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingDef);
            AddRow(DefaultSiegeEngineTypes.Catapult, attacker: false, tier: 1, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingDef);
            AddRow(DefaultSiegeEngineTypes.FireBallista, attacker: false, tier: 2, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingDef);
            AddRow(DefaultSiegeEngineTypes.FireCatapult, attacker: false, tier: 2, rowCap: RangedCapPerSide, ranged: true, thresholds, goldPerManDay, standingDef);
            RefreshSummary();
        }

        private static Dictionary<SiegeEngineType, int> ToDict(List<KeyValuePair<SiegeEngineType, int>>? pick)
        {
            var result = new Dictionary<SiegeEngineType, int>();
            if (pick != null)
                foreach (var pair in pick)
                    if (pair.Key != null && pair.Value > 0) result[pair.Key] = pair.Value;
            return result;
        }

        private void AddRow(SiegeEngineType engine, bool attacker, int tier, int rowCap, bool ranged,
            int[] tierThresholds, int goldPerManDay, Dictionary<SiegeEngineType, int> standing)
        {
            if (engine == null) return; // a modded-out engine simply has no bench row
            standing.TryGetValue(engine, out var count);
            var costEach = 0;
            try { costEach = SiegeDrillMath.EngineCost(engine.ManDayCost, goldPerManDay); } catch { }
            _rows.Add(new SiegeEquipRowVM(engine, attacker, tier, tierThresholds[tier],
                unlocked: _tier >= tier, rowCap, ranged, costEach,
                _tier >= tier ? count : 0, ChangeCount));
        }

        // ------------------------------ the tallies ------------------------------

        private void ChangeCount(SiegeEquipRowVM row, int delta)
        {
            if (delta > 0)
            {
                if (!row.Unlocked) return;
                if (row.Count >= row.RowCap) return;
                if (row.CountsAgainstRangedCap && RangedOnSide(row.IsAttackerSide) >= RangedCapPerSide) return;
            }
            row.SetCount(Math.Max(0, row.Count + delta));
            RefreshSummary();
        }

        private int RangedOnSide(bool attackerSide)
        {
            var total = 0;
            foreach (var row in _rows)
                if (row.IsAttackerSide == attackerSide && row.CountsAgainstRangedCap) total += row.Count;
            return total;
        }

        private int TotalBill()
        {
            long total = 0;
            foreach (var row in _rows) total += (long)row.CostEach * row.Count;
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        public void ExecuteConfirm()
        {
            var atk = new List<KeyValuePair<SiegeEngineType, int>>();
            var def = new List<KeyValuePair<SiegeEngineType, int>>();
            foreach (var row in _rows)
            {
                if (row.Count <= 0) continue;
                (row.IsAttackerSide ? atk : def).Add(new KeyValuePair<SiegeEngineType, int>(row.Engine, row.Count));
            }
            _onConfirm?.Invoke(atk, def);
        }

        public void ExecuteCancel() => _onCancel?.Invoke();

        // ------------------------------ what the window says ------------------------------

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(SummaryText));
        }

        [DataSourceProperty]
        public string TitleText => "The engineer's bench";

        [DataSourceProperty]
        public string SubtitleText => "Siege equipment for the drill, both sides of the walls — "
            + _engineerLabel + " builds tier " + _tier
            + (_tier >= 3 ? " (everything)" : "") + ". Assault ladders always stand; one ram, two "
            + "towers, " + RangedCapPerSide + " ranged engines a side. Each engine adds its worth "
            + "to the drill's bill.";

        [DataSourceProperty]
        public string SummaryText
        {
            get
            {
                var atk = 0;
                var def = 0;
                foreach (var row in _rows)
                {
                    if (row.IsAttackerSide) atk += row.Count;
                    else def += row.Count;
                }
                if (atk + def == 0) return "No engines — ladders and bare hands (confirming now clears the bench).";
                var bill = TotalBill();
                return atk + " attacking, " + def + " on the walls"
                    + (bill > 0 ? " · " + bill + " denars of equipment" : " · free");
            }
        }

        [DataSourceProperty]
        public string SummaryColor => "#8E8A80FF";

        [DataSourceProperty]
        public string ConfirmText => "Confirm";

        [DataSourceProperty]
        public string CancelText => "Cancel";

        [DataSourceProperty]
        public MBBindingList<SiegeEquipRowVM> Rows
        {
            get => _rows;
            set { if (value != _rows) { _rows = value; OnPropertyChangedWithValue(value, nameof(Rows)); } }
        }
    }
}
