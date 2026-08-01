using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
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

        public static string[] GuessPossibleAsteroidOres(int version, int seed, int generatorSeed, out bool isIceAsteroid)
        {
            isIceAsteroid = false;

            var allMaterials = MyDefinitionManager.Static.GetVoxelMaterialDefinitions();

            var depositMaterials = new List<MyVoxelMaterialDefinition>();

            FillMaterials(allMaterials, depositMaterials, version);

            if (depositMaterials.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (version >= 3)
            {
                // MyRandom is just Random with a PushSeed
                var random = new Random(generatorSeed);

                // coreMaterials is always empty for version 3+
                FilterKindDuplicates(random, depositMaterials);

                // surfaceMaterials always has the one material: Stone
                random.Next();

                ProcessSpawnProbabilities(depositMaterials);

                if (random.Next(100) < 1)
                {
                    isIceAsteroid = true;
                    return Array.Empty<string>();
                }
                else if (version >= 4)
                {
                    int maxCount1 = random.NextDouble() > 0.8f ? 4 : 2;
                    int maxCount2 = random.NextDouble() > 0.4f ? 2 : 1;
                    LimitMaterials(random, depositMaterials, maxCount1);

                    random = new Random(seed);

                    LimitMaterials(random, depositMaterials, maxCount2);
                }
            }

            return depositMaterials.Select(e => e.MinedOre).Distinct().ToArray();
        }

        private static void FillMaterials(
                IEnumerable<MyVoxelMaterialDefinition> allMaterials,
                List<MyVoxelMaterialDefinition> depositMaterials,
                int version
            )
        {
            foreach (var material in allMaterials)
            {
                if (material.SpawnsInAsteroids
                    && material.MinVersion <= version
                    && material.MaxVersion >= version
                    && material.MinedOre != "Stone")
                {
                    depositMaterials.Add(material);
                }
            }
        }

        private static void FilterKindDuplicates(Random random, List<MyVoxelMaterialDefinition> materials)
        {
            materials.Sort((x, y) => string.Compare(x.MinedOre, y.MinedOre, StringComparison.OrdinalIgnoreCase));

            int pos = 0;

            for (int i = 1; i <= materials.Count; i++)
            {
                if (i == materials.Count || materials[i].MinedOre != materials[i - 1].MinedOre)
                {
                    materials[pos++] = materials[random.Next(pos, i)];
                }
            }

            materials.RemoveRange(pos, materials.Count - pos);
        }

        private static void ProcessSpawnProbabilities(List<MyVoxelMaterialDefinition> materials)
        {
            int count = materials.Count;

            for (int i = 0; i < count; i++)
            {
                var material = materials[i];
                int addCount = material.AsteroidGeneratorSpawnProbabilityMultiplier - 1;

                for (int j = 0; j < addCount; j++)
                {
                    materials.Add(material);
                }
            }
        }

        private static void LimitMaterials(Random random, List<MyVoxelMaterialDefinition> materials, int maxCount)
        {
            while (materials.Count > maxCount)
            {
                materials.RemoveAt(random.Next(materials.Count));
            }
        }
    }
}
