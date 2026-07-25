using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Library;
using TrainingBattles.Core;

namespace TrainingBattles.UI
{
    /// <summary>
    /// The ship-divide window's mind: two columns — "with you" and "opposite" — a hull crossing
    /// on click, the flagship pinned. Opens on either the player's earlier pick or the automatic
    /// crew-proportional split (<see cref="FleetSplitMath"/>), and the "As the men divide" button
    /// returns to that default; Confirm then hands back NULL (follow the men, live) rather than a
    /// frozen copy of today's default, so re-dividing the men later re-divides the fleet too.
    /// Any hand-moved hull turns the pick explicit. Confirm is barred only when a side would sail
    /// empty — which the pinned flagship and the move logic already prevent.
    /// </summary>
    public class ShipDivideVM : ViewModel
    {
        private readonly List<Ship> _ships;
        private readonly int _flagshipIndex;
        private readonly int _playerMen;
        private readonly int _opponentMen;
        private readonly Action<List<Ship>?> _onConfirm;
        private readonly Action _onCancel;

        private bool _followMen; // true = the columns mirror the automatic split, untouched

        private MBBindingList<ShipDivideRowVM> _yourRows = new MBBindingList<ShipDivideRowVM>();
        private MBBindingList<ShipDivideRowVM> _oppositeRows = new MBBindingList<ShipDivideRowVM>();

        /// <param name="ships">The whole fleet, in fleet order.</param>
        /// <param name="flagshipIndex">The hull pinned to the player.</param>
        /// <param name="playerMen">Healthy men staying with the player.</param>
        /// <param name="opponentMen">Healthy men of the opposing half.</param>
        /// <param name="currentPick">The standing manual pick (opponent's hulls), null = automatic.</param>
        /// <param name="onConfirm">Receives the opponent's hulls — or null for "follow the men".</param>
        public ShipDivideVM(List<Ship> ships, int flagshipIndex, int playerMen, int opponentMen,
            List<Ship>? currentPick, Action<List<Ship>?> onConfirm, Action onCancel)
        {
            _ships = ships;
            _flagshipIndex = flagshipIndex;
            _playerMen = Math.Max(playerMen, 1);
            _opponentMen = Math.Max(opponentMen, 1);
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (currentPick != null && FillFromPick(currentPick)) _followMen = false;
            else { FillFromMath(); _followMen = true; }
            RefreshSummaries();
        }

        // ------------------------------ filling the columns ------------------------------

        private bool FillFromPick(List<Ship> pick)
        {
            var crossing = new HashSet<Ship>();
            foreach (var ship in pick)
            {
                var index = _ships.IndexOf(ship);
                if (index >= 0 && index != _flagshipIndex) crossing.Add(ship);
            }
            if (crossing.Count == 0 || crossing.Count >= _ships.Count) return false; // stale — fall back
            FillRows(index => crossing.Contains(_ships[index]));
            return true;
        }

        private void FillFromMath()
        {
            var capacities = new List<int>(_ships.Count);
            foreach (var ship in _ships)
            {
                var capacity = 0;
                try { capacity = ship.TotalCrewCapacity; } catch { }
                capacities.Add(capacity);
            }
            var crossing = new HashSet<int>(
                FleetSplitMath.OpponentShips(capacities, _flagshipIndex, _playerMen, _opponentMen));
            FillRows(crossing.Contains);
        }

        private void FillRows(Func<int, bool> goesOpposite)
        {
            var yours = new MBBindingList<ShipDivideRowVM>();
            var opposite = new MBBindingList<ShipDivideRowVM>();
            for (var i = 0; i < _ships.Count; i++)
            {
                var row = new ShipDivideRowVM(_ships[i], i == _flagshipIndex, MoveRow);
                if (i != _flagshipIndex && goesOpposite(i)) opposite.Add(row);
                else yours.Add(row);
            }
            YourRows = yours;
            OppositeRows = opposite;
        }

        // ------------------------------ moving a hull ------------------------------

        private void MoveRow(ShipDivideRowVM row)
        {
            if (YourRows.Contains(row))
            {
                // The player's side keeps the flagship, so it can never empty — no guard needed.
                YourRows.Remove(row);
                OppositeRows.Add(row);
            }
            else if (OppositeRows.Contains(row))
            {
                if (OppositeRows.Count <= 1) return; // both sides sail or nobody drills
                OppositeRows.Remove(row);
                YourRows.Add(row);
            }
            _followMen = false; // a hand on the tiller makes the pick explicit
            RefreshSummaries();
        }

        public void ExecuteDefault()
        {
            FillFromMath();
            _followMen = true;
            RefreshSummaries();
        }

        public void ExecuteConfirm()
        {
            if (_followMen)
            {
                _onConfirm?.Invoke(null);
                return;
            }
            var pick = new List<Ship>();
            foreach (var row in OppositeRows) pick.Add(row.Ship);
            _onConfirm?.Invoke(pick);
        }

        public void ExecuteCancel() => _onCancel?.Invoke();

        // ------------------------------ what the columns say ------------------------------

        private void RefreshSummaries()
        {
            OnPropertyChanged(nameof(YourSummary));
            OnPropertyChanged(nameof(OppositeSummary));
            OnPropertyChanged(nameof(YourSummaryColor));
            OnPropertyChanged(nameof(OppositeSummaryColor));
            OnPropertyChanged(nameof(ModeText));
        }

        private static int CapacityOf(MBBindingList<ShipDivideRowVM> rows)
        {
            var total = 0;
            foreach (var row in rows)
            {
                try { total += row.Ship.TotalCrewCapacity; } catch { }
            }
            return total;
        }

        private static string Summarize(MBBindingList<ShipDivideRowVM> rows, int men)
        {
            var capacity = CapacityOf(rows);
            return rows.Count + (rows.Count == 1 ? " hull" : " hulls")
                + " · berths for " + capacity + " · " + men + " men";
        }

        private static string ColorFor(MBBindingList<ShipDivideRowVM> rows, int men) =>
            CapacityOf(rows) < men ? "#C97A4AFF" : "#8E8A80FF"; // crowded decks get the warning tint

        [DataSourceProperty]
        public string TitleText => "Divide the ships";

        [DataSourceProperty]
        public string SubtitleText => "As the men drill in two halves, so sails the fleet — "
            + _playerMen + " men with you, " + _opponentMen + " opposite. Select a hull to send it across.";

        [DataSourceProperty]
        public string YourHeader => "With you";

        [DataSourceProperty]
        public string OppositeHeader => "Opposite";

        [DataSourceProperty]
        public string YourSummary => Summarize(_yourRows, _playerMen);

        [DataSourceProperty]
        public string OppositeSummary => Summarize(_oppositeRows, _opponentMen);

        [DataSourceProperty]
        public string YourSummaryColor => ColorFor(_yourRows, _playerMen);

        [DataSourceProperty]
        public string OppositeSummaryColor => ColorFor(_oppositeRows, _opponentMen);

        /// <summary>The standing rule, told plainly under the buttons.</summary>
        [DataSourceProperty]
        public string ModeText => _followMen
            ? "Following the men: the fleet re-divides with each new division of the company."
            : "Your pick: these very hulls, whatever the halves become.";

        [DataSourceProperty]
        public string DefaultButtonText => "As the men divide";

        [DataSourceProperty]
        public string ConfirmText => "Confirm";

        [DataSourceProperty]
        public string CancelText => "Cancel";

        [DataSourceProperty]
        public MBBindingList<ShipDivideRowVM> YourRows
        {
            get => _yourRows;
            set { if (value != _yourRows) { _yourRows = value; OnPropertyChangedWithValue(value, nameof(YourRows)); } }
        }

        [DataSourceProperty]
        public MBBindingList<ShipDivideRowVM> OppositeRows
        {
            get => _oppositeRows;
            set { if (value != _oppositeRows) { _oppositeRows = value; OnPropertyChangedWithValue(value, nameof(OppositeRows)); } }
        }
    }
}
