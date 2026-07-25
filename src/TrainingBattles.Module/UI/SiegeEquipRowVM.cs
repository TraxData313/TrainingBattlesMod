using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TrainingBattles.UI
{
    /// <summary>
    /// One engine on the engineer's bench: which side will man it, what tier of the engineer's
    /// craft it takes, what it costs — and how many the drill fields. Locked rows (the engineer's
    /// skill short of the tier) show their price of entry instead of a tally. The +/− hands go
    /// through the window VM so the side caps stay honest.
    /// </summary>
    public class SiegeEquipRowVM : ViewModel
    {
        private readonly Action<SiegeEquipRowVM, int> _onChange;
        private int _count;

        public SiegeEngineType Engine { get; }
        public bool IsAttackerSide { get; }
        /// <summary>The engineer tier this engine takes (0 = always open).</summary>
        public int RequiredTier { get; }
        /// <summary>The Engineering skill the required tier opens at — for the locked label.</summary>
        public int RequiredSkill { get; }
        public bool Unlocked { get; }
        /// <summary>Per-row ceiling (1 ram, 2 towers); ranged engines share a side-wide cap
        /// enforced by the window VM.</summary>
        public int RowCap { get; }
        public bool CountsAgainstRangedCap { get; }
        public int CostEach { get; }

        public SiegeEquipRowVM(SiegeEngineType engine, bool attackerSide, int requiredTier,
            int requiredSkill, bool unlocked, int rowCap, bool countsAgainstRangedCap,
            int costEach, int count, Action<SiegeEquipRowVM, int> onChange)
        {
            Engine = engine;
            IsAttackerSide = attackerSide;
            RequiredTier = requiredTier;
            RequiredSkill = requiredSkill;
            Unlocked = unlocked;
            RowCap = rowCap;
            CountsAgainstRangedCap = countsAgainstRangedCap;
            CostEach = costEach;
            _count = unlocked ? count : 0;
            _onChange = onChange;
        }

        public void ExecuteAdd() => _onChange?.Invoke(this, +1);

        public void ExecuteRemove() => _onChange?.Invoke(this, -1);

        internal void SetCount(int count)
        {
            if (count == _count) return;
            _count = count;
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(HasAny));
        }

        internal int Count => _count;

        [DataSourceProperty]
        public string Name
        {
            get
            {
                try { return Engine?.Name?.ToString() ?? "Engine"; }
                catch { return "Engine"; }
            }
        }

        /// <summary>"Attack · 480 denars each" — or the locked row's honest price of entry.</summary>
        [DataSourceProperty]
        public string Detail
        {
            get
            {
                var side = IsAttackerSide ? "Attack" : "Defense";
                if (!Unlocked)
                    return side + " · needs Engineering " + RequiredSkill;
                return CostEach > 0
                    ? side + " · " + CostEach + " denars each"
                    : side + " · free";
            }
        }

        [DataSourceProperty]
        public bool CanAdd => Unlocked;

        [DataSourceProperty]
        public string CountText => Unlocked ? _count.ToString() : "—";

        [DataSourceProperty]
        public bool HasAny => _count > 0;
    }
}
