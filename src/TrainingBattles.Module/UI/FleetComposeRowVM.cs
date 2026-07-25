using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TrainingBattles.UI
{
    /// <summary>
    /// One hull CLASS on the phantom shipyard's slips: its name, whose yards launch it, its
    /// crew berths — and how many the composed enemy fleet sails. The +/− hands go through
    /// the window VM so the totals stay honest.
    /// </summary>
    public class FleetComposeRowVM : ViewModel
    {
        private readonly Action<FleetComposeRowVM, int> _onChange;
        private int _count;

        public ShipHull Hull { get; }
        public string CultureLabel { get; }

        public FleetComposeRowVM(ShipHull hull, string cultureLabel, int count,
            Action<FleetComposeRowVM, int> onChange)
        {
            Hull = hull;
            CultureLabel = cultureLabel;
            _count = count;
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
                try { return Hull?.Name?.ToString() ?? "Hull"; }
                catch { return "Hull"; }
            }
        }

        /// <summary>"Sturgian yards · 90 crew" — where it's launched and what it berths.</summary>
        [DataSourceProperty]
        public string Detail
        {
            get
            {
                try
                {
                    var crew = Hull.TotalCrewCapacity;
                    return (string.IsNullOrEmpty(CultureLabel) ? "" : CultureLabel + " yards · ")
                        + crew + " crew";
                }
                catch { return CultureLabel; }
            }
        }

        [DataSourceProperty]
        public string CountText => _count.ToString();

        [DataSourceProperty]
        public bool HasAny => _count > 0;
    }
}
