using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace TrainingBattles.UI
{
    /// <summary>
    /// The one home for the mod's custom Gauntlet windows (the ship-divide picker, the phantom
    /// shipyard — and whatever the future asks: siege gear, garrison drills). One modal window
    /// at a time, laid over whatever screen is up (the muster menu lives on the map screen, so
    /// the window opens right over it). The pattern is ImmersiveAI's chat window: a GauntletLayer
    /// + LoadMovie over a ViewModel, prefab XML under module\GUI\Prefabs — no Harmony, no view
    /// classes. Escape is polled from the tick and routed to the VM's cancel, so the window can
    /// never strand the player. Everything is best-effort: a UI failure closes the window,
    /// never the game.
    /// </summary>
    internal static class TrainingWindow
    {
        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static ViewModel? _vm;
        private static Action? _onEscape;

        internal static bool IsOpen => _layer != null;

        /// <summary>Opens the movie over the current top screen. <paramref name="onEscape"/> is
        /// the Escape key's road — wire it to the same handler as the window's Cancel button.</summary>
        internal static void Open(string movieName, ViewModel vm, Action onEscape)
        {
            if (IsOpen) Close(); // one window at a time — the newcomer wins
            try
            {
                _vm = vm;
                _onEscape = onEscape;
                _layer = new GauntletLayer(movieName, 4500);
                _movie = _layer.LoadMovie(movieName, vm);
                _layer.InputRestrictions.SetInputRestrictions();
                _layer.IsFocusLayer = true;
                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
            }
            catch (Exception ex)
            {
                Close();
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the window could not open — " + ex.Message));
            }
        }

        internal static void Close()
        {
            try
            {
                if (_layer != null)
                {
                    _layer.IsFocusLayer = false;
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _host?.RemoveLayer(_layer);
                }
            }
            catch { /* the screen may already be gone; nothing to restore */ }
            finally
            {
                _layer = null;
                _movie = null;
                _host = null;
                _vm = null;
                _onEscape = null;
            }
        }

        /// <summary>Called every application tick from <see cref="SubModule"/>. Cheap when no
        /// window is up; while one is, Escape means Cancel.</summary>
        internal static void Tick()
        {
            try
            {
                if (!IsOpen) return;
                var input = _layer?.Input;
                if (input != null && input.IsKeyReleased(InputKey.Escape))
                    _onEscape?.Invoke();
            }
            catch { /* never let the window's plumbing touch the frame */ }
        }
    }
}
