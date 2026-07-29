using ProtoBuf;
using System;
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

        [ProtoMember(9)]
        public bool IsInhibited { get; set; }

        [ProtoMember(10)]
        public bool NoDisableSave { get; set; }

        [ProtoMember(11)]
        public bool UseCreateProceduralVoxelMap { get; set; }

        [ProtoIgnore]
        public Exception Exception { get; set; }

        [ProtoIgnore]
        public IMyVoxelMap VoxelMap { get; set; }

        [ProtoIgnore]
        public Func<ProceduralVoxelDetails, IMyVoxelMap> AddAction { get; set; }

        [ProtoIgnore]
        public Action<ProceduralVoxelDetails, IMyVoxelMap> DeleteAction { get; set; }

        [ProtoIgnore]
        public Action<string> LogAction { get; set; }

        [ProtoIgnore]
        public Action<string> LogDebugAction { get; set; }

        public void OnClose(IMyEntity entity)
        {
            if (ReferenceEquals(entity, VoxelMap))
            {
                IsInhibited = true;
            }
        }

        public void ExecuteAdd()
        {
            try
            {
                VoxelMap = AddAction(this);
                VoxelMap.OnMarkForClose += OnClose;

                if (VoxelMap.MarkedForClose)
                {
                    IsInhibited = true;
                }
            }
            catch (Exception ex)
            {
                Exception = ex;
            }

            IsCompleted = true;
        }

        public void ExecuteDelete()
        {
            VoxelMap.OnMarkForClose -= OnClose;
            DeleteAction(this, VoxelMap);
        }

        public void Log(string line)
        {
            LogAction?.Invoke(line);
        }

        public void LogDebug(string line)
        {
            LogDebugAction?.Invoke(line);
        }
    }
}
