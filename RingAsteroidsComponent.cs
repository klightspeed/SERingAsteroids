using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SERingAsteroids.OctreeStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace SERingAsteroids
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Planet), false)]
    public class RingAsteroidsComponent : MyGameLogicComponent
    {
        private double _ringOuterRadius;
        private double _ringInnerRadius;
        private double _ringHeight;
        private double _sectorSize;
        private int _maxAsteroidsPerSector;
        private double _ringLongitudeAscendingNode;
        private double _ringInclination;
        private double _minAsteroidSize;
        private double _maxAsteroidSize;
        private double _entityMovementThreshold;
        private double _sizeExponent = 2.0;
        private double _exclusionZone = 64.0;
        private double _exclusionZoneMult = 1.5;
        private int _voxelGeneratorVersion;
        private bool _taperRingEdge;
        private bool _logDebug;
        private bool _includeNameInSeed;
        private bool _disableCleanup;
        private bool _disableSaveLimit;
        private bool _limitPhysics;
        private readonly List<RingZone> _ringZones = new List<RingZone>();

        private MatrixD _ringMatrix;
        private MatrixD _ringInvMatrix;
        private BoundingBoxD _ringBoundingBox;
        private int _minRingSectorY;
        private int _maxRingSectorY;
        private string logfilename;
        private TextWriter logfile;
        private MyPlanet _planet;
        private bool _processing;
        private bool _reloadRequired;
        private int _lastPersistentVoxelMapCount;
        private int _lastManagedVoxelMapCount;
        private double _saveDistance;
        private double _playerSpawnDistance;
        private double _gridSpawnDistance;
        private double _physicsDistance;

        private readonly Queue<string> loglines = new Queue<string>();
        private readonly Dictionary<long, Vector3D> _entityPositions = new Dictionary<long, Vector3D>();
        private readonly Dictionary<long, IMyVoxelBase> _voxelMaps = new Dictionary<long, IMyVoxelBase>();
        private readonly Dictionary<string, IMyVoxelBase> _voxelMapsByName = new Dictionary<string, IMyVoxelBase>();
        private readonly Dictionary<long, Vector2I> _voxelMapSectors = new Dictionary<long, Vector2I>();
        private readonly Dictionary<long, Vector2I> _entitySectors = new Dictionary<long, Vector2I>();
        private readonly Dictionary<Vector2I, HashSet<long>> _ringSectorVoxelMaps = new Dictionary<Vector2I, HashSet<long>>();
        private readonly Dictionary<Vector2I, int> _ringSectorSeeds = new Dictionary<Vector2I, int>();
        private readonly Dictionary<Vector2I, int> _ringSectorMaxAsteroids = new Dictionary<Vector2I, int>();
        private readonly Dictionary<Vector2I, Dictionary<string, ProceduralVoxelDetails>> _voxelCreationDetails = new Dictionary<Vector2I, Dictionary<string, ProceduralVoxelDetails>>();
        private readonly HashSet<Vector2I> _ringSectorsCompleted = new HashSet<Vector2I>();
        private Queue<MyTuple<ProceduralVoxelDetails, double, double>> _addVoxelsByDistance = new Queue<MyTuple<ProceduralVoxelDetails, double, double>>();
        private List<MyTuple<ProceduralVoxelDetails, double, double>> _voxelsByDistance = new List<MyTuple<ProceduralVoxelDetails, double, double>>();
        private Queue<MyTuple<ProceduralVoxelDetails, double, double>> _delVoxelsByDistance = new Queue<MyTuple<ProceduralVoxelDetails, double, double>>();

        private readonly object _loggerLock = new object();

        private void LogDebug(string str)
        {
            if (_logDebug)
            {
                Log(str);
            }
        }

        private void Log(string str)
        {
            lock (_loggerLock)
            {
                try
                {
                    if (logfile == null)
                    {
                        loglines.Enqueue($"[{DateTime.Now:HH:mm:ss:ffff}][{Entity.EntityId}][{_planet?.StorageName}] {str}");
                    }
                    else
                    {
                        string qline;

                        while (loglines.TryDequeue(out qline))
                        {
                            logfile.WriteLine(qline);
                        }

                        logfile.WriteLine($"[{DateTime.Now:HH:mm:ss:ffff}][{Entity.EntityId}][{_planet?.StorageName}] {str}");
                        logfile.Flush();
                    }
                }
                catch
                { }
            }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            _planet = Entity as MyPlanet;

            if (_planet == null)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void Close()
        {
            if (logfile != null)
            {
                logfile.Close();
                logfile = null;
            }

            base.Close();
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (_planet == null)
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            if (_processing || _planet == null)
                return;

            ReloadConfig();
        }

        private void ReloadConfig()
        {
            _reloadRequired = false;

            var config = RingConfig.GetRingConfig(_planet, this);

            if (config.DebugDrawRingBounds == true)
            {
                SessionComponent.AddOrUpdateShownRing(config);
            }
            else
            {
                SessionComponent.RemoveShownRing(_planet.StorageName);
            }

            if (config.EarlyLog == true || config.Enabled == true)
            {
                if (logfile == null)
                {
                    logfilename = $"{typeof(RingAsteroidsComponent).Name}-{Entity.EntityId}-{DateTime.Now:yyyyMMddHHmmss}.log";
                    logfile = MyAPIGateway.Utilities.WriteFileInLocalStorage(logfilename, typeof(RingAsteroidsComponent));
                }
            }

            Log($"Planet {_planet.StorageName}");
            Log($"Enabled: {config.Enabled?.ToString() ?? "not set"}");
            Log($"Debug log: {config.LogDebug?.ToString() ?? "not set"}");

            if (config.Enabled != true)
            {
                Log("Ring asteroids disabled for this planet");
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            if (!config.IsConfigComplete)
            {
                foreach (var propname in config.GetMissingValueNames())
                {
                    Log($"Missing property value: {propname}");
                }

                Log("Ring asteroids disabled for this planet");
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            _ringInnerRadius = config.RingInnerRadius.Value;
            _ringOuterRadius = config.RingOuterRadius.Value;
            _ringHeight = config.RingHeight.Value;
            _ringInclination = config.RingInclination.Value;
            _ringLongitudeAscendingNode = config.RingLongitudeAscendingNode.Value;
            _sectorSize = config.SectorSize.Value;
            _maxAsteroidsPerSector = config.MaxAsteroidsPerSector.Value;
            _minAsteroidSize = config.MinAsteroidSize.Value;
            _maxAsteroidSize = config.MaxAsteroidSize.Value;
            _entityMovementThreshold = config.EntityMovementThreshold.Value;
            _sizeExponent = config.SizeExponent ?? 1.0;
            _taperRingEdge = config.TaperRingEdge ?? true;
            _exclusionZone = config.ExclusionZoneSize ?? _minAsteroidSize;
            _exclusionZoneMult = config.ExclusionZoneSizeMult ?? 1.5;
            _ringMatrix = config.GetRingMatrix();
            _voxelGeneratorVersion = config.VoxelGeneratorVersion ?? MyAPIGateway.Session.SessionSettings.VoxelGeneratorVersion;
            _logDebug = config.LogDebug ?? false;
            _includeNameInSeed = config.IncludePlanetNameInRandomSeed ?? false;
            _disableCleanup = config.DisableAsteroidCleanup ?? false;
            _disableSaveLimit = config.DisableReducedSaveDistance ?? false;
            _limitPhysics = config.DisablePhysicsIfOutOfRange ?? false;
            _saveDistance = config.AsteroidSaveDistance ?? MyAPIGateway.Session.SessionSettings.SyncDistance * 1.2;
            _playerSpawnDistance = config.AsteroidPlayerSpawnDistance ?? MyAPIGateway.Session.SessionSettings.ViewDistance * 1.2;
            _gridSpawnDistance = config.AsteroidGridSpawnDistance ?? 1200;
            _physicsDistance = config.AsteroidPhysicsDistance ?? 1200;

            if (config.RingZones != null)
            {
                _ringZones.Clear();

                foreach (var zone in config.RingZones)
                {
                    _ringZones.Add(new RingZone
                    {
                        InnerRadius = Math.Floor(zone.InnerRadius / _sectorSize + 0.5),
                        OuterRadius = Math.Floor(zone.OuterRadius / _sectorSize + 0.5),
                        MaxAsteroidsPerSector = zone.MaxAsteroidsPerSector
                    });
                }
            }

            _ringInnerRadius = Math.Floor(_ringInnerRadius / _sectorSize + 0.5) * _sectorSize;
            _ringOuterRadius = Math.Floor(_ringOuterRadius / _sectorSize + 0.5) * _sectorSize;
            _minRingSectorY = (int)(_ringInnerRadius / _sectorSize);
            _maxRingSectorY = (int)(_ringOuterRadius / _sectorSize) - 1;

            var planetPos = _planet.PositionComp.GetPosition();
            var ringbbmax = new Vector3D(_ringOuterRadius * 2, _ringOuterRadius * 2, _ringOuterRadius * 2);
            _ringInvMatrix = MatrixD.Invert(_ringMatrix);
            _ringBoundingBox = new BoundingBoxD(planetPos - ringbbmax, planetPos + ringbbmax);

            LogDebug($"Planet Position: {planetPos}");
            LogDebug($"Matrix: {_ringMatrix}");
            LogDebug($"Inv Matrix: {_ringInvMatrix}");
            LogDebug($"Bounding Box: {_ringBoundingBox}");

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public void RequestReload()
        {
            _reloadRequired = true;
        }

        private Vector2I GetRingSectorForPosition(Vector3D position, string friendlyName)
        {
            var planetLocalPosition = position - _planet.PositionComp.GetPosition();
            var ringLocalPosition = Vector3D.Transform(position, _ringInvMatrix);
            var ringXZPosition = new Vector2D(ringLocalPosition.X, ringLocalPosition.Z);
            Vector2I ringSector = default(Vector2I);

            var radius_sq = ringXZPosition.LengthSquared();
            var innerRad = Math.Max(_ringInnerRadius - 75000, 0);
            var outerRad = _ringOuterRadius + 75000;
            var ringHeight = _ringHeight + 75000;

            if (radius_sq > innerRad * innerRad)
            {
                var planetLon = Math.Atan2(planetLocalPosition.Z, planetLocalPosition.X) * 180 / Math.PI;
                var planetLat = Math.Atan(planetLocalPosition.Y / Math.Sqrt(planetLocalPosition.X * planetLocalPosition.X + planetLocalPosition.Z * planetLocalPosition.Z)) * 180 / Math.PI;
                var ringLon = Math.Atan2(ringLocalPosition.Z, ringLocalPosition.X) * 180 / Math.PI;
                if (ringLon < 0)
                    ringLon += 360;

                if (radius_sq >= innerRad * innerRad && radius_sq <= outerRad * outerRad && Math.Abs(ringLocalPosition.Y) < ringHeight)
                {
                    var radius = Math.Sqrt(radius_sq);
                    var sectorRadius = (int)Math.Floor(radius / _sectorSize);
                    var longitude = Math.Floor(Math.Atan2(ringLocalPosition.Z, ringLocalPosition.X) * sectorRadius * 3 / Math.PI + 0.5);
                    if (longitude < 0)
                        longitude += sectorRadius * 6;
                    ringSector = new Vector2I((int)longitude, sectorRadius);
                }

                LogDebug(
                    $"{friendlyName} " +
                    $"at X:{position.X:F3} Y:{position.Y:F3} Z:{position.Z:F3} " +
                    $"ring X:{ringLocalPosition.X:F3} Y:{ringLocalPosition.Y:F3} Z:{ringLocalPosition.Z:F3} " +
                    $"rad {(Math.Sqrt(radius_sq) / _sectorSize):F3} phi {ringLon:F3} h {ringLocalPosition.Y:F3} " +
                    $"sector {ringSector}");
            }

            return ringSector;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (_processing || _planet == null)
                return;

            if (_reloadRequired)
            {
                Log("Reloading config");
                ReloadConfig();
            }

            var entities = new List<MyEntity>();
            var voxelmaps = new List<IMyVoxelBase>();
            var players = new List<IMyPlayer>();
            var gridBlocks = new Dictionary<long, List<IMySlimBlock>>();
            var jumpDrives = new Dictionary<long, IMyJumpDrive>();
            var jumpTargets = new Dictionary<long, Vector3D?>();
            var processingStart = DateTime.UtcNow;

            try
            {
                MyGamePruningStructure.GetTopMostEntitiesInBox(ref _ringBoundingBox, entities);
                MyAPIGateway.Session.VoxelMaps.GetInstances(voxelmaps);
                MyAPIGateway.Players.GetPlayers(players);

                foreach (var entity in entities)
                {
                    if (entity is IMyCubeGrid)
                    {
                        var grid = (IMyCubeGrid)entity;
                        var blocks = new List<IMySlimBlock>();
                        grid.GetBlocks(blocks);
                        gridBlocks[grid.EntityId] = blocks;

                        if (grid.JumpSystem?.IsJumping == true)
                        {
                            jumpDrives[grid.EntityId] = grid.GetFatBlocks<IMyJumpDrive>().FirstOrDefault();
                            jumpTargets[grid.EntityId] = grid.JumpSystem.GetJumpDriveTarget();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"##MOD: Ring asteroid error: {ex}");
                MyLog.Default.WriteLineAndConsole($"##MOD: Ring asteroid error: {ex}");
                return;
            }

            MyAPIGateway.Parallel.StartBackground(() =>
            {
                _processing = true;

                try
                {
                    GetVoxelMaps(voxelmaps);

                    var persistentVoxelMapCount = _voxelMaps.Values.Count(e => e.Closed == false && e.Save == true);
                    var managedVoxelMapCount = _voxelsByDistance.Select(e => e.Item1).Count(e => e.VoxelMap?.Closed == false);

                    Dictionary<Vector2I, List<IMyEntity>> entitySectors;
                    var sectorsToProcess = GetEntityMovements(entities, jumpDrives, jumpTargets, out entitySectors);
                    sectorsToProcess = GetSectorsToProcess(sectorsToProcess);

                    foreach (var sector in sectorsToProcess)
                    {
                        AddAsteroidsToSector(sector);
                    }

                    OrderPendingAsteroidsByDistance(entitySectors, gridBlocks, players);

                    if (persistentVoxelMapCount != _lastPersistentVoxelMapCount ||
                        managedVoxelMapCount < _lastManagedVoxelMapCount - 50 ||
                        managedVoxelMapCount > _lastManagedVoxelMapCount + 50)
                    {
                        var voxelMapCount = _voxelMaps.Values.Count(e => e.Closed == false);

                        MyLog.Default.Debug("RingAsteroidComponent: {0}: {1} persistent voxel maps in world ({2} total)", _planet.StorageName, persistentVoxelMapCount, voxelMapCount);
                        LogDebug($"{persistentVoxelMapCount} persistent voxel maps in world ({voxelMapCount} total)");

                        var persistentManagedVoxelMapCount = _voxelsByDistance.Select(e => e.Item1).Count(e => e.VoxelMap?.Save == true && e.VoxelMap?.Closed == false);
                        MyLog.Default.Debug("RingAsteroidComponent: {0}: {1} persistent voxel maps managed by mod ({2} total)", _planet.StorageName, persistentManagedVoxelMapCount, managedVoxelMapCount);
                        LogDebug($"{persistentManagedVoxelMapCount} persistent voxel maps managed by mod ({managedVoxelMapCount} total)");

                        var voxelMapsOutsideRange = _voxelsByDistance.Count(e => e.Item2 > 5000 && e.Item1.VoxelMap?.Save == true && e.Item1.VoxelMap?.Closed == false);
                        MyLog.Default.Debug("RingAsteroidComponent: {0}: {1} persistent voxel maps outside 5km", _planet.StorageName, voxelMapsOutsideRange);
                        LogDebug($"{voxelMapsOutsideRange} persistent voxel maps outside 5km");

                        var now = DateTime.UtcNow;
                        var timeTaken = now - processingStart;

                        MyLog.Default.Debug("RingAsteroidComponent: {0}: Processing took {1} seconds", _planet.StorageName, timeTaken.TotalSeconds);
                        LogDebug($"Processing took {timeTaken.TotalSeconds} seconds");

                        _lastPersistentVoxelMapCount = persistentVoxelMapCount;
                        _lastManagedVoxelMapCount = managedVoxelMapCount;
                    }

                    if (_addVoxelsByDistance.Count != 0)
                    {
                        MyLog.Default.Debug("RingAsteroidComponent: {0}: {1} asteroids to add", _planet.StorageName, _addVoxelsByDistance.Count);
                        LogDebug($"{_addVoxelsByDistance.Count} asteroids to add");
                    }

                    if (_delVoxelsByDistance.Count != 0)
                    {
                        MyLog.Default.Debug("RingAsteroidComponent: {0}: {1} asteroids to delete", _planet.StorageName, _delVoxelsByDistance.Count);
                        LogDebug($"{_delVoxelsByDistance.Count} asteroids to delete");
                    }

                    MyTuple<ProceduralVoxelDetails, double, double> voxelDetails;

                    while (SessionComponent.VoxelAddQueueLength < 50 && _addVoxelsByDistance.TryDequeue(out voxelDetails))
                    {
                        LogDebug($"Queueing create for asteroid {voxelDetails.Item1.Name} (Dist={voxelDetails.Item2})");
                        SessionComponent.EnqueueVoxelAdd(voxelDetails.Item1);
                    }

                    while (SessionComponent.VoxelDelQueueLength < 50 && _delVoxelsByDistance.TryDequeue(out voxelDetails))
                    {
                        LogDebug($"Queueing delete for asteroid {voxelDetails.Item1.VoxelMap.EntityId} [{voxelDetails.Item1.VoxelMap.StorageName}] (Dist={voxelDetails.Item2})");
                        SessionComponent.EnqueueVoxelDelete(voxelDetails.Item1);
                    }
                }
                catch (Exception ex)
                {
                    Log($"##MOD: Ring asteroid error: {ex}");
                    MyLog.Default.WriteLineAndConsole($"##MOD: Ring asteroid error: {ex}");
                    throw;
                }
                finally
                {
                    _processing = false;
                }
            });
        }

        private class AsteroidCreationException : Exception
        {
            public AsteroidCreationException(string message, Exception innerException) : base(message, innerException) { }
        }

        private IMyVoxelMap CreateProceduralAsteroid(int seed, float size, int generatorSeed, Vector3D pos, string name, int generator)
        {
            IMyVoxelMap voxelmap;
#if false
            voxelmap = MyAPIGateway.Session.VoxelMaps.CreateProceduralVoxelMap(seed, size, MatrixD.CreateTranslation(pos));
#else
            var voxelMaterialDefinitions = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();
            var defaultMaterials = 
                voxelMaterialDefinitions
                    .Where(e => e.SpawnsInAsteroids && e.MinVersion <= _voxelGeneratorVersion && e.MaxVersion >= _voxelGeneratorVersion)
                    .Select(e => new OctreeStorage.Chunks.MaterialIndexEntry { Index = e.Index, Name = e.Id.SubtypeName }).ToArray();
            var asteroid = OctreeStorage.OctreeStorage.CreateAsteroid(seed, size, generatorSeed, materials: defaultMaterials);
            var bytes = asteroid.GetBytes();

            IMyStorage storage;

            try
            {
                storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(bytes);
            }
            catch (Exception ex)
            {
                Log($"Error creating asteroid: {ex}");
                Log($"Writing bad asteroid data to {name}");

                using (var writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(name, typeof(RingAsteroidsComponent)))
                {
                    writer.Write(bytes);
                }

                throw new AsteroidCreationException("Error creating asteroid", ex);
            }

            pos -= new Vector3D(storage.Size.X + 1, storage.Size.Y + 1, storage.Size.Z + 1) / 2;

            voxelmap = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(name, storage, pos, 0L);
            MyEntities.RaiseEntityCreated(voxelmap as MyEntity);

            if (!_disableCleanup && !_disableSaveLimit)
                voxelmap.Save = false;
#endif
            LogDebug($"Spawned asteroid {voxelmap.EntityId} [{voxelmap.StorageName}]");

            return voxelmap;
        }

        private void DeleteAsteroid(IMyVoxelMap voxelmap)
        {
            LogDebug($"Deleting asteroid {voxelmap.EntityId} [{voxelmap.StorageName}]");
            voxelmap.Close();
        }

        private void AddAsteroidsToSector(Vector2I sector)
        {
            LogDebug($"Processing sector {sector}");

            int seed;

            if (!_ringSectorSeeds.TryGetValue(sector, out seed))
            {
                var hashcode = sector.GetHashCode();

                if (_includeNameInSeed)
                {
                    hashcode = unchecked(-2018137331 * -1521134295) + hashcode;
                    hashcode = hashcode * -1521134295 + _planet.StorageName.GetHashCode();
                    hashcode = hashcode * -1521134295 + _planet.EntityId.GetHashCode();
                }
                _ringSectorSeeds[sector] = seed = (int)MyHashRandomUtils.JenkinsHash((uint)hashcode);
            }

            HashSet<long> ids;

            if (!_ringSectorVoxelMaps.TryGetValue(sector, out ids))
            {
                _ringSectorVoxelMaps[sector] = ids = new HashSet<long>();
            }

            var random = new Random(seed);
            var maxAsteroidsPerSector = _maxAsteroidsPerSector;
            var minAsteroidSize = _minAsteroidSize;
            var maxAsteroidSize = _maxAsteroidSize;
            var innerRingHeight = _ringHeight;
            var outerRingHeight = _ringHeight;

            if (_taperRingEdge)
            {
                if (sector.Y == _minRingSectorY)
                {
                    innerRingHeight = 0;
                }
                else if (sector.Y == _maxRingSectorY)
                {
                    outerRingHeight = 0;
                }
            }

            var ringHeight = _ringHeight;

            var zone = _ringZones.FirstOrDefault(e => sector.Y < e.OuterRadius && sector.Y + 1 > e.InnerRadius && e.OuterRadius > e.InnerRadius);

            if (zone != null)
            {
                maxAsteroidsPerSector = zone.MaxAsteroidsPerSector ?? maxAsteroidsPerSector;
                minAsteroidSize = zone.MinAsteroidSize ?? minAsteroidSize;
                maxAsteroidSize = zone.MaxAsteroidSize ?? maxAsteroidSize;

                if (zone.MaxAsteroidsPerSector != null)
                {
                    var zoneInnerRingHeight = zone.InnerRingHeight ?? zone.RingHeight ?? innerRingHeight;
                    var zoneOuterRingHeight = zone.OuterRingHeight ?? zone.RingHeight ?? outerRingHeight;
                    ringHeight = (zoneInnerRingHeight + zoneOuterRingHeight) / 2;
                }

                if (zone.InnerRingHeight != null || zone.OuterRingHeight != null)
                {
                    var zoneInnerRingHeight = zone.InnerRingHeight ?? zone.RingHeight ?? innerRingHeight;
                    var zoneOuterRingHeight = zone.OuterRingHeight ?? zone.RingHeight ?? outerRingHeight;
                    var radiusRange = Math.Max(zone.OuterRadius - zone.InnerRadius, 1);
                    var zoneInnerFraction = Math.Min(Math.Max(0, (sector.Y - zone.InnerRadius) / radiusRange), 1);
                    var zoneOuterFraction = Math.Min(Math.Max(0, (sector.Y + 1 - zone.InnerRadius) / radiusRange), 1);
                    innerRingHeight = zoneOuterRingHeight * zoneInnerFraction + zoneInnerRingHeight * (1 - zoneInnerFraction);
                    outerRingHeight = zoneOuterRingHeight * zoneOuterFraction + zoneInnerRingHeight * (1 - zoneOuterFraction);
                }
                else if (zone.TaperEdges == true)
                {
                    if (sector.Y <= zone.InnerRadius && sector.Y + 1 < zone.OuterRadius)
                    {
                        outerRingHeight = zone.RingHeight ?? outerRingHeight;
                    }
                    else if (sector.Y > zone.InnerRadius && sector.Y + 1 >= zone.OuterRadius)
                    {
                        innerRingHeight = zone.RingHeight ?? innerRingHeight;
                    }
                    else
                    {
                        innerRingHeight = zone.RingHeight ?? innerRingHeight;
                        outerRingHeight = zone.RingHeight ?? outerRingHeight;
                    }
                }
                else
                {
                    innerRingHeight = zone.RingHeight ?? innerRingHeight;
                    outerRingHeight = zone.RingHeight ?? outerRingHeight;
                }
            }

            if (ringHeight == 0 || (innerRingHeight == 0 && outerRingHeight == 0))
            {
                maxAsteroidsPerSector = 0;
            }
            else
            {
                maxAsteroidsPerSector *= (int)Math.Floor((innerRingHeight + outerRingHeight) / 2 / ringHeight + 0.5);
            }

            var maxAsteroids = random.Next(maxAsteroidsPerSector / 2, maxAsteroidsPerSector);
            _ringSectorMaxAsteroids[sector] = maxAsteroids;

            int tries = 0;

            Dictionary<string, ProceduralVoxelDetails> sectorVoxels;

            if (!_voxelCreationDetails.TryGetValue(sector, out sectorVoxels))
            {
                _voxelCreationDetails[sector] = sectorVoxels = new Dictionary<string, ProceduralVoxelDetails>();
            }

            while (sectorVoxels.Count < maxAsteroids && tries < maxAsteroidsPerSector * 2 && !SessionComponent.Unloading && !_reloadRequired)
            {
                var relrad = random.NextDouble();

                if (innerRingHeight > outerRingHeight * 1.05 || outerRingHeight > innerRingHeight * 1.05)
                {
                    var a = Math.Min(Math.Max((outerRingHeight - innerRingHeight) / Math.Max(innerRingHeight, outerRingHeight), -1), 1);

                    // inverse of ax^2 + (1 - a)x
                    relrad = (a - 1 + Math.Sqrt(a * a + 4 * a * relrad - 2 * a + 1)) / (2 * a);
                }

                ringHeight = innerRingHeight * (1 - relrad) + outerRingHeight * relrad;

                var logmin = Math.Log(minAsteroidSize);
                var logmax = Math.Log(maxAsteroidSize);

                if (ringHeight * 2 < maxAsteroidSize)
                {
                    logmax = Math.Log(Math.Max(minAsteroidSize, ringHeight * 2));
                }

                var size = (float)Math.Exp(Math.Pow(random.NextDouble(), Math.Abs(_sizeExponent)) * (logmax - logmin) + logmin);

                var rad = relrad + sector.Y;
                var radphi = random.NextDouble() + sector.X - 0.5;
                var phi = radphi / sector.Y * Math.PI / 3;
                var y = (random.NextDouble() - 0.5) * Math.Max(ringHeight * 2 - size, 0);

                var aseed = random.Next();
                var gseed = random.Next();

                var x = rad * _sectorSize * Math.Cos(phi);
                var z = rad * _sectorSize * Math.Sin(phi);

                var pos = Vector3D.Transform(new Vector3D(x, y, z), _ringMatrix);

                var name = $"RingAsteroid_P({_planet.StorageName}-{_planet.EntityId})_{sector.X}_{sector.Y}_{tries}_{aseed}";

                IMyVoxelBase existing;

                var voxelDetails = new ProceduralVoxelDetails
                {
                    Sector = sector,
                    Position = pos,
                    Name = name,
                    Seed = aseed,
                    Size = size,
                    GeneratorSeed = gseed,
                    VoxelGeneratorVersion = _voxelGeneratorVersion,
                    AddAction = CreateProceduralAsteroid,
                    DeleteAction = DeleteAsteroid
                };

                if (!sectorVoxels.ContainsKey(name))
                {
                    if (!_voxelMapsByName.TryGetValue(name, out existing) || existing.Closed)
                    {
                        LogDebug($"Sector {sector}: Adding {size}m asteroid {name} with seed {aseed} at rad:{rad:F3} phi:{(phi * 180 / Math.PI):F3} h:{y:F3} X:{pos.X:F3} Y:{pos.Y:F3} Z:{pos.Z:F3} ({sectorVoxels.Count} / {tries} / {maxAsteroids})");

                        if (CanAddAsteroidToSector(voxelDetails, sectorVoxels))
                        {
                            sectorVoxels[name] = voxelDetails;
                        }
                    }
                    else
                    {
                        LogDebug($"Sector {sector}: Got {size}m asteroid {name} with seed {aseed} at rad:{rad:F3} phi:{(phi * 180 / Math.PI):F3} h:{y:F3} X:{pos.X:F3} Y:{pos.Y:F3} Z:{pos.Z:F3} ({sectorVoxels.Count} / {tries} / {maxAsteroids})");
                        voxelDetails.VoxelMap = existing as IMyVoxelMap;
                        sectorVoxels[name] = voxelDetails;
                    }
                }

                tries++;
            }

            if (!_reloadRequired)
            {
                _ringSectorsCompleted.Add(sector);
            }
        }

        private bool CanAddAsteroidToSector(ProceduralVoxelDetails voxelDetails, Dictionary<string, ProceduralVoxelDetails> sectorVoxels)
        {
            var overlapRadius = voxelDetails.Size * Math.Max(1.0, _exclusionZoneMult) / 2 + Math.Max(0, _exclusionZone);
            var sphere = new BoundingSphereD(voxelDetails.Position, overlapRadius);
            var overlap = MyAPIGateway.Session.VoxelMaps.GetOverlappingWithSphere(ref sphere);

            if (overlap != null && overlap.EntityId == _planet.EntityId)
            {
                overlap = null;

                foreach (var voxel in _voxelMaps.Values)
                {
                    var radius = voxel.WorldVolume.Radius;

                    if (voxel is MyPlanet)
                    {
                        radius = ((MyPlanet)voxel).AtmosphereRadius;
                    }

                    var distsq = (voxel.PositionComp.GetPosition() - voxelDetails.Position).LengthSquared();

                    if (distsq < Math.Pow(overlapRadius + radius, 2))
                    {
                        overlap = voxel;
                        break;
                    }
                }
            }

            ProceduralVoxelDetails overlapPending = null;

            if (overlap == null)
            {
                if (sectorVoxels.Count != 0)
                {
                    overlapPending = sectorVoxels.Values.FirstOrDefault(e => Vector3D.DistanceSquared(e.Position, voxelDetails.Position) < Math.Pow(e.Size / 2 + overlapRadius, 2));
                }
            }

            if (overlap != null)
            {
                var radius = overlap.WorldVolume.Radius;

                if (overlap is MyPlanet)
                {
                    radius = ((MyPlanet)overlap).AtmosphereRadius;
                }

                LogDebug($"Asteroid {voxelDetails.Name} at {voxelDetails.Position} Overlapped asteroid {overlap.EntityId} [{overlap.GetType().Name}: {overlap.StorageName}] with radius {radius} at {overlap.PositionComp.GetPosition()}");
            }
            else if (overlapPending != null)
            {
                LogDebug($"Asteroid {voxelDetails.Name} at {voxelDetails.Position} Overlapped just added asteroid {overlapPending.Name} with size {overlapPending.Size} at {overlapPending.Position}");
            }
            else
            {
                return true;
            }

            return false;
        }

        private List<Vector2I> GetSectorsToProcess(List<Vector2I> sectorsToProcess)
        {
            var entitySectors = sectorsToProcess.ToList();
            var retSectors = new HashSet<Vector2I>();

            for (int i = 1; i < 8; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    foreach (var sector in entitySectors)
                    {
                        var interleave = sector.X / sector.Y;
                        var deltas = new List<Vector2I>
                        {
                            sector + new Vector2I(-i + j * interleave, j),
                            sector + new Vector2I(-i - j * interleave, -j),
                            sector + new Vector2I(i + j * interleave, j),
                            sector + new Vector2I(i - j * interleave, -j),
                            sector + new Vector2I(-j + i * interleave, i),
                            sector + new Vector2I(-j - i * interleave, -i),
                            sector + new Vector2I(j + i * interleave, i),
                            sector + new Vector2I(j - i * interleave, -i)
                        };

                        for (int k = 0; k < deltas.Count; k++)
                        {
                            var delta = deltas[k];

                            if (delta.Y > 0)
                            {
                                delta.X %= (delta.Y * 6);

                                if (delta.X < 0)
                                {
                                    delta.X += delta.Y * 6;
                                }

                                sectorsToProcess.Add(delta);
                            }
                        }
                    }
                }
            }

            foreach (var sector in _ringSectorVoxelMaps.Keys)
            {
                sectorsToProcess.Add(sector);
            }

            foreach (var sector in sectorsToProcess)
            {
                if (sector.Y >= _minRingSectorY && sector.Y <= _maxRingSectorY)
                {
                    int seed;

                    if (!_ringSectorSeeds.TryGetValue(sector, out seed))
                    {
                        _ringSectorSeeds[sector] = seed = (int)MyHashRandomUtils.JenkinsHash((uint)sector.GetHashCode());
                    }

                    int maxAsteroids;

                    if (!_ringSectorMaxAsteroids.TryGetValue(sector, out maxAsteroids))
                    {
                        var random = new Random(seed);
                        var maxAsteroidsPerSector = _maxAsteroidsPerSector;

                        var zone = _ringZones.FirstOrDefault(e => e.InnerRadius <= sector.Y && e.OuterRadius > sector.Y);

                        if (zone != null)
                        {
                            maxAsteroidsPerSector = zone.MaxAsteroidsPerSector ?? maxAsteroidsPerSector;
                        }

                        _ringSectorMaxAsteroids[sector] = random.Next(maxAsteroidsPerSector * 3 / 4, maxAsteroidsPerSector);
                    }

                    HashSet<long> ids;

                    if (!_ringSectorsCompleted.Contains(sector) && (!retSectors.Contains(sector) || !_ringSectorVoxelMaps.TryGetValue(sector, out ids) || ids.Count < maxAsteroids))
                    {
                        retSectors.Add(sector);
                    }
                }
            }

            return retSectors.ToList();
        }

        private void GetVoxelMaps(List<IMyVoxelBase> voxelmaps)
        {
            foreach (var entity in voxelmaps)
            {
                bool addVoxel = false;
                var storageName = entity.StorageName;
                var pos = entity.PositionComp?.GetPosition() ?? Vector3D.Zero;

                if (!_voxelMaps.ContainsKey(entity.EntityId) && storageName != null && !entity.Closed)
                {
                    addVoxel = true;
                    _voxelMaps[entity.EntityId] = entity;
                    _voxelMapsByName[storageName] = entity;
                }

                if (addVoxel)
                {
                    var sector = GetRingSectorForPosition(pos, $"{entity.EntityId} [{storageName}]");

                    _voxelMapSectors[entity.EntityId] = sector;

                    if (sector != default(Vector2I) && storageName.StartsWith($"RingAsteroid_P({_planet.StorageName}-{_planet.EntityId})_"))
                    {
                        HashSet<long> ids;

                        if (!_ringSectorVoxelMaps.TryGetValue(sector, out ids))
                        {
                            _ringSectorVoxelMaps[sector] = ids = new HashSet<long>();
                        }

                        ids.Add(entity.EntityId);
                    }
                }
            }

            List<IMyVoxelBase> voxels;

            voxels = _voxelMaps.Values.ToList();

            foreach (var voxelmap in voxels)
            {
                if (voxelmap.Closed)
                {
                    Vector2I sector;
                    bool hasSector = false;

                    if (_voxelMapSectors.TryGetValue(voxelmap.EntityId, out sector))
                    {
                        hasSector = true;
                        _voxelMapSectors.Remove(voxelmap.EntityId);
                    }

                    if (hasSector)
                    {
                        HashSet<long> ids;

                        if (_ringSectorVoxelMaps.TryGetValue(sector, out ids))
                        {
                            ids.Remove(voxelmap.EntityId);
                        }

                        _ringSectorsCompleted.Remove(sector);
                    }

                    _voxelMaps.Remove(voxelmap.EntityId);
                }
            }
        }

        private List<Vector2I> GetEntityMovements(IEnumerable<IMyEntity> entities, Dictionary<long, IMyJumpDrive> jumpDrives, Dictionary<long, Vector3D?> jumpTargets, out Dictionary<Vector2I, List<IMyEntity>> entitySectors)
        {
            var sectorsToProcess = new HashSet<Vector2I>();
            entitySectors = new Dictionary<Vector2I, List<IMyEntity>>();

            foreach (var entity in entities)
            {
                if (!(entity is IMyVoxelBase))
                {
                    var entityPos = entity.PositionComp.GetPosition();
                    Vector3D oldEntityPos;
                    Vector2I sector;
                    List<IMyEntity> sectorEntities;

                    if (!_entityPositions.TryGetValue(entity.EntityId, out oldEntityPos) ||
                        !_entitySectors.TryGetValue(entity.EntityId, out sector) ||
                        (entityPos - oldEntityPos).LengthSquared() > _entityMovementThreshold * _entityMovementThreshold)
                    {
                        _entityPositions[entity.EntityId] = entityPos;
                        sector = GetRingSectorForPosition(entityPos, $"{entity.GetType().Name} {entity.EntityId} [{entity.DisplayName}]");
                        _entitySectors[entity.EntityId] = sector;

                        if (sector != default(Vector2I))
                        {
                            sectorsToProcess.Add(sector);
                        }
                    }

                    if (sector != default(Vector2I))
                    {
                        if (!entitySectors.TryGetValue(sector, out sectorEntities))
                        {
                            entitySectors[sector] = sectorEntities = new List<IMyEntity>();
                        }

                        sectorEntities.Add(entity);
                    }
                }


                if (entity is IMyCubeGrid)
                {
                    var grid = (IMyCubeGrid)entity;
                    var jumpsystem = grid.JumpSystem;

                    if (jumpsystem?.IsJumping == true)
                    {
                        var jumpTarget = jumpTargets.GetValueOrDefault(grid.EntityId);
                        var jumpdrive = jumpDrives.GetValueOrDefault(grid.EntityId);

                        if (jumpTarget != null && jumpdrive != null)
                        {
                            Vector3D oldpos;
                            Vector2I sector;

                            if (!_entityPositions.TryGetValue(jumpdrive.EntityId, out oldpos) ||
                                !_entitySectors.TryGetValue(jumpdrive.EntityId, out sector) ||
                                oldpos != jumpTarget)
                            {
                                _entityPositions[jumpdrive.EntityId] = jumpTarget.Value;
                                sector = GetRingSectorForPosition(jumpTarget.Value, $"{entity.GetType().Name} {entity.EntityId} [{entity.DisplayName}] JumpTarget");
                                _entitySectors[jumpdrive.EntityId] = sector;

                                if (sector != default(Vector2I) && sector != _entitySectors[entity.EntityId])
                                {
                                    sectorsToProcess.Add(sector);
                                }
                            }

                            if (sector != default(Vector2I) && sector != _entitySectors[entity.EntityId])
                            {
                                List<IMyEntity> sectorEntities;

                                if (!entitySectors.TryGetValue(sector, out sectorEntities))
                                {
                                    entitySectors[sector] = sectorEntities = new List<IMyEntity>();
                                }

                                sectorEntities.Add(entity);
                            }
                        }
                    }
                }
            }

            return sectorsToProcess.ToList();
        }

        private void OrderPendingAsteroidsByDistance(Dictionary<Vector2I, List<IMyEntity>> entitySectors, Dictionary<long, List<IMySlimBlock>> gridBlocks, List<IMyPlayer> players)
        {
            HashSet<long> playerControlledEntities = GetPlayerControlledEntities(players);

            GetVoxelsByDistance(entitySectors, gridBlocks, playerControlledEntities);

            var addVoxels = new Queue<MyTuple<ProceduralVoxelDetails, double, double>>();
            var delVoxels = new Stack<MyTuple<ProceduralVoxelDetails, double, double>>();

            foreach (var tuple in _voxelsByDistance)
            {
                var voxelDetails = tuple.Item1;
                var distFromEntity = tuple.Item2;
                var distFromPlayer = tuple.Item3;

                if (_limitPhysics && voxelDetails.VoxelMap != null && !voxelDetails.VoxelMap.Closed && !voxelDetails.DeletePending)
                {
                    var physics = voxelDetails.VoxelMap.Physics;

                    if (physics != null)
                    {
                        if (distFromEntity > _physicsDistance * 1.25 && physics.IsActive)
                        {
                            physics.Enabled = false;
                        }
                        else if (distFromEntity < _physicsDistance && !physics.IsActive)
                        {
                            physics.Enabled = true;
                        }
                    }
                }

                if ((voxelDetails.VoxelMap == null || voxelDetails.VoxelMap.Closed) && distFromEntity < _playerSpawnDistance && !voxelDetails.AddPending && !voxelDetails.IsInhibited)
                {
                    if (distFromEntity <= 0)
                    {
                        voxelDetails.IsInhibited = true;
                    }
                    else if (distFromEntity < _gridSpawnDistance || distFromPlayer < _playerSpawnDistance)
                    {
                        voxelDetails.IsModified = false;
                        addVoxels.Enqueue(tuple);
                    }
                }
                else if (!_disableCleanup && voxelDetails.VoxelMap != null && !voxelDetails.VoxelMap.Closed && (!voxelDetails.IsModified || !voxelDetails.VoxelMap.Save) && !voxelDetails.DeletePending)
                {
                    if (voxelDetails.VoxelMap.Save && distFromEntity > _saveDistance * 1.25)
                    {
                        if (IsVoxelMapModified(voxelDetails))
                        {
                            LogDebug($"Setting asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] IsModified=true");
                            voxelDetails.IsModified = true;
                        }
                        else if (!_disableSaveLimit)
                        {
                            LogDebug($"Setting asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] Save=false (Dist={distFromEntity} Sector={voxelDetails.Sector})");
                            voxelDetails.VoxelMap.Save = false;
                        }
                        else if (distFromPlayer > _playerSpawnDistance * 1.25)
                        {
                            delVoxels.Push(tuple);
                        }
                    }
                    else if (distFromEntity > _gridSpawnDistance * 1.25 && distFromPlayer > _playerSpawnDistance * 1.25)
                    {
                        delVoxels.Push(tuple);
                    }
                    else if (voxelDetails.VoxelMap.Save == false && distFromEntity < _saveDistance)
                    {
                        LogDebug($"Setting asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] Save=true (Dist={distFromEntity} Sector={voxelDetails.Sector})");
                        voxelDetails.VoxelMap.Save = true;
                    }
                }
            }

            _addVoxelsByDistance = addVoxels;
            _delVoxelsByDistance = new Queue<MyTuple<ProceduralVoxelDetails, double, double>>(delVoxels);
        }

        private bool IsVoxelMapModified(ProceduralVoxelDetails voxelDetails)
        {
            byte[] data;
            voxelDetails.VoxelMap.Storage.Save(out data);

            // If voxel has been modified, compressed data will be returned
            var modified = data[0] != 0x06;
            var uncompressedLen = BitConverter.ToInt32(data, data.Length - 4);

            if (modified)
            {
                if (data.Length >= 2048) LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (Filesize={data.Length})");
                if (uncompressedLen < data.Length) LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (UncompressedLength={uncompressedLen} Filesize={data.Length})");
                if (uncompressedLen >= 4096) LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (UncompressedLength={uncompressedLen} Filesize={data.Length})");
                if (data[0] != 0x1f) LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (data[0]={data[0]})");
            }


            // Asteroids with even the tiniest dent are almost always >2kB in size, while unmodified asteroids are usually <1kB in size
            // Testing in an empty world, a 64x64x64 asteroid with a single 1x1x1 voxel hand addition was 701 bytes compressed or 1391 bytes uncompressed
            // Meanwhile an unmodified asteroid generated using CreateProceduralVoxelMap in the Perdiso system was 889 bytes compressed or 1813 bytes uncompressed
            if (modified && data[0] == 0x1f && data.Length < 2048 && uncompressedLen > data.Length && uncompressedLen < 4096)
            {
                var buf = new byte[data.Length + 4];

                // GZip files include the uncompressed length at the end of the buffer
                // MyCompression.Decompress expects this at the start of the buffer
                // Copy length from end of buffer to start of buffer
                Array.Copy(data, data.Length - 4, buf, 0, 4);
                Array.Copy(data, 0, buf, 4, data.Length);

                try
                {
                    var cbuf = MyCompression.Decompress(buf);
                    var buffer = new ByteArrayBuffer(cbuf);

                    OctreeStorage.OctreeStorage storage;
                    if (OctreeStorage.OctreeStorage.TryRead(buffer, out storage, s => LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}]: {s}")))
                    {
                        if (storage.MacroContentNodes.Nodes.Length != 0)
                            LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (MacroContentNodes: {storage.MacroContentNodes.Nodes.Length})");
                        if (storage.MacroMaterialNodes.Nodes.Length != 0)
                            LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (MacroMaterialNodes: {storage.MacroMaterialNodes.Nodes.Length})");
                        if (storage.ContentLeaves.Length != 1)
                            LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (ContentLeaves: {storage.ContentLeaves.Length})");
                        if (storage.MaterialLeaves.Length != 1)
                            LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (ContentLeaves: {storage.MaterialLeaves.Length})");
                        if (storage.ContentLeaves.Any(e => e.Type != OctreeStorage.OctreeStorageChunkType.ContentLeafProvider))
                            LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (ContentLeaves: HasNonProvider)");
                        if (storage.MaterialLeaves.Any(e => e.Type != OctreeStorage.OctreeStorageChunkType.MaterialLeafProvider))
                            LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (MaterialLeaves: HasNonProvider)");

                        modified = storage.MacroContentNodes.Nodes.Length != 0
                                || storage.MacroMaterialNodes.Nodes.Length != 0
                                || storage.ContentLeaves.Length != 1
                                || storage.ContentLeaves.Any(e => e.Type != OctreeStorage.OctreeStorageChunkType.ContentLeafProvider)
                                || storage.MaterialLeaves.Length != 1
                                || storage.MaterialLeaves.Any(e => e.Type != OctreeStorage.OctreeStorageChunkType.MaterialLeafProvider);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Asteroid {voxelDetails.VoxelMap.EntityId} [{voxelDetails.VoxelMap.StorageName}] (Exception: {ex})");
                }
            }

            return modified;
        }

        private static HashSet<long> GetPlayerControlledEntities(List<IMyPlayer> players)
        {
            var playerControlledEntities = new HashSet<long>();

            foreach (var player in players)
            {
                if (!player.IsBot)
                {
                    if (player.Character != null)
                    {
                        var entity = (IMyEntity)player.Character;

                        while (entity != null)
                        {
                            playerControlledEntities.Add(entity.EntityId);
                            entity = entity.Parent;
                        }
                    }

                    if (player.Controller?.ControlledEntity?.Entity != null)
                    {
                        var entity = player.Controller.ControlledEntity.Entity;

                        while (entity != null)
                        {
                            playerControlledEntities.Add(entity.EntityId);
                            entity = entity.Parent;
                        }
                    }
                }
            }

            return playerControlledEntities;
        }

        private void GetVoxelsByDistance(Dictionary<Vector2I, List<IMyEntity>> entitySectors, Dictionary<long, List<IMySlimBlock>> gridBlocks, HashSet<long> playerControlledEntities)
        {
            var voxelDistances = new List<MyTuple<ProceduralVoxelDetails, double, double>>();

            foreach (var kvp in _voxelCreationDetails)
            {
                var entities = new Dictionary<long, IMyEntity>();
                var voxelCreates = kvp.Value;
                var sectorsByDistance = new List<MyTuple<Vector2I, double>>();
                var sector = kvp.Key;
                var secrad = (sector.Y + 0.5) * _sectorSize;
                var secphi = sector.X * Math.PI / sector.Y / 3;
                var seccentre = new Vector3D(secrad * Math.Cos(secphi), 0, secrad * Math.Sin(secphi));

                foreach (var entkvp in entitySectors)
                {
                    var entsector = entkvp.Key;
                    var entsecrad = (entsector.Y + 0.5) * _sectorSize;
                    var entsecphi = entsector.X * Math.PI / entsector.Y / 3;
                    var entseccentre = new Vector3D(entsecrad * Math.Cos(entsecphi), 0, entsecrad * Math.Sin(entsecphi));
                    var entsecdist = (entseccentre - seccentre).Length();
                    sectorsByDistance.Add(new MyTuple<Vector2I, double>(entsector, entsecdist));
                }

                seccentre = Vector3D.Transform(seccentre, _ringMatrix);
                var mindist = sectorsByDistance.Count == 0 ? double.MaxValue : sectorsByDistance.Min(e => e.Item2);

                foreach (var tuple in sectorsByDistance)
                {
                    if (tuple.Item2 < mindist + _sectorSize * 3)
                    {
                        var secentities = entitySectors[tuple.Item1];
                        var entityMinDist = secentities.Min(e => (_entityPositions[e.EntityId] - seccentre).Length());

                        foreach (var entity in secentities)
                        {
                            Vector3D pos = _entityPositions[entity.EntityId];
                            var dist = (pos - seccentre).Length();
                            var jumping = entity is IMyCubeGrid && ((IMyCubeGrid)entity).JumpSystem?.IsJumping == true;

                            if (dist < entityMinDist + _sectorSize * 3 || jumping)
                            {
                                entities[entity.EntityId] = entity;
                            }
                        }
                    }
                }

                var sectorVoxelCreates = voxelCreates.Values.ToList();

                GetUnmappedExistingRingAsteroids(kvp.Key, voxelCreates, sectorVoxelCreates);

                foreach (var voxelCreate in sectorVoxelCreates)
                {
                    var voxeldist = double.MaxValue;
                    var voxeldistfromplayer = double.MaxValue;

                    foreach (var entity in entities.Values)
                    {
                        if (entity is IMyCubeGrid && !entity.Closed)
                        {
                            GetGridDistance(playerControlledEntities, gridBlocks, voxelCreate, ref voxeldist, ref voxeldistfromplayer, entity);
                        }
                        else
                        {
                            var dist = (_entityPositions[entity.EntityId] - voxelCreate.Position).Length();

                            if (entity.LocalVolume.Radius > 0)
                            {
                                dist -= entity.LocalVolume.Radius;
                            }

                            if (dist < voxeldist)
                            {
                                voxeldist = dist;
                            }

                            if (playerControlledEntities.Contains(entity.EntityId) && dist < voxeldistfromplayer)
                            {
                                voxeldistfromplayer = dist;
                            }
                        }
                    }

                    if (voxeldistfromplayer < voxeldist)
                    {
                        voxeldist = voxeldistfromplayer;
                    }

                    voxelDistances.Add(new MyTuple<ProceduralVoxelDetails, double, double>(voxelCreate, voxeldist - voxelCreate.Size, voxeldistfromplayer - voxelCreate.Size));
                }

            }

            _voxelsByDistance = voxelDistances.OrderBy(e => e.Item2).ToList();
        }

        private void GetUnmappedExistingRingAsteroids(Vector2I sector, Dictionary<string, ProceduralVoxelDetails> voxelCreates, List<ProceduralVoxelDetails> sectorVoxelCreates)
        {
            HashSet<long> voxelids;
            if (_ringSectorVoxelMaps.TryGetValue(sector, out voxelids))
            {
                foreach (var id in voxelids)
                {
                    IMyVoxelBase voxelmap;

                    if (_voxelMaps.TryGetValue(id, out voxelmap))
                    {
                        var name = voxelmap.StorageName;
                        var position = voxelmap.PositionComp?.GetPosition();
                        var size = voxelmap.Storage?.Size.X;

                        if (voxelmap is IMyVoxelMap &&
                            !voxelmap.Closed &&
                            !voxelCreates.ContainsKey(name) &&
                            position != null &&
                            size != null &&
                            name.StartsWith($"RingAsteroid_P({_planet.StorageName}-{_planet.EntityId})_"))
                        {
                            sectorVoxelCreates.Add(new ProceduralVoxelDetails
                            {
                                Sector = sector,
                                Name = name,
                                VoxelMap = (IMyVoxelMap)voxelmap,
                                Position = position.Value,
                                Size = size.Value,
                                DeleteAction = DeleteAsteroid
                            });
                        }
                    }

                    if (_voxelMaps.TryGetValue(id, out voxelmap) &&
                        voxelmap is IMyVoxelMap &&
                        !voxelmap.Closed &&
                        !voxelCreates.ContainsKey(voxelmap.StorageName) &&
                        voxelmap.StorageName.StartsWith($"RingAsteroid_P({_planet.StorageName}-{_planet.EntityId})_"))
                    {
                    }
                }
            }
        }

        private void GetGridDistance(HashSet<long> playerControlledEntities, Dictionary<long, List<IMySlimBlock>> gridBlocks, ProceduralVoxelDetails voxelCreate, ref double voxeldist, ref double voxeldistfromplayer, IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;
            var gridpos = grid.WorldToGridInteger(voxelCreate.Position);
            var maxpos = grid.Max;
            var minpos = grid.Min;
            var nearestcorner = Vector3I.Clamp(gridpos, grid.Min, grid.Max);
            var distsq = (grid.GridIntegerToWorld(nearestcorner) - voxelCreate.Position).LengthSquared();

            if (distsq < voxeldist * voxeldist || (distsq < voxeldistfromplayer * voxeldistfromplayer && playerControlledEntities.Contains(entity.EntityId)))
            {
                List<IMySlimBlock> blocks;

                if (!gridBlocks.TryGetValue(grid.EntityId, out blocks))
                {
                    blocks = new List<IMySlimBlock>();

                    for (int retries = 3; ; retries--)
                    {
                        try
                        {
                            grid.GetBlocks(blocks);
                            break;
                        }
                        catch (Exception ex) when (retries > 0)
                        {
                            Log($"Error enumerating grid blocks: {ex.Message} - retrying");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error enumerating grid blocks: {ex.Message} - aborting grid block enumeration");
                            return;
                        }
                    }

                    gridBlocks[grid.EntityId] = blocks;
                }

                var mingriddistsq = long.MaxValue;
                var mingriddistfromplayersq = long.MaxValue;

                foreach (var block in blocks)
                {
                    var vec = (block.Position - gridpos);
                    var vecdistsq = (long)vec.X * vec.X + (long)vec.Y * vec.Y + (long)vec.Z * vec.Z;

                    if (vecdistsq < mingriddistsq)
                    {
                        mingriddistsq = vecdistsq;
                    }

                    var fatblock = block.FatBlock;

                    if (fatblock != null && playerControlledEntities.Contains(fatblock.EntityId) && vecdistsq < mingriddistfromplayersq)
                    {
                        mingriddistfromplayersq = vecdistsq;
                    }
                }

                var mingriddist = grid.GridSize * Math.Sqrt(mingriddistsq);
                var mingriddistfromplayer = grid.GridSize * Math.Sqrt(mingriddistfromplayersq);

                if (mingriddist < voxeldist)
                {
                    voxeldist = mingriddist;
                }

                if (mingriddistfromplayer < voxeldistfromplayer)
                {
                    voxeldistfromplayer = mingriddistfromplayer;
                }
            }

            if (grid.JumpSystem?.IsJumping == true)
            {
                var jumpTarget = grid.JumpSystem.GetJumpDriveTarget();

                if (jumpTarget != null)
                {
                    var dist = (jumpTarget.Value - voxelCreate.Position).Length();

                    // Don't inhibit spawn at jump target
                    if (dist < voxelCreate.Size * 1.2)
                    {
                        dist = voxelCreate.Size * 1.2;
                    }

                    if (dist < voxeldist)
                    {
                        voxeldist = dist;
                    }

                    if (dist < voxeldistfromplayer && playerControlledEntities.Contains(entity.EntityId))
                    {
                        voxeldistfromplayer = dist;
                    }
                }
            }
        }
    }
}
