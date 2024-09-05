using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERingAsteroids
{
    [ProtoContract]
    public class ProceduralVoxelList
    {
        [ProtoMember(1)]
        public List<ProceduralVoxelDetails> Voxels { get; set; }
    }
}
