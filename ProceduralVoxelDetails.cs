using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SERingAsteroids
{
    [ProtoContract]
    public class ProceduralVoxelDetails
    {
        [ProtoMember(1)]
        public Vector2I Sector { get; set; }

        [ProtoIgnore]
        public bool IsCompleted { get; set; }

        [ProtoIgnore]
        public bool AddPending { get; set; }

        [ProtoIgnore]
        public bool DeletePending { get; set; }

        [ProtoIgnore]
        public bool IsModified { get; set; }

        [ProtoIgnore]
        public bool IsInhibited { get; set; }

        [ProtoMember(2)]
        public Vector3D Position { get; set; }

        [ProtoMember(3)]
        public float Size { get; set; }

        [ProtoMember(4)]
        public int Seed { get; set; }

        [ProtoMember(5)]
        public int GeneratorSeed { get; set; }

        [ProtoMember(6)]
        public int VoxelGeneratorVersion { get; set; }

        [ProtoMember(7)]
        public long EntityId { get; set; }

        [ProtoMember(8)]
        public string Name { get; set; }

        [ProtoIgnore]
        public Exception Exception { get; set; }

        [ProtoIgnore]
        public IMyVoxelMap VoxelMap { get; set; }

        [ProtoIgnore]
        public Action<string> LogAction { get; set; }

        [ProtoIgnore]
        public Action<string> LogDebugAction { get; set; }

        [ProtoIgnore]
        public bool NoDisableSave { get; set; }

        public void ExecuteAdd()
        {
            try
            {
                VoxelMap = CreateProceduralAsteroid();
            }
            catch (Exception ex)
            {
                Exception = ex;
            }

            IsCompleted = true;
        }

        public void ExecuteDelete()
        {
            DeleteAsteroid();
        }

        private void Log(string message) => LogAction?.Invoke(message);

        private void LogDebug(string message) => LogDebugAction?.Invoke(message);

        private IMyVoxelMap CreateProceduralAsteroid()
        {
            IMyVoxelMap voxelmap;

            if (EntityId != 0L)
            {
                var entity = MyAPIGateway.Entities.GetEntityById(EntityId);
                if (entity is IMyVoxelMap)
                {
                    return (IMyVoxelMap)entity;
                }
            }

#if false
            voxelmap = MyAPIGateway.Session.VoxelMaps.CreateProceduralVoxelMap(seed, size, MatrixD.CreateTranslation(pos));
#else
            var voxelMaterialDefinitions = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();
            var defaultMaterials =
                voxelMaterialDefinitions
                    .Where(e => e.SpawnsInAsteroids && e.MinVersion <= VoxelGeneratorVersion && e.MaxVersion >= VoxelGeneratorVersion)
                    .Select(e => new OctreeStorage.Chunks.MaterialIndexEntry { Index = e.Index, Name = e.Id.SubtypeName }).ToArray();
            var asteroid = OctreeStorage.OctreeStorage.CreateAsteroid(Seed, Size, GeneratorSeed, materials: defaultMaterials);
            var bytes = asteroid.GetBytes();

            IMyStorage storage;

            try
            {
                storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(bytes);
            }
            catch (Exception ex)
            {
                Log($"Error creating asteroid: {ex}");
                Log($"Writing bad asteroid data to {Name}");

                using (var writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(Name, typeof(RingAsteroidsComponent)))
                {
                    writer.Write(bytes);
                }

                throw new AsteroidCreationException("Error creating asteroid", ex);
            }

            var pos = Position - new Vector3D(storage.Size.X + 1, storage.Size.Y + 1, storage.Size.Z + 1) / 2;

            voxelmap = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(Name, storage, Position, EntityId);
            MyEntities.RaiseEntityCreated(voxelmap as MyEntity);

            if (!NoDisableSave)
                voxelmap.Save = false;
#endif
            LogDebug($"Spawned asteroid {voxelmap.EntityId} [{voxelmap.StorageName}]");

            return voxelmap;
        }

        private void DeleteAsteroid()
        {
            LogDebug($"Deleting asteroid {VoxelMap.EntityId} [{VoxelMap.StorageName}]");
            VoxelMap.Close();
        }
    }
}
