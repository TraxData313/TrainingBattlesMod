using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TrainingBattles.Models
{
    /// <summary>
    /// The battle-hour gate for REAL battles (Anton, 2026.07.24: "Choose the time of day" before
    /// every battle type — field, siege, sea; night battles are unwatchable on many screens).
    /// A decorator over whatever MapWeatherModel was registered before us — vanilla's or another
    /// mod's — exactly like <see cref="TrainingBattlesSceneModel"/> decorates the SceneModel chain.
    ///
    /// Vanilla builds every battle mission's sky by calling <see cref="GetAtmosphereModel"/> while
    /// filling the MissionInitializerRecord (MenuHelper and SandBoxMissions, verified in the
    /// decompiled corpus). So: when the player pinned an hour (ModConfig.BattleTimeOfDay) AND a
    /// player map event is live (that call happens only when a battle mission is opening — sieges
    /// and sea battles included), the campaign atmosphere is swapped for the same fixed-hour
    /// preset vanilla's CUSTOM BATTLE uses (BannerlordMissions.CreateAtmosphereInfoForMission).
    /// Everything else — map weather ticks, wind, snow — always falls through untouched.
    ///
    /// Training drills are deliberately EXEMPT here: they build their own record through
    /// ScoutMission.CreatePatchAwareRecord, which applies the very same preset itself — the
    /// TrainingActive guard just keeps the two doors from double-dipping.
    /// </summary>
    public sealed class TrainingBattlesMapWeatherModel : MapWeatherModel
    {
        private readonly ModConfig _config;
        private MapWeatherModel? _fallback;

        public TrainingBattlesMapWeatherModel(ModConfig config)
        {
            _config = config;
        }

        // BaseModel is null only if nothing registered a MapWeatherModel before us — should never
        // happen (vanilla always does), but weather must keep ticking regardless.
        private MapWeatherModel Chain => BaseModel ?? (_fallback ??= new DefaultMapWeatherModel());

        public override AtmosphereInfo GetAtmosphereModel(CampaignVec2 position)
        {
            var hour = _config.BattleTimeOfDay;
            if (hour >= 0 && !TrainingBattleBehavior.TrainingActive && MapEvent.PlayerMapEvent != null)
            {
                var preset = AtmospherePresets.For(hour);
                if (preset != null) return preset.Value;
            }
            return Chain.GetAtmosphereModel(position);
        }

        // ------------- everything below is pure delegation down the chain -------------

        public override CampaignTime WeatherUpdateFrequency => Chain.WeatherUpdateFrequency;

        public override CampaignTime WeatherUpdatePeriod => Chain.WeatherUpdatePeriod;

        public override AtmosphereState GetInterpolatedAtmosphereState(CampaignTime timeOfYear, Vec3 pos)
            => Chain.GetInterpolatedAtmosphereState(timeOfYear, pos);

        public override void GetSeasonTimeFactorOfCampaignTime(CampaignTime ct, out float timeFactorForSnow, out float timeFactorForRain, bool snapCampaignTimeToWeatherPeriod = true)
            => Chain.GetSeasonTimeFactorOfCampaignTime(ct, out timeFactorForSnow, out timeFactorForRain, snapCampaignTimeToWeatherPeriod);

        public override WeatherEvent UpdateWeatherForPosition(CampaignVec2 position, CampaignTime ct)
            => Chain.UpdateWeatherForPosition(position, ct);

        public override void InitializeCaches()
            => Chain.InitializeCaches();

        public override WeatherEvent GetWeatherEventInPosition(Vec2 pos)
            => Chain.GetWeatherEventInPosition(pos);

        public override void GetSnowAndRainDataForPosition(Vec2 position, CampaignTime ct, out float snowValue, out float rainValue)
            => Chain.GetSnowAndRainDataForPosition(position, ct, out snowValue, out rainValue);

        public override WeatherEventEffectOnTerrain GetWeatherEffectOnTerrainForPosition(Vec2 pos)
            => Chain.GetWeatherEffectOnTerrainForPosition(pos);

        public override Vec2 GetWindForPosition(CampaignVec2 position)
            => Chain.GetWindForPosition(position);
    }

    /// <summary>The five fixed-hour skies vanilla's custom battle ships (its Morning/Noon/
    /// Afternoon/Evening/Night choices), plus the current campaign season — one shared table for
    /// the drill record, the muster scout and the real-battle weather gate.</summary>
    internal static class AtmospherePresets
    {
        /// <summary>Menu/MCM labels, in the order of <see cref="ModConfig.SupportedBattleHours"/>.</summary>
        public static string Label(int hour) => hour switch
        {
            6 => "Morning (06:00)",
            12 => "Noon (12:00)",
            15 => "Afternoon (15:00)",
            18 => "Evening (18:00)",
            22 => "Night (22:00)",
            _ => "Campaign clock",
        };

        public static AtmosphereInfo? For(int hour)
        {
            // The hour→asset table is vanilla's own (BannerlordMissions, verified this version).
            string? preset = hour switch
            {
                6 => "TOD_06_00_SemiCloudy",
                12 => "TOD_12_00_SemiCloudy",
                15 => "TOD_04_00_SemiCloudy",
                18 => "TOD_03_00_SemiCloudy",
                22 => "TOD_01_00_SemiCloudy",
                _ => null,
            };
            if (preset == null) return null;
            var info = default(AtmosphereInfo);
            info.AtmosphereName = preset;
            info.TimeInfo = new TimeInformation { Season = (int)CampaignTime.Now.GetSeasonOfYear };
            return info;
        }
    }
}
