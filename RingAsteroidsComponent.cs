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
        private double _sizeExponent = 1.0;
        private bool _logDebug;
        private List<RingZone> _ringZones = new List<RingZone>();

        private MatrixD _ringMatrix;
        private MatrixD _ringInvMatrix;
        private BoundingBoxD _ringBoundingBox;
        private int _minRingSectorY;
        private int _maxRingSectorY;
        private string logfilename;
        private TextWriter logfile;
        private MyPlanet _planet;
        private bool _processing;

        private readonly Queue<string> loglines = new Queue<string>();
        private readonly Dictionary<long, Vector3D> _entityPositions = new Dictionary<long, Vector3D>();
        private readonly Dictionary<long, IMyVoxelBase> _voxelMaps = new Dictionary<long, IMyVoxelBase>();
        private readonly Dictionary<long, Vector2I> _voxelMapSectors = new Dictionary<long, Vector2I>();
        private readonly Dictionary<Vector2I, HashSet<long>> _ringSectorVoxelMaps = new Dictionary<Vector2I, HashSet<long>>();
        private readonly Dictionary<Vector2I, int> _ringSectorSeeds = new Dictionary<Vector2I, int>();
        private readonly Dictionary<Vector2I, int> _ringSectorMaxAsteroids = new Dictionary<Vector2I, int>();
        private readonly HashSet<Vector2I> _ringSectorsToProcess = new HashSet<Vector2I>();

        private void LogDebug(string str)
        {
            if (_logDebug)
            {
                Log(str);
            }
        }

        private void Log(string str)
        {
            try
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
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
                });
            }
            catch
            { }
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

            var config = RingConfig.GetRingConfig(_planet);

            if (config.EarlyLog == true || config.Enabled == true)
            {
                logfilename = $"{typeof(RingAsteroidsComponent).Name}-{Entity.EntityId}-{DateTime.Now:yyyyMMddHHmmss}.log";
                logfile = MyAPIGateway.Utilities.WriteFileInLocalStorage(logfilename, typeof(RingAsteroidsComponent));
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
            _logDebug = config.LogDebug ?? false;

            if (config.RingZones != null)
            {
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
            _maxRingSectorY = (int)(_ringOuterRadius / _sectorSize);

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
#if false
            return MyAPIGateway.Session.VoxelMaps.CreateProceduralVoxelMap(seed, size, MatrixD.CreateTranslation(pos));
#else
            var asteroid = OctreeStorage.OctreeStorage.CreateAsteroid(seed, size, generatorSeed);
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

            return MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(name, storage, pos, 0L);
#endif
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

            var zone = _ringZones.FirstOrDefault(e => e.InnerRadius <= sector.Y && e.OuterRadius > sector.Y);

            if (zone != null)
            {
                maxAsteroidsPerSector = zone.MaxAsteroidsPerSector ?? maxAsteroidsPerSector;
                minAsteroidSize = zone.MinAsteroidSize ?? minAsteroidSize;
                maxAsteroidSize = zone.MaxAsteroidSize ?? maxAsteroidSize;
            }

            var maxAsteroids = random.Next(maxAsteroidsPerSector / 2, maxAsteroidsPerSector);
            _ringSectorMaxAsteroids[sector] = maxAsteroids;

            int tries = 0;

            var logmin = Math.Log(minAsteroidSize);
            var logmax = Math.Log(maxAsteroidSize);

            while (ids.Count < maxAsteroids && tries < maxAsteroidsPerSector * 2)
            {
                var rad = random.NextDouble() + sector.Y - 0.5;
                var radphi = random.NextDouble() + sector.X - 0.5;
                var phi = radphi / sector.Y * Math.PI / 3;
                var y = (random.NextDouble() - 0.5) * _ringHeight * 2;

                var size = (float)Math.Exp(Math.Pow(random.NextDouble(), Math.Abs(_sizeExponent)) * (logmax - logmin) + logmin);
                var aseed = random.Next();
                var gseed = random.Next();

                var x = rad * _sectorSize * Math.Cos(phi);
                var z = rad * _sectorSize * Math.Sin(phi);

                var pos = Vector3D.Transform(new Vector3D(x, y, z), _ringMatrix);

                var name = $"RingAsteroid_P({_planet.StorageName}-{_planet.EntityId})_{sector.X}_{sector.Y}_{tries}_{aseed}";

                LogDebug($"Sector {sector}: Attempting to spawn {size}m asteroid {name} with seed {aseed} at rad:{rad:N3} phi:{phi:N3} h:{y:N3} X:{pos.X:N3} Y:{pos.Y:N3} Z:{pos.Z:N3} ({ids.Count} / {tries} / {maxAsteroids})");

                var sphere = new BoundingSphereD(pos, size * 2);
                var overlap = MyAPIGateway.Session.VoxelMaps.GetOverlappingWithSphere(ref sphere);

                if (overlap != null && overlap.EntityId == _planet.EntityId)
                {
                    overlap = _voxelMaps.Values.FirstOrDefault(e => (e.PositionComp.GetPosition() - pos).LengthSquared() < size * size * 4);
                }

                if (overlap == null)
                {
                    IMyVoxelMap voxel = null;
                    Exception exception = null;

                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        try
                        {
                            voxel = CreateProceduralAsteroid(aseed, size, gseed, pos, name);
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }
                    });

                    while (voxel == null)
                    {
                        if (exception != null)
                        {
                            Log($"Error creating asteroid: {exception}");
                            Log("Ring asteroids disabled for this planet");
                            NeedsUpdate = MyEntityUpdateEnum.NONE;
                            return;
                        }

                        MyAPIGateway.Parallel.Sleep(1);
                    }

                    _voxelMaps[voxel.EntityId] = voxel;
                    _voxelMapSectors[voxel.EntityId] = sector;
                    ids.Add(voxel.EntityId);
                    LogDebug($"Spawned asteroid {voxel.EntityId}");
                }
                else
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

                tries++;
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
                if (!_voxelMaps.ContainsKey(entity.EntityId))
                {
                    _voxelMaps[entity.EntityId] = entity;
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

            foreach (var voxelmap in _voxelMaps.Values.ToList())
            {
                if (voxelmap.Closed)
                {
                    Vector2I sector;

                    if (_voxelMapSectors.TryGetValue(voxelmap.EntityId, out sector))
                    {
                        _voxelMapSectors.Remove(voxelmap.EntityId);

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
