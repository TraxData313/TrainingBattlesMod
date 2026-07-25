using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace TrainingBattles.UI
{
    /// <summary>
    /// The phantom shipyard: every hull the world's cultures launch, each with a +/− tally, and
    /// a fleet-wide fittings tier (bare / harbor I / II / III) that dresses each conjured hull's
    /// slots with the best pieces that harbor level sells (<see cref="Core.PhantomFleetMath"/>
    /// does the picking, deterministically). The footer weighs the composed berths against the
    /// phantom company so the enemy is neither packed to the rails nor sailing empty decks.
    /// Confirm with no hulls clears the composition. A hard cap keeps the mission afloat.
    /// </summary>
    public class FleetComposeVM : ViewModel
    {
        /// <summary>The most hulls a phantom fleet may sail — a naval mission with more melts frames.</summary>
        public const int MaxHulls = 12;

        private readonly int _phantomMen;
        private readonly Action<List<KeyValuePair<ShipHull, int>>, int> _onConfirm;
        private readonly Action _onCancel;
        private int _tier;

        private MBBindingList<FleetComposeRowVM> _rows = new MBBindingList<FleetComposeRowVM>();

        /// <param name="phantomMen">The composed mock company's headcount (0 = not composed yet).</param>
        /// <param name="currentPick">The standing composition, re-opened for editing.</param>
        /// <param name="currentTier">The standing fittings tier.</param>
        /// <param name="onConfirm">Receives (hull, count) pairs — empty clears — and the tier.</param>
        public FleetComposeVM(int phantomMen, List<KeyValuePair<ShipHull, int>>? currentPick, int currentTier,
            Action<List<KeyValuePair<ShipHull, int>>, int> onConfirm, Action onCancel)
        {
            _phantomMen = phantomMen;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _tier = Math.Max(0, Math.Min(currentTier, 3));

            var standing = new Dictionary<ShipHull, int>();
            if (currentPick != null)
                foreach (var pair in currentPick)
                    if (pair.Key != null && pair.Value > 0) standing[pair.Key] = pair.Value;

            foreach (var (hull, culture) in HullsOfTheWorld())
            {
                standing.TryGetValue(hull, out var count);
                _rows.Add(new FleetComposeRowVM(hull, culture, count, ChangeCount));
            }
            RefreshSummary();
        }

        /// <summary>Every hull class any culture's yards launch — main cultures first, each
        /// culture's list in its own order, a hull named once for its first culture.</summary>
        private static List<(ShipHull Hull, string Culture)> HullsOfTheWorld()
        {
            var result = new List<(ShipHull, string)>();
            try
            {
                var cultures = new List<CultureObject>();
                foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                    if (culture?.AvailableShipHulls != null && culture.AvailableShipHulls.Count > 0)
                        cultures.Add(culture);
                cultures.Sort((a, b) => a.IsMainCulture != b.IsMainCulture
                    ? (a.IsMainCulture ? -1 : 1)
                    : string.Compare(a.Name?.ToString(), b.Name?.ToString(), StringComparison.Ordinal));
                var seen = new HashSet<ShipHull>();
                foreach (var culture in cultures)
                    foreach (var hull in culture.AvailableShipHulls)
                        if (hull != null && seen.Add(hull))
                            result.Add((hull, culture.Name?.ToString() ?? string.Empty));
            }
            catch { }
            return result;
        }

        // ------------------------------ the tallies ------------------------------

        private void ChangeCount(FleetComposeRowVM row, int delta)
        {
            var total = TotalHulls();
            if (delta > 0 && total >= MaxHulls) return; // the slips are full
            row.SetCount(Math.Max(0, row.Count + delta));
            RefreshSummary();
        }

        private int TotalHulls()
        {
            var total = 0;
            foreach (var row in _rows) total += row.Count;
            return total;
        }

        private int TotalCapacity()
        {
            var total = 0;
            foreach (var row in _rows)
            {
                if (row.Count <= 0) continue;
                try { total += row.Hull.TotalCrewCapacity * row.Count; } catch { }
            }
            return total;
        }

        public void ExecuteCycleTier()
        {
            _tier = (_tier + 1) % 4;
            OnPropertyChanged(nameof(TierText));
        }

        public void ExecuteConfirm()
        {
            var pick = new List<KeyValuePair<ShipHull, int>>();
            foreach (var row in _rows)
                if (row.Count > 0) pick.Add(new KeyValuePair<ShipHull, int>(row.Hull, row.Count));
            _onConfirm?.Invoke(pick, _tier);
        }

        public void ExecuteCancel() => _onCancel?.Invoke();

        // ------------------------------ what the window says ------------------------------

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(SummaryColor));
        }

        [DataSourceProperty]
        public string TitleText => "The enemy's shipyard";

        [DataSourceProperty]
        public string SubtitleText => "Lay down the phantom fleet, hull by hull — any culture's "
            + "yards, up to " + MaxHulls + " ships. The fittings tier dresses every hull alike; "
            + "the phantoms and their ships dissolve after the drill.";

        [DataSourceProperty]
        public string SummaryText
        {
            get
            {
                var hulls = TotalHulls();
                if (hulls == 0) return "No hulls laid down — confirming now clears the fleet.";
                var text = hulls + (hulls == 1 ? " hull" : " hulls")
                    + " · berths for " + TotalCapacity()
                    + " · " + _phantomMen + " phantoms";
                if (TotalCapacity() < _phantomMen) text += " — crowded decks";
                return text;
            }
        }

        [DataSourceProperty]
        public string SummaryColor =>
            TotalHulls() > 0 && TotalCapacity() < _phantomMen ? "#C97A4AFF" : "#8E8A80FF";

        [DataSourceProperty]
        public string TierText
        {
            get
            {
                switch (_tier)
                {
                    case 1: return "Fittings: harbor I";
                    case 2: return "Fittings: harbor II";
                    case 3: return "Fittings: harbor III";
                    default: return "Fittings: bare hulls";
                }
            }
        }

        [DataSourceProperty]
        public string ConfirmText => "Confirm";

        [DataSourceProperty]
        public string CancelText => "Cancel";

        [DataSourceProperty]
        public MBBindingList<FleetComposeRowVM> Rows
        {
            get => _rows;
            set { if (value != _rows) { _rows = value; OnPropertyChangedWithValue(value, nameof(Rows)); } }
        }
    }
}
