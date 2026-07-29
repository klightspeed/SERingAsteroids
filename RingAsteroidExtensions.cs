using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace SERingAsteroids
{
    public static class RingAsteroidExtensions
    {
        public static IMyVoxelMap CreateProceduralAsteroid(this ProceduralVoxelDetails details)
        {
            IMyVoxelMap voxelmap;

            if (details.UseCreateProceduralVoxelMap)
            {
                // CreateProceduralVoxelMap added an optional depositSeed parameter in 1.209 (Economy 2)
                // We can't control the name using this function.
                // It'll always be ProcAsteroid-{seed}r{size}[-{num}]
                voxelmap = MyAPIGateway.Session.VoxelMaps.CreateProceduralVoxelMap(
                    details.Seed,
                    details.Size,
                    MatrixD.CreateTranslation(details.Position)
#if !(VERSION_208 || VERSION_207 || VERSION_206 || VERSION_205 || VERSION_204 || VERSION_203 || VERSION_202 || VERSION_201 || VERSION_200 || VERSION_199 || VERSION_198 || VERSION_197 || VERSION_196 || VERSION_195 || VERSION_194)
                    , details.GeneratorSeed
#endif
                );
            }
            else
            {
                var voxelMaterialDefinitions = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();
                var defaultMaterials =
                    voxelMaterialDefinitions
                        .Where(e => e.SpawnsInAsteroids && e.MinVersion <= details.VoxelGeneratorVersion && e.MaxVersion >= details.VoxelGeneratorVersion)
                        .Select(e => new OctreeStorage.Chunks.MaterialIndexEntry { Index = e.Index, Name = e.Id.SubtypeName }).ToArray();

                var asteroid = OctreeStorage.OctreeStorage.CreateAsteroid(details.Seed, details.Size, details.GeneratorSeed, materials: defaultMaterials);
                var bytes = asteroid.GetBytes();
                var pos = details.Position;

                IMyStorage storage;

                try
                {
                    storage = MyAPIGateway.Session.VoxelMaps.CreateStorage(bytes);
                }
                catch (Exception ex)
                {
                    details.Log($"Error creating asteroid: {ex}");
                    details.Log($"Writing bad asteroid data to {details.Name}");

                    using (var writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(details.Name, typeof(RingAsteroidsComponent)))
                    {
                        writer.Write(bytes);
                    }

                    throw new AsteroidCreationException("Error creating asteroid", ex);
                }

                pos -= new Vector3D(storage.Size.X + 1, storage.Size.Y + 1, storage.Size.Z + 1) / 2;

                voxelmap = MyAPIGateway.Session.VoxelMaps.CreateVoxelMap(details.Name, storage, pos, details.EntityId);
                MyEntities.RaiseEntityCreated(voxelmap as MyEntity);
            }

            if (!details.NoDisableSave)
                voxelmap.Save = false;

            details.LogDebug($"Spawned asteroid {voxelmap.EntityId} [{voxelmap.StorageName}]");

            return voxelmap;
        }

        public static void DeleteAsteroid(ProceduralVoxelDetails details, IMyVoxelMap voxelmap)
        {
            details.LogDebug($"Deleting asteroid {voxelmap.EntityId} [{voxelmap.StorageName}]");
            voxelmap.Close();
        }

    }
}
