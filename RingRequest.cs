using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERingAsteroids
{
    [ProtoContract]
    public class RingRequest
    {
        [ProtoMember(100)]
        public string PlanetName { get; set; }
    }
}
