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
        public int? VoxelGeneratorVersion { get; set; }
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

        public static List<RingConfig> SBCStoredConfigs = null;

        public static RingConfig SBCStoredDefaultConfig = null;

        public static Dictionary<string, RingAsteroidsComponent> PlanetRingComponents = new Dictionary<string, RingAsteroidsComponent>();

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
                ExclusionZoneSize = ExclusionZoneSize,
                ExclusionZoneSizeMult = ExclusionZoneSizeMult,
                VoxelGeneratorVersion = VoxelGeneratorVersion,
                RingZones = RingZones?.Select(e => new RingZone
                {
                    InnerRadius = e.InnerRadius,
                    OuterRadius = e.OuterRadius,
                    MaxAsteroidsPerSector = e.MaxAsteroidsPerSector,
                    MaxAsteroidSize = e.MaxAsteroidSize,
                    MinAsteroidSize = e.MinAsteroidSize
                }).ToList(),
                LogDebug = LogDebug,
                SizeExponent = SizeExponent,
                TaperRingEdge = TaperRingEdge,
                SectorSize = SectorSize,
                Vanilla = Vanilla,
            };
        }

        public static void LoadSBCStoredConfigs()
        {
            List<RingConfig> configList;

            if (MyAPIGateway.Utilities.GetVariable("SERingAsteroids_RingConfigs", out configList))
            {
                SBCStoredConfigs = configList;
            }
            else
            {
                SBCStoredConfigs = new List<RingConfig>();
            }

            RingConfig config;

            if (MyAPIGateway.Utilities.GetVariable("SERingAsteroids_DefaultRingConfig", out config))
            {
                SBCStoredDefaultConfig = config;
            }
        }

        public static void SaveSBCStoredConfigs()
        {
            if (SBCStoredConfigs != null && SBCStoredConfigs.Count > 0)
            {
                MyAPIGateway.Utilities.SetVariable("SERingAsteroids_RingConfigs", SBCStoredConfigs);
            }

            if (SBCStoredDefaultConfig != null)
            {
                MyAPIGateway.Utilities.SetVariable("SERingAsteroids_DefaultRingConfig", SBCStoredDefaultConfig);
            }
        }

        private static void AddOrUpdateSBCStoredConfig(RingConfig config)
        {
            var sbcConfigIndex = SBCStoredConfigs.FindIndex(e => e.PlanetName == config.PlanetName && e.ModId == config.ModId && e.Vanilla == config.Vanilla);
            var sbcConfig = sbcConfigIndex >= 0 ? SBCStoredConfigs[sbcConfigIndex] : null;

            if (!config.Equals(sbcConfig))
            {
                if (sbcConfigIndex >= 0)
                {
                    SBCStoredConfigs[sbcConfigIndex] = config;
                }
                else
                {
                    SBCStoredConfigs.Add(config);
                }

                SaveSBCStoredConfigs();
            }
        }

        public static RingConfig GetRingConfig(MyPlanet planet, RingAsteroidsComponent component)
        {
            PlanetRingComponents[planet.StorageName] = component;

            var ringConfig = new RingConfig();

            var configFileName = planet.StorageName + ".xml";
            var defConfigFileName = "ringDefaults.xml";
            var modid = planet.Generator.Context.ModId;

            var configs = new List<RingConfig>();

            if (SBCStoredConfigs == null)
            {
                LoadSBCStoredConfigs();
            }

            try
            {
                RingConfig config;

                if (ReadConfig(configFileName, out config))
                {
                    configs.Add(config);
                    config.PlanetName = planet.StorageName;

                    if (config.ModId == null && config.Vanilla != true)
                    {
                        config.ModId = modid;
                    }

                    if (config.Vanilla == null && config.ModId == null)
                    {
                        config.Vanilla = modid == null;
                    }

                    AddOrUpdateSBCStoredConfig(config);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"##MOD: Ring asteroid error reading planet config: {ex}");
            }

            if (modid != null)
            {
                foreach (var config in SBCStoredConfigs.Where(e => e.ModId == modid).OrderByDescending(e => e.PlanetName))
                {
                    if (planet.StorageName.StartsWith(config.PlanetName))
                    {
                        configs.Add(config);
                    }
                }
            }
            else
            {
                foreach (var config in SBCStoredConfigs.Where(e => e.Vanilla == true).OrderByDescending(e => e.PlanetName))
                {
                    if (planet.StorageName.StartsWith(config.PlanetName))
                    {
                        configs.Add(config);
                    }
                }
            }

            try
            {
                RingConfig config;

                if (ReadConfig(defConfigFileName, out config))
                {
                    config.PlanetName = null;
                    config.Vanilla = null;
                    config.ModId = null;
                    config.Enabled = null;
                    configs.Add(config);
                    SBCStoredDefaultConfig = config;
                    SaveSBCStoredConfigs();
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"##MOD: Ring asteroid error reading default config: {ex}");
            }

            if (SBCStoredDefaultConfig != null)
            {
                configs.Add(SBCStoredDefaultConfig);
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
                    ringConfig.ExclusionZoneSize          = ringConfig.ExclusionZoneSize          ?? config.ExclusionZoneSize;
                    ringConfig.ExclusionZoneSizeMult      = ringConfig.ExclusionZoneSizeMult      ?? config.ExclusionZoneSizeMult;
                    ringConfig.RingZones                  = ringConfig.RingZones                  ?? config.RingZones;
                    ringConfig.TaperRingEdge              = ringConfig.TaperRingEdge              ?? config.TaperRingEdge;
                    ringConfig.EarlyLog                   = ringConfig.EarlyLog                   ?? config.EarlyLog;
                    ringConfig.LogDebug                   = ringConfig.LogDebug                   ?? config.LogDebug;

                    if (config.PlanetName != null)
                        ringConfig.Enabled                = ringConfig.Enabled                    ?? config.Enabled;
                }
            }

            if (ringConfig.PlanetName != null)
            {
                var exampleConfig = ringConfig.Clone();
                exampleConfig.PlanetName = planet.StorageName;
                exampleConfig.ModId = modid;
                exampleConfig.Enabled = exampleConfig.Enabled ?? false;
                exampleConfig.Vanilla = exampleConfig.Vanilla ?? (modid == null);
                exampleConfig.SizeExponent = exampleConfig.SizeExponent ?? 2.0;

                exampleConfig.RingZones = exampleConfig.RingZones ?? new List<RingZone>();

                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFileName + ".example", typeof(RingAsteroidsComponent)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(exampleConfig));
                }
            }

            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("ringDefaults.xml.example", typeof(RingAsteroidsComponent)))
            {
                var exampleDefaultConfig = GlobalDefault.Clone();
                exampleDefaultConfig.Enabled = null;
                exampleDefaultConfig.PlanetName = null;
                exampleDefaultConfig.ModId = null;
                exampleDefaultConfig.Vanilla = null;

                writer.Write(MyAPIGateway.Utilities.SerializeToXML(exampleDefaultConfig));
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

        private static readonly Dictionary<string, string> PropNameShortestPrefixes = new Dictionary<string, string>
        {
            ["ena"] = "enabled",
            ["ea"] = "earlylog",
            ["el"] = "earlylog",
            ["log"] = "logdebug",
            ["ld"] = "logdebug",
            ["taper"] = "taperringedge",
            ["ringo"] = "ringouterradius",
            ["out"] = "ringouterradius",
            ["or"] = "ringouterradius",
            ["ringi"] = "ringinnerradius",
            ["inn"] = "ringinnerradius",
            ["ir"] = "ringinnerradius",
            ["ringh"] = "ringheight",
            ["ht"] = "ringheight",
            ["secsz"] = "sectorsize",
            ["maxpersec"] = "maxasteroidspersector",
            ["lan"] = "ringlongitudeascendingnode",
            ["inc"] = "ringinclination",
            ["minsz"] = "minasteroidsize",
            ["maxsz"] = "maxasteroidsize",
            ["entmov"] = "entitymovementthreshold",
            ["thres"] = "entitymovementthreshold",
            ["szexp"] = "sizeexponent",
            ["xzsiz"] = "exclusionzonesize",
            ["xzmul"] = "exclusionzonesizemult"
        };

        public static void UpdateConfig(MyPlanet planet, string propname, string strvalue)
        {
            propname = propname.ToLowerInvariant();
            propname = PropNameShortestPrefixes.OrderBy(e => e.Key).FirstOrDefault(e => propname.StartsWith(e.Key)).Value ?? propname;

            if (planet == null)
                return;

            if (SBCStoredConfigs == null)
            {
                LoadSBCStoredConfigs();
            }

            var modid = planet.Generator.Context.ModId;

            var config = SBCStoredConfigs.FirstOrDefault(e => e.PlanetName == planet.StorageName && e.ModId == modid && e.Vanilla == (modid == null));

            if (config == null)
            {
                config = new RingConfig
                {
                    PlanetName = planet.StorageName,
                    ModId = modid,
                    Vanilla = modid == null
                };

                SBCStoredConfigs.Add(config);
            }

            bool? boolval = null;
            double? doubleval = null;
            double dblval;

            if (double.TryParse(strvalue, out dblval))
            {
                doubleval = dblval;
            }

            if (strvalue?.ToLowerInvariant() == "yes" || strvalue?.ToLowerInvariant() == "true" || doubleval > 0)
            {
                boolval = true;
            }
            else if (strvalue?.ToLowerInvariant() == "no" || strvalue?.ToLowerInvariant() == "false" || doubleval <= 0)
            {
                boolval = false;
            }

            if (boolval != null || string.IsNullOrEmpty(strvalue))
            {
                switch (propname.ToLowerInvariant())
                {
                    case "taperringedge": config.TaperRingEdge = boolval; break;
                    case "enabled": config.Enabled = boolval; break;
                    case "earlylog": config.EarlyLog = boolval; break;
                    case "logdebug": config.LogDebug = boolval; break;
                }
            }

            if (doubleval != null || string.IsNullOrEmpty(strvalue))
            {
                switch (propname.ToLowerInvariant())
                {
                    case "ringouterradius": config.RingOuterRadius = doubleval; break;
                    case "ringinnerradius": config.RingInnerRadius = doubleval; break;
                    case "ringheight": config.RingHeight = doubleval; break;
                    case "sectorsize": config.SectorSize = doubleval; break;
                    case "maxasteroidspersector": config.MaxAsteroidsPerSector = (int?)doubleval; break;
                    case "ringlongitudeascendingnode": config.RingLongitudeAscendingNode = doubleval; break;
                    case "ringinclination": config.RingInclination = doubleval; break;
                    case "minasteroidsize": config.MinAsteroidSize = doubleval; break;
                    case "maxasteroidsize": config.MaxAsteroidSize = doubleval; break;
                    case "entitymovementthreshold": config.EntityMovementThreshold = doubleval; break;
                    case "sizeexponent": config.SizeExponent = doubleval; break;
                    case "exclusionzonesize": config.ExclusionZoneSize = doubleval; break;
                    case "exclusionzonesizemult": config.ExclusionZoneSizeMult = doubleval; break;
                }
            }
        }

        public static void RequestReload(MyPlanet planet, bool? enabled)
        {
            RingAsteroidsComponent component;

            if (planet != null && PlanetRingComponents.TryGetValue(planet.StorageName, out component))
            {
                if (enabled != null)
                {
                    UpdateConfig(planet, "enabled", enabled.ToString());
                }

                SaveConfigs();

                component.RequestReload();
            }
        }

        private static bool SequenceEquals<T>(List<T> x, List<T> y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.Count != y.Count) return false;
            return x.SequenceEqual(y);
        }

        public override bool Equals(object obj)
        {
            RingConfig other = obj as RingConfig;
            return !ReferenceEquals(other, null) &&
                   PlanetName == other.PlanetName &&
                   ModId == other.ModId &&
                   Vanilla == other.Vanilla &&
                   RingOuterRadius == other.RingOuterRadius &&
                   RingInnerRadius == other.RingInnerRadius &&
                   RingHeight == other.RingHeight &&
                   SectorSize == other.SectorSize &&
                   MaxAsteroidsPerSector == other.MaxAsteroidsPerSector &&
                   RingLongitudeAscendingNode == other.RingLongitudeAscendingNode &&
                   RingInclination == other.RingInclination &&
                   MinAsteroidSize == other.MinAsteroidSize &&
                   MaxAsteroidSize == other.MaxAsteroidSize &&
                   EntityMovementThreshold == other.EntityMovementThreshold &&
                   SizeExponent == other.SizeExponent &&
                   ExclusionZoneSize == other.ExclusionZoneSize &&
                   ExclusionZoneSizeMult == other.ExclusionZoneSizeMult &&
                   VoxelGeneratorVersion == other.VoxelGeneratorVersion &&
                   SequenceEquals(RingZones, other.RingZones) &&
                   TaperRingEdge == other.TaperRingEdge &&
                   Enabled == other.Enabled &&
                   EarlyLog == other.EarlyLog &&
                   LogDebug == other.LogDebug;
        }

        public override int GetHashCode()
        {
            int hashCode = -843855945;
            hashCode = hashCode * -1521134295 + PlanetName?.GetHashCode() ?? 0;
            hashCode = hashCode * -1521134295 + ModId?.GetHashCode() ?? 0;
            hashCode = hashCode * -1521134295 + Vanilla.GetHashCode();
            hashCode = hashCode * -1521134295 + RingOuterRadius.GetHashCode();
            hashCode = hashCode * -1521134295 + RingInnerRadius.GetHashCode();
            hashCode = hashCode * -1521134295 + RingHeight.GetHashCode();
            hashCode = hashCode * -1521134295 + SectorSize.GetHashCode();
            hashCode = hashCode * -1521134295 + MaxAsteroidsPerSector.GetHashCode();
            hashCode = hashCode * -1521134295 + RingLongitudeAscendingNode.GetHashCode();
            hashCode = hashCode * -1521134295 + RingInclination.GetHashCode();
            hashCode = hashCode * -1521134295 + MinAsteroidSize.GetHashCode();
            hashCode = hashCode * -1521134295 + MaxAsteroidSize.GetHashCode();
            hashCode = hashCode * -1521134295 + EntityMovementThreshold.GetHashCode();
            hashCode = hashCode * -1521134295 + SizeExponent.GetHashCode();
            hashCode = hashCode * -1521134295 + ExclusionZoneSize.GetHashCode();
            hashCode = hashCode * -1521134295 + ExclusionZoneSizeMult.GetHashCode();
            hashCode = hashCode * -1521134295 + VoxelGeneratorVersion.GetHashCode();

            if (RingZones != null)
            {
                hashCode = hashCode * -1521134295 + RingZones.Count.GetHashCode();

                foreach (var zone in RingZones)
                {
                    hashCode = hashCode * -1521134295 + zone.GetHashCode();
                }
            }

            hashCode = hashCode * -1521134295 + TaperRingEdge.GetHashCode();
            hashCode = hashCode * -1521134295 + Enabled.GetHashCode();
            hashCode = hashCode * -1521134295 + EarlyLog.GetHashCode();
            hashCode = hashCode * -1521134295 + LogDebug.GetHashCode();
            return hashCode;
        }
    }
}
