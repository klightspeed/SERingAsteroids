using System;
using VRage.Game.ModAPI;
using VRageMath;

namespace SERingAsteroids
{
    public class AddVoxelDetails
    {
        public bool IsCompleted;
        public Vector3D Position;
        public float Size;
        public int Seed;
        public int GeneratorSeed;
        public string Name;
        public Exception Exception;
        public IMyVoxelMap VoxelMap;
        public Func<int, float, int, Vector3D, string, IMyVoxelMap> AddAction;
    }
}
