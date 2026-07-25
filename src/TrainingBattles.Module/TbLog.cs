using System;
using System.IO;

namespace TrainingBattles
{
    /// <summary>
    /// The mod's rolling debug log: <c>Configs\TrainingBattles\training_battles.log</c>, one
    /// timestamped line per event, trimmed to its newest half when it tops ~1 MB. This is the
    /// WHEN-AND-WHO ledger (drills fought, duels judged, grounds picked, officers resolved,
    /// config migrations); the deep per-stack arithmetic of a single drill still lives in
    /// <c>last_drill_report.txt</c>. Toggled by <see cref="ModConfig.DebugLogging"/> (wired at
    /// startup, default on — Anton wants every playtest debuggable after the fact). Best-effort
    /// by design: a failed write must never cost gameplay, so everything swallows.
    /// </summary>
    internal static class TbLog
    {
        private const long TrimAtBytes = 1_000_000;
        private static readonly object Gate = new object();

        /// <summary>Set from config at startup (and on every MCM change). On when unset —
        /// early lines from before the config loads are worth keeping.</summary>
        public static bool Enabled = true;

        public static string LogFilePath => Path.Combine(ModConfig.ConfigDirectory, "training_battles.log");

        /// <summary>One line: <c>2026.07.25 13:02:11 [drill] ...</c>. The area is a short
        /// fixed tag (drill, duel, ground, hour, scout, config, officers) so the file greps
        /// clean.</summary>
        public static void Info(string area, string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(ModConfig.ConfigDirectory);
                    File.AppendAllText(LogFilePath,
                        DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")
                        + " [" + area + "] " + message + Environment.NewLine);
                    TrimIfHuge();
                }
            }
            catch { /* the log is a luxury, the game is not */ }
        }

        /// <summary>Keeps the file's newest half once it tops <see cref="TrimAtBytes"/> —
        /// cut at a line break so no half-line survives the trim.</summary>
        private static void TrimIfHuge()
        {
            var info = new FileInfo(LogFilePath);
            if (!info.Exists || info.Length <= TrimAtBytes) return;
            var text = File.ReadAllText(LogFilePath);
            var cut = text.IndexOf('\n', text.Length / 2);
            if (cut < 0) return;
            File.WriteAllText(LogFilePath,
                "(older lines trimmed " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + ")"
                + Environment.NewLine + text.Substring(cut + 1));
        }
    }
}
