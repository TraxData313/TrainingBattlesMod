using System;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Library;

namespace TrainingBattles.UI
{
    /// <summary>
    /// One hull in the ship-divide window: its name, a stats line (crew, fittings, health), and
    /// whether it may cross at all — the flagship is pinned to the player's side and says so.
    /// Clicking the row hands the hull to the other column (the window VM does the moving).
    /// </summary>
    public class ShipDivideRowVM : ViewModel
    {
        private readonly Action<ShipDivideRowVM> _onMove;

        public Ship Ship { get; }
        public bool IsFlagship { get; }

        public ShipDivideRowVM(Ship ship, bool isFlagship, Action<ShipDivideRowVM> onMove)
        {
            Ship = ship;
            IsFlagship = isFlagship;
            _onMove = onMove;
        }

        public void ExecuteMove()
        {
            if (!IsFlagship) _onMove?.Invoke(this);
        }

        [DataSourceProperty]
        public string Name
        {
            get
            {
                try { return Ship?.Name?.ToString() ?? "Ship"; }
                catch { return "Ship"; }
            }
        }

        /// <summary>"90 crew · 3 fittings · hull 100%" — and the flagship's pin, plainly.</summary>
        [DataSourceProperty]
        public string Detail
        {
            get
            {
                try
                {
                    var text = Ship.TotalCrewCapacity + " crew";
                    var fittings = 0;
                    try { fittings = Ship.GetShipSlotAndPieceNames().Count; } catch { }
                    if (fittings > 0) text += " · " + fittings + (fittings == 1 ? " fitting" : " fittings");
                    var max = Ship.MaxHitPoints;
                    if (max > 0f)
                        text += " · hull " + (int)Math.Round(Ship.HitPoints / max * 100f) + "%";
                    if (IsFlagship) text += " · flagship — stays with you";
                    return text;
                }
                catch
                {
                    return IsFlagship ? "flagship — stays with you" : string.Empty;
                }
            }
        }

        [DataSourceProperty]
        public bool CanMove => !IsFlagship;
    }
}
