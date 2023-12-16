using System.Collections;
using System;
using System.Collections.Generic;
using VRage.Game.Components;

namespace SERingAsteroids
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class SessionComponent : MySessionComponentBase
    {
        public static bool Unloading { get; private set; }
        private static readonly Queue<AddVoxelDetails> VoxelsToAdd = new Queue<AddVoxelDetails>();

        public override void SaveData()
        {
            RingConfig.SaveConfigs();
        }

        protected override void UnloadData()
        {
            Unloading = true;
        }

        public static void EnqueueVoxelAdd(AddVoxelDetails voxelDetails)
        {
            lock (((ICollection)VoxelsToAdd).SyncRoot)
            {
                VoxelsToAdd.Enqueue(voxelDetails);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            AddVoxelDetails addVoxelDetails;
            int voxelCountAdded = 0;

            while (VoxelsToAdd.TryDequeueSync(out addVoxelDetails))
            {
                addVoxelDetails.Execute();

                voxelCountAdded++;

                if (voxelCountAdded > 5)
                {
                    break;
                }
            }
        }
    }
}
