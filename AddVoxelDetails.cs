using System;
using VRage.Game.ModAPI;
using VRageMath;

namespace SERingAsteroids
{
    public class AddVoxelDetails
    {
        public bool IsCompleted { get; set; }
        public Vector3D Position { get; set; }
        public float Size { get; set; }
        public int Seed { get; set; }
        public int GeneratorSeed { get; set; }
        public int VoxelGeneratorVersion { get; set; }
        public string Name { get; set; }
        public Exception Exception { get; set; }
        public IMyVoxelMap VoxelMap { get; set; }
        public Func<int, float, int, Vector3D, string, int, IMyVoxelMap> AddAction { get; set; }

        public void Execute()
        {
            try
            {
                VoxelMap = AddAction(Seed, Size, GeneratorSeed, Position, Name, VoxelGeneratorVersion);
            }
            catch (Exception ex)
            {
                Exception = ex;
            }

            IsCompleted = true;
        }
    }
}
