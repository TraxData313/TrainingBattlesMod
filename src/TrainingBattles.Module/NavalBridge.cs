using System;
using System.IO;
using System.Reflection;
using TaleWorlds.MountAndBlade;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TrainingBattles.Naval")]

namespace TrainingBattles
{
    /// <summary>
    /// THE DOOR TO THE NAVAL SATELLITE — and the module's guarantee that it loads without War Sails.
    ///
    /// THE BUG THIS EXISTS FOR (Nexus, 2026.07.28): v1.3.0-v1.3.3 threw a dependency error on
    /// startup for every player WITHOUT War Sails, and the mod never loaded at all. One class did
    /// it — the sea scout's deployment controller, which derived from a NavalDLC type. A base type
    /// is resolved EAGERLY at type load, so any <c>Assembly.GetTypes()</c> walk over
    /// TrainingBattles.dll threw a ReflectionTypeLoadException on an install without the DLC — and
    /// the view-creator scan, MCM's settings scan and the savegame scan all take that walk while
    /// the game boots. Method BODIES are different: they are JIT-compiled on first call, so a
    /// naval type inside one costs nothing until that method actually runs (impossible without the
    /// DLC — you cannot be at sea). Hence the module's standing rule, now without exceptions:
    /// <b>naval types in method bodies only</b>. Anything needing one in its TYPE SURFACE — a base
    /// class, an implemented interface, a field, a method signature — lives in
    /// TrainingBattles.Naval.dll instead, which this class loads by hand and only when the DLC is
    /// already in the AppDomain.
    ///
    /// The satellite ships beside TrainingBattles.dll in the module's bin folder (deploy.ps1 and
    /// package.ps1 both copy it) and is never named in SubModule.xml — the game loads only the
    /// declared DLL, so the satellite arrives exactly when we ask for it, never on boot.
    /// </summary>
    internal static class NavalBridge
    {
        private const string SatelliteName = "TrainingBattles.Naval";
        private const string DeploymentControllerType = "TrainingBattles.Naval.SeaScoutDeploymentController";

        private static Assembly? _satellite;
        private static bool _attempted;

        /// <summary>True when War Sails' own assembly is loaded — the gate on every satellite road.
        /// Checked by name so this file, like the rest of the module, never references the DLC.</summary>
        public static bool WarSailsLoaded
        {
            get
            {
                try
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        if (string.Equals(assembly.GetName().Name, "NavalDLC", StringComparison.OrdinalIgnoreCase))
                            return true;
                }
                catch { }
                return false;
            }
        }

        /// <summary>The sea scout ride's flight-recording deployment controller, or null when the
        /// satellite cannot be had (no War Sails, file missing, load refused). Callers fall back to
        /// the DLC's plain controller — the ride still sails, only the log goes quiet.</summary>
        public static MissionBehavior? CreateSeaScoutDeploymentController()
        {
            var assembly = Load();
            if (assembly == null) return null;
            try
            {
                var type = assembly.GetType(DeploymentControllerType, throwOnError: false);
                if (type == null)
                {
                    TbLog.Info("naval-bridge", "satellite loaded but " + DeploymentControllerType + " is missing");
                    return null;
                }
                return Activator.CreateInstance(type) as MissionBehavior;
            }
            catch (Exception ex)
            {
                TbLog.Info("naval-bridge", "controller construction failed: " + ex.GetType().Name + " " + ex.Message);
                return null;
            }
        }

        /// <summary>Loads the satellite once, from OUR OWN folder (never the probing path — the
        /// game's module bin is not the AppDomain's base directory). Failure is remembered, so a
        /// missing satellite costs one log line per session, not one per ride.</summary>
        private static Assembly? Load()
        {
            if (_attempted) return _satellite;
            _attempted = true;
            if (!WarSailsLoaded)
            {
                TbLog.Info("naval-bridge", "War Sails not loaded — naval satellite left on the shelf");
                return null;
            }
            try
            {
                var here = Path.GetDirectoryName(typeof(NavalBridge).Assembly.Location);
                if (string.IsNullOrEmpty(here)) return null;
                var path = Path.Combine(here, SatelliteName + ".dll");
                if (!File.Exists(path))
                {
                    TbLog.Info("naval-bridge", "naval satellite not found beside the module: " + path);
                    return null;
                }
                _satellite = Assembly.LoadFrom(path);
                TbLog.Info("naval-bridge", "naval satellite loaded: " + path);
            }
            catch (Exception ex)
            {
                TbLog.Info("naval-bridge", "naval satellite load failed: " + ex.GetType().Name + " " + ex.Message);
                _satellite = null;
            }
            return _satellite;
        }
    }
}
