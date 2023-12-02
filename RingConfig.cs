using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Utils;

namespace SERingAsteroids
{
    public class RingConfig
    {
        public string PlanetName { get; set; }
        public string ModId { get; set; }
        public bool? Vanilla { get; set; }
        public double? RingOuterRadius { get; set; }
        public double? RingInnerRadius { get; set; }
        public double? RingHeight { get; set; }
        public double? SectorSize { get; set; }
        public int? MaxAsteroidsPerSector { get; set; }
        public double? RingLongitudeAscendingNode { get; set; }
        public double? RingInclination { get; set; }
        public double? MinAsteroidSize { get; set; }
        public double? MaxAsteroidSize { get; set; }
        public double? EntityMovementThreshold { get; set; }
        public double? SizeExponent { get; set; }
        public double? ExclusionZoneSize { get; set; }
        public double? ExclusionZoneSizeMult { get; set; }
        public List<RingZone> RingZones { get; set; }
        public bool? TaperRingEdge { get; set; }
        public bool? Enabled { get; set; }
        public bool? EarlyLog { get; set; }
        public bool? LogDebug { get; set; }

        public bool IsConfigComplete
        {
            get
            {
                return PlanetName != null &&
                       RingOuterRadius != null &&
                       RingInnerRadius != null &&
                       RingHeight != null &&
                       SectorSize != null &&
                       MaxAsteroidsPerSector != null &&
                       RingLongitudeAscendingNode != null &&
                       RingInclination != null &&
                       MaxAsteroidSize != null &&
                       MinAsteroidSize != null &&
                       EntityMovementThreshold != null;
            }
        }

        public IEnumerable<string> GetMissingValueNames()
        {
            if (PlanetName == null) yield return "PlanetName";
            if (RingOuterRadius == null) yield return "RingOuterRadius";
            if (RingInnerRadius == null) yield return "RingInnerRadius";
            if (RingHeight == null) yield return "RingHeight";
            if (SectorSize == null) yield return "SectorSize";
            if (MaxAsteroidsPerSector == null) yield return "MaxAsteroidsPerSector";
            if (RingLongitudeAscendingNode == null) yield return "RingLongitudeAscendingNode";
            if (RingInclination == null) yield return "RingInclination";
            if (MaxAsteroidSize == null) yield return "MaxAsteroidSize";
            if (MinAsteroidSize == null) yield return "MinAsteroidSize";
            if (EntityMovementThreshold == null) yield return "EntityMovementThreshold";
        }

        public static RingConfig GlobalDefault { get; set; } = new RingConfig
        {
            MinAsteroidSize = 128,
            MaxAsteroidSize = 2048,
            EntityMovementThreshold = 512,
            ExclusionZoneSize = 64,
            ExclusionZoneSizeMult = 1.5,
            TaperRingEdge = true,
            SizeExponent = 2.0
        };

        public static List<RingConfig> ConfigDefaults { get; set; } = new List<RingConfig>
        {
            new RingConfig
            {
                PlanetName = "Bylen", // Ares at War Part 2/3
                ModId = "2742647898.sbm",
                RingLongitudeAscendingNode = 0,
                RingInclination = 0,
                RingZones = new List<RingZone>
                {
                    new RingZone
                    {
                        InnerRadius = 740000,
                        OuterRadius = 780000,
                        MaxAsteroidsPerSector = 0
                    },
                    new RingZone
                    {
                        InnerRadius = 880000,
                        OuterRadius = 920000,
                        MaxAsteroidsPerSector = 10
                    },
                    new RingZone
                    {
                        InnerRadius = 980000,
                        OuterRadius = 1020000,
                        MaxAsteroidsPerSector = 0
                    }
                }
            },
            new RingConfig
            {
                PlanetName = "Bylen",
                RingLongitudeAscendingNode = -2.85,
                RingInclination = 22.42,
                RingOuterRadius = 1100000.0,
                RingInnerRadius = 650000.0,
                RingHeight = 3500.0,
                SectorSize = 10000.0,
                MaxAsteroidsPerSector = 50,
                RingZones = new List<RingZone>
                {
                    new RingZone
                    {
                        InnerRadius = 880000,
                        OuterRadius = 920000,
                        MaxAsteroidsPerSector = 10
                    }
                }
            },
            new RingConfig
            {
                PlanetName = "Demus",
                ModId = "1670307722.sbm",
                RingLongitudeAscendingNode = -2.85,
                RingInclination = 22.42,
                RingOuterRadius = 1100000.0,
                RingInnerRadius = 650000.0,
                RingHeight = 3500.0,
                SectorSize = 10000.0,
                MaxAsteroidsPerSector = 50,
                RingZones = new List<RingZone>
                {
                    new RingZone
                    {
                        InnerRadius = 880000,
                        OuterRadius = 920000,
                        MaxAsteroidsPerSector = 10
                    }
                }
            }
        };

        public static Dictionary<string, RingConfig> StoredConfigs = new Dictionary<string, RingConfig>();

        public RingConfig Clone()
        {
            return new RingConfig
            {
                PlanetName = PlanetName,
                ModId = ModId,
                MaxAsteroidSize = MaxAsteroidSize,
                MaxAsteroidsPerSector = MaxAsteroidsPerSector,
                MinAsteroidSize = MinAsteroidSize,
                RingLongitudeAscendingNode = RingLongitudeAscendingNode,
                EarlyLog = EarlyLog,
                Enabled = Enabled,
                EntityMovementThreshold = EntityMovementThreshold,
                RingHeight = RingHeight,
                RingInclination = RingInclination,
                RingInnerRadius = RingInnerRadius,
                RingOuterRadius = RingOuterRadius,
                RingZones = RingZones?.Select(e => new RingZone
                {
                    InnerRadius = e.InnerRadius,
                    OuterRadius = e.OuterRadius,
                    MaxAsteroidsPerSector = e.MaxAsteroidsPerSector,
                    MaxAsteroidSize = e.MaxAsteroidSize,
                    MinAsteroidSize = e.MinAsteroidSize
                }).ToList(),
                SectorSize = SectorSize,
                Vanilla = Vanilla,
            };
        }

        public static RingConfig GetRingConfig(MyPlanet planet)
        {
            var ringConfig = new RingConfig();

            var configFileName = planet.StorageName + ".xml";
            var defConfigFileName = "ringDefaults.xml";
            var modid = planet.Generator.Context.ModId;

            var configs = new List<RingConfig>();

            try
            {
                RingConfig config;

                if (ReadConfig(configFileName, out config))
                {
                    configs.Add(config);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"##MOD: Ring asteroid error reading planet config: {ex}");
            }

            try
            {
                RingConfig config;

                if (ReadConfig(defConfigFileName, out config))
                {
                    configs.Add(config);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"##MOD: Ring asteroid error reading default config: {ex}");
            }

            if (modid != null)
            {
                foreach (var config in ConfigDefaults.Where(e => e.ModId == modid).OrderByDescending(e => e.PlanetName))
                {
                    if (planet.StorageName.StartsWith(config.PlanetName))
                    {
                        configs.Add(config);
                    }
                }
            }
            else
            {
                foreach (var config in ConfigDefaults.Where(e => e.Vanilla == true).OrderByDescending(e => e.PlanetName))
                {
                    if (planet.StorageName.StartsWith(config.PlanetName))
                    {
                        configs.Add(config);
                    }
                }
            }

            foreach (var config in ConfigDefaults.Where(e => e.ModId == null).OrderByDescending(e => e.PlanetName))
            {
                if (planet.StorageName.StartsWith(config.PlanetName))
                {
                    configs.Add(config);
                }
            }

            configs.Add(GlobalDefault);

            foreach (var config in configs)
            {
                if (config.ModId == null || modid != null && config.ModId == modid || modid == null && config.Vanilla == true)
                {
                    ringConfig.PlanetName                 = ringConfig.PlanetName                 ?? config.PlanetName;
                    ringConfig.RingInclination            = ringConfig.RingInclination            ?? config.RingInclination;
                    ringConfig.RingInnerRadius            = ringConfig.RingInnerRadius            ?? config.RingInnerRadius;
                    ringConfig.RingOuterRadius            = ringConfig.RingOuterRadius            ?? config.RingOuterRadius;
                    ringConfig.MaxAsteroidSize            = ringConfig.MaxAsteroidSize            ?? config.MaxAsteroidSize;
                    ringConfig.MinAsteroidSize            = ringConfig.MinAsteroidSize            ?? config.MinAsteroidSize;
                    ringConfig.SectorSize                 = ringConfig.SectorSize                 ?? config.SectorSize;
                    ringConfig.EntityMovementThreshold    = ringConfig.EntityMovementThreshold    ?? config.EntityMovementThreshold;
                    ringConfig.MaxAsteroidsPerSector      = ringConfig.MaxAsteroidsPerSector      ?? config.MaxAsteroidsPerSector;
                    ringConfig.RingHeight                 = ringConfig.RingHeight                 ?? config.RingHeight;
                    ringConfig.RingLongitudeAscendingNode = ringConfig.RingLongitudeAscendingNode ?? config.RingLongitudeAscendingNode;
                    ringConfig.SizeExponent               = ringConfig.SizeExponent               ?? config.SizeExponent;
                    ringConfig.RingZones                  = ringConfig.RingZones                  ?? config.RingZones;
                    ringConfig.TaperRingEdge              = ringConfig.TaperRingEdge              ?? config.TaperRingEdge;
                    ringConfig.Enabled                    = ringConfig.Enabled                    ?? config.Enabled;
                    ringConfig.EarlyLog                   = ringConfig.EarlyLog                   ?? config.EarlyLog;
                    ringConfig.LogDebug                   = ringConfig.LogDebug                   ?? config.LogDebug;
                }
            }

            if (ringConfig.PlanetName != null)
            {
                var exampleConfig = ringConfig.Clone();
                exampleConfig.PlanetName = planet.StorageName;
                exampleConfig.Enabled = exampleConfig.Enabled ?? false;
                exampleConfig.EarlyLog = exampleConfig.EarlyLog ?? false;
                exampleConfig.Vanilla = exampleConfig.Vanilla ?? false;
                exampleConfig.SizeExponent = exampleConfig.SizeExponent ?? 2.0;

                exampleConfig.RingZones = exampleConfig.RingZones ?? new List<RingZone>();

                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFileName + ".example", typeof(RingAsteroidsComponent)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(exampleConfig));
                }
            }

            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("ringDefaults.xml.example", typeof(RingAsteroidsComponent)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(GlobalDefault));
            }

            return ringConfig;
        }

        private static bool ReadConfig(string configFileName, out RingConfig config)
        {
            lock (StoredConfigs)
            {
                if (StoredConfigs.TryGetValue(configFileName, out config))
                {
                    return true;
                }

                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configFileName, typeof(RingAsteroidsComponent)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFileName, typeof(RingAsteroidsComponent)))
                    {
                        config = MyAPIGateway.Utilities.SerializeFromXML<RingConfig>(reader.ReadToEnd());
                        StoredConfigs[configFileName] = config;
                        return true;
                    }
                }

                return false;
            }
        }

        public static void SaveConfigs()
        {
            lock (StoredConfigs)
            {
                foreach (var kvp in StoredConfigs)
                {
                    if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(kvp.Key, typeof(RingAsteroidsComponent)))
                    {
                        using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(kvp.Key, typeof(RingAsteroidsComponent)))
                        {
                            writer.Write(MyAPIGateway.Utilities.SerializeToXML(kvp.Value));
                        }
                    }
                }
            }
        }
    }
}
