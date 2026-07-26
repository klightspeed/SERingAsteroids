using ProtoBuf;

namespace SERingAsteroids
{
    [ProtoContract]
    public class RingRequest
    {
        [ProtoMember(100)]
        public string PlanetName { get; set; }
    }
}
