using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
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
        private bool _taperRingEdge;
        private bool _logDebug;
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

        private readonly Queue<string> loglines = new Queue<string>();
        private readonly Dictionary<long, Vector3D> _entityPositions = new Dictionary<long, Vector3D>();
        private readonly Dictionary<long, IMyVoxelBase> _voxelMaps = new Dictionary<long, IMyVoxelBase>();
        private readonly Dictionary<long, Vector2I> _voxelMapSectors = new Dictionary<long, Vector2I>();
        private readonly Dictionary<Vector2I, HashSet<long>> _ringSectorVoxelMaps = new Dictionary<Vector2I, HashSet<long>>();
        private readonly Dictionary<Vector2I, int> _ringSectorSeeds = new Dictionary<Vector2I, int>();
        private readonly Dictionary<Vector2I, int> _ringSectorMaxAsteroids = new Dictionary<Vector2I, int>();
        private readonly HashSet<Vector2I> _ringSectorsToProcess = new HashSet<Vector2I>();

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
            _logDebug = config.LogDebug ?? false;

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

            var rotx = MatrixD.CreateRotationX(-_ringInclination * Math.PI / 180);
            var roty = MatrixD.CreateRotationY(_ringLongitudeAscendingNode * Math.PI / 180);
            var planetPos = _planet.PositionComp.GetPosition();
            var ringbbmax = new Vector3D(_ringOuterRadius * 2, _ringOuterRadius * 2, _ringOuterRadius * 2);
            var trans = MatrixD.CreateTranslation(_planet.PositionComp.GetPosition());
            _ringMatrix = rotx * roty * trans;
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
            var innerRad = _ringInnerRadius - _sectorSize * 2;
            var outerRad = _ringOuterRadius + _sectorSize * 2;
            var ringHeight = _ringHeight + _sectorSize * 2;

            if (radius_sq > innerRad * innerRad)
            {
                var planetLon = Math.Atan2(planetLocalPosition.Z, planetLocalPosition.X) * 180 / Math.PI;
                var planetLat = Math.Atan(planetLocalPosition.Y / Math.Sqrt(planetLocalPosition.X * planetLocalPosition.X + planetLocalPosition.Z * planetLocalPosition.Z)) * 180 / Math.PI;
                var ringLon = Math.Atan2(ringLocalPosition.Z, ringLocalPosition.X) * 180 / Math.PI;

                if (radius_sq >= innerRad * innerRad && radius_sq <= outerRad * outerRad && Math.Abs(ringLocalPosition.Y) < ringHeight)
                {
                    var radius = Math.Sqrt(radius_sq);
                    var sectorRadius = (int)Math.Floor(radius / _sectorSize + 0.5);
                    var longitude = Math.Floor(Math.Atan2(ringLocalPosition.Z, ringLocalPosition.X) * sectorRadius * 3 / Math.PI - 0.5);
                    ringSector = new Vector2I((int)longitude, sectorRadius);
                }

                LogDebug(
                    $"{friendlyName} " +
                    $"at X:{position.X:N3} Y:{position.Y:N3} Z:{position.Z:N3} " +
                    $"ring X:{ringLocalPosition.X:N3} Y:{ringLocalPosition.Y:N3} Z:{ringLocalPosition.Z:N3} " +
                    $"lon {ringLon:N3} h {ringLocalPosition.Y:N3} rad {Math.Sqrt(radius_sq):N3} " +
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

            MyAPIGateway.Parallel.Start(() =>
            {
                _processing = true;

                try
                {
                    var entities = MyAPIGateway.Entities.GetTopMostEntitiesInBox(ref _ringBoundingBox);
                    GetVoxelMaps();

                    List<Vector2I> sectorsToProcess = GetEntityMovements(entities);
                    GetSectorsToProcess(sectorsToProcess);

                    if (_ringSectorsToProcess.Count != 0)
                    {
                        var sector = _ringSectorsToProcess.First();
                        _ringSectorsToProcess.Remove(sector);
                        AddAsteroidsToSector(sector);
                    }
                }
                catch (Exception ex)
                {
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

        private IMyVoxelMap CreateProceduralAsteroid(int seed, float size, int generatorSeed, Vector3D pos, string name)
        {
            IMyVoxelMap voxelmap;
#if false
            voxelmap = MyAPIGateway.Session.VoxelMaps.CreateProceduralVoxelMap(seed, size, MatrixD.CreateTranslation(pos));
#else
            var voxelMaterialDefinitions = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();
            var defaultMaterials = voxelMaterialDefinitions.Select(e => new OctreeStorage.Chunks.MaterialIndexEntry { Index = e.Index, Name = e.Id.SubtypeName }).ToArray();
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

            voxelmap = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(name, storage, pos, 0L);
#endif
            LogDebug($"Spawned asteroid {voxelmap.EntityId}");

            return voxelmap;
        }

        private void AddAsteroidsToSector(Vector2I sector)
        {
            int seed;

            if (!_ringSectorSeeds.TryGetValue(sector, out seed))
            {
                _ringSectorSeeds[sector] = seed = (int)MyHashRandomUtils.JenkinsHash((uint)sector.GetHashCode());
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

            var zone = _ringZones.FirstOrDefault(e => sector.Y < e.OuterRadius && sector.Y + 1 > e.InnerRadius && e.OuterRadius > e.InnerRadius);

            if (zone != null)
            {
                maxAsteroidsPerSector = zone.MaxAsteroidsPerSector ?? maxAsteroidsPerSector;
                minAsteroidSize = zone.MinAsteroidSize ?? minAsteroidSize;
                maxAsteroidSize = zone.MaxAsteroidSize ?? maxAsteroidSize;

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

            var maxAsteroids = random.Next(maxAsteroidsPerSector / 2, maxAsteroidsPerSector);
            _ringSectorMaxAsteroids[sector] = maxAsteroids;

            int tries = 0;

            var pendingVoxels = new List<AddVoxelDetails>();

            while (ids.Count < maxAsteroids && tries < maxAsteroidsPerSector * 2 && !SessionComponent.Unloading && !_reloadRequired)
            {
                var relrad = random.NextDouble();

                if (innerRingHeight > outerRingHeight * 1.05 || outerRingHeight > innerRingHeight * 1.05)
                {
                    var a = Math.Min(Math.Max((outerRingHeight - innerRingHeight) / Math.Max(innerRingHeight, outerRingHeight), -1), 1);

                    // inverse of ax^2 + (1 - a)x
                    relrad = (a - 1 + Math.Sqrt(a * a + 4 * a * relrad - 2 * a + 1)) / (2 * a);
                }

                var ringHeight = innerRingHeight * (1 - relrad) + outerRingHeight * relrad;

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

                LogDebug($"Sector {sector}: Attempting to spawn {size}m asteroid {name} with seed {aseed} at rad:{rad:N3} phi:{phi:N3} h:{y:N3} X:{pos.X:N3} Y:{pos.Y:N3} Z:{pos.Z:N3} ({ids.Count} / {tries} / {maxAsteroids})");

                var overlapRadius = size * Math.Max(1.0, _exclusionZoneMult) / 2 + Math.Max(0, _exclusionZone);
                var sphere = new BoundingSphereD(pos, overlapRadius);
                var overlap = MyAPIGateway.Session.VoxelMaps.GetOverlappingWithSphere(ref sphere);

                if (overlap != null && overlap.EntityId == _planet.EntityId)
                {
                    overlap = _voxelMaps.Values.FirstOrDefault(e => (e.PositionComp.GetPosition() - pos).LengthSquared() < Math.Pow(overlapRadius + e.WorldVolume.Radius, 2));
                }

                AddVoxelDetails overlapPending = null;

                if (overlap == null)
                {
                    if (pendingVoxels.Count != 0)
                    {
                        overlapPending = pendingVoxels.FirstOrDefault(e => Vector3D.DistanceSquared(e.Position, pos) < Math.Pow(e.Size / 2 + overlapRadius, 2));
                    }
                }

                if (overlap != null)
                {
                    LogDebug($"Overlapped asteroid {overlap.EntityId} [{overlap.StorageName}]");

                    if (!ids.Contains(overlap.EntityId))
                    {
                        var overlap_sector = GetRingSectorForPosition(overlap.PositionComp.GetPosition(), $"{overlap.EntityId} [{overlap.StorageName}]");

                        if (overlap_sector == sector)
                        {
                            ids.Add(overlap.EntityId);
                        }
                    }
                }
                else if (overlapPending != null)
                {
                    LogDebug($"Overlapped just added asteroid {overlapPending.Name}");
                }
                else
                {
                    var voxelDetails = new AddVoxelDetails
                    {
                        Position = pos,
                        Name = name,
                        Seed = aseed,
                        Size = size,
                        GeneratorSeed = gseed,
                        AddAction = CreateProceduralAsteroid
                    };

                    pendingVoxels.Add(voxelDetails);
                    SessionComponent.EnqueueVoxelAdd(voxelDetails);

                    while (true)
                    {
                        var completed = pendingVoxels.Where(e => e.VoxelMap != null).ToList();

                        var exception = pendingVoxels.FirstOrDefault(e => e.Exception != null)?.Exception;

                        if (exception != null)
                        {
                            Log($"Error creating asteroid: {exception}");
                            Log("Ring asteroids disabled for this planet");
                            NeedsUpdate = MyEntityUpdateEnum.NONE;
                            return;
                        }

                        foreach (var details in completed)
                        {
                            var voxel = details.VoxelMap;

                            _voxelMaps[voxel.EntityId] = voxel;

                            _voxelMapSectors[voxel.EntityId] = sector;

                            ids.Add(voxel.EntityId);

                            pendingVoxels.Remove(details);
                        }

                        if (pendingVoxels.Count < 5)
                        {
                            break;
                        }

                        MyAPIGateway.Parallel.Sleep(1);
                    }
                }

                tries++;
            }

            if (SessionComponent.Unloading)
            {
                return;
            }

            while (true)
            {
                var completed = pendingVoxels.Where(e => e.VoxelMap != null).ToList();

                var exception = pendingVoxels.FirstOrDefault(e => e.Exception != null)?.Exception;

                if (exception != null)
                {
                    Log($"Error creating asteroid: {exception}");
                    Log("Ring asteroids disabled for this planet");
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                    return;
                }

                foreach (var details in completed)
                {
                    var voxel = details.VoxelMap;

                    _voxelMaps[voxel.EntityId] = voxel;

                    _voxelMapSectors[voxel.EntityId] = sector;

                    ids.Add(voxel.EntityId);

                    pendingVoxels.Remove(details);
                }

                if (pendingVoxels.Count == 0)
                {
                    break;
                }

                MyAPIGateway.Parallel.Sleep(1);
            }
        }

        private void GetSectorsToProcess(List<Vector2I> sectorsToProcess)
        {
            var entitySectors = sectorsToProcess.ToList();

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

                            if (delta.X < 0)
                            {
                                delta.X += delta.Y * 3;
                            }
                            else if (delta.X >= delta.Y * 3)
                            {
                                delta.X -= delta.Y * 3;
                            }

                            sectorsToProcess.Add(delta);
                        }
                    }
                }
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

                    if (!_ringSectorsToProcess.Contains(sector) || !_ringSectorVoxelMaps.TryGetValue(sector, out ids) || ids.Count < maxAsteroids)
                    {
                        _ringSectorsToProcess.Add(sector);
                    }
                }
            }
        }

        private void GetVoxelMaps()
        {
            var voxelmaps = new List<IMyVoxelBase>();
            MyAPIGateway.Session.VoxelMaps.GetInstances(voxelmaps);
            foreach (var entity in voxelmaps)
            {
                bool addVoxel = false;

                if (!_voxelMaps.ContainsKey(entity.EntityId))
                {
                    addVoxel = true;
                    _voxelMaps[entity.EntityId] = entity;
                }

                if (addVoxel)
                {
                    var pos = entity.PositionComp.GetPosition();
                    var sector = GetRingSectorForPosition(pos, $"{entity.EntityId} [{entity.StorageName}]");

                    _voxelMapSectors[entity.EntityId] = sector;

                    if (sector != default(Vector2I))
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
                    }

                    _voxelMaps.Remove(voxelmap.EntityId);
                }
            }
        }

        private List<Vector2I> GetEntityMovements(List<IMyEntity> entities)
        {
            var sectorsToProcess = new List<Vector2I>();

            foreach (var entity in entities)
            {
                if (!(entity is IMyVoxelBase))
                {
                    var entityPos = entity.PositionComp.GetPosition();
                    Vector3D oldEntityPos;

                    if (!_entityPositions.TryGetValue(entity.EntityId, out oldEntityPos) || (entityPos - oldEntityPos).LengthSquared() > _entityMovementThreshold * _entityMovementThreshold)
                    {
                        _entityPositions[entity.EntityId] = entityPos;
                        var sector = GetRingSectorForPosition(entityPos, $"{entity.GetType().Name} {entity.EntityId} [{entity.DisplayName}]");

                        if (sector != default(Vector2I))
                        {
                            sectorsToProcess.Add(sector);
                        }
                    }
                }
            }

            return sectorsToProcess;
        }
    }
}
