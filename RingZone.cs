using ProtoBuf;

namespace SERingAsteroids
{
    [ProtoContract]
    public class RingZone
    {
        /// <summary>
        /// Inner radius of zone
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Rounded to nearest multiple of <see cref="RingConfig.SectorSize"/>
        /// </remarks>
        [ProtoMember(1)]
        public double InnerRadius { get; set; }

        /// <summary>
        /// Outer radius of zone
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Rounded to nearest multiple of <see cref="RingConfig.SectorSize"/>
        /// </remarks>
        [ProtoMember(2)]
        public double OuterRadius { get; set; }

        /// <summary>
        /// Override ring height for this zone
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Default: <see cref="RingConfig.RingHeight"/><br/>
        /// Set to 0 for a ring gap
        /// </remarks>
        [ProtoMember(3)]
        public double? RingHeight { get; set; }

        /// <summary>
        /// Override height of inner edge of zone
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Height is linearly interpolated between this and <see cref="OuterRingHeight"/>
        /// </remarks>
        [ProtoMember(4)]
        public double? InnerRingHeight { get; set; }

        /// <summary>
        /// Override height of outer edge of zone
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Height is linearly interpolated between this and <see cref="InnerRingHeight"/>
        /// </remarks>
        [ProtoMember(5)]
        public double? OuterRingHeight { get; set; }

        /// <summary>
        /// Override max asteroids per sector
        /// </summary>
        /// <remarks>
        /// Default: <see cref="RingConfig.MaxAsteroidsPerSector"/>
        /// </remarks>
        [ProtoMember(6)]
        public int? MaxAsteroidsPerSector { get; set; }

        /// <summary>
        /// Override minimum asteroid size
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Default: <see cref="RingConfig.MinAsteroidSize"/>
        /// </remarks>
        [ProtoMember(7)]
        public double? MinAsteroidSize { get; set; }

        /// <summary>
        /// Override maximum asteroid size
        /// </summary>
        /// <remarks>
        /// Unit: metres<br/>
        /// Default: <see cref="RingConfig.MaxAsteroidSize"/>
        /// </remarks>
        [ProtoMember(8)]
        public double? MaxAsteroidSize { get; set; }

        /// <summary>
        /// True to taper inner and outer edges toward the normal ring height
        /// </summary>
        [ProtoMember(9)]
        public bool? TaperEdges { get; set; }

        public override bool Equals(object obj)
        {
            RingZone zone = obj as RingZone;
            return !ReferenceEquals(zone, null) &&
                   InnerRadius == zone.InnerRadius &&
                   OuterRadius == zone.OuterRadius &&
                   RingHeight == zone.RingHeight &&
                   InnerRingHeight == zone.InnerRingHeight &&
                   OuterRingHeight == zone.OuterRingHeight &&
                   MaxAsteroidsPerSector == zone.MaxAsteroidsPerSector &&
                   MinAsteroidSize == zone.MinAsteroidSize &&
                   MaxAsteroidSize == zone.MaxAsteroidSize &&
                   TaperEdges == zone.TaperEdges;
        }

        public override int GetHashCode()
        {
            int hashCode = -2018137331;
            hashCode = hashCode * -1521134295 + InnerRadius.GetHashCode();
            hashCode = hashCode * -1521134295 + OuterRadius.GetHashCode();
            hashCode = hashCode * -1521134295 + RingHeight.GetHashCode();
            hashCode = hashCode * -1521134295 + InnerRingHeight.GetHashCode();
            hashCode = hashCode * -1521134295 + OuterRingHeight.GetHashCode();
            hashCode = hashCode * -1521134295 + MaxAsteroidsPerSector.GetHashCode();
            hashCode = hashCode * -1521134295 + MinAsteroidSize.GetHashCode();
            hashCode = hashCode * -1521134295 + MaxAsteroidSize.GetHashCode();
            hashCode = hashCode * -1521134295 + TaperEdges.GetHashCode();
            return hashCode;
        }
    }
}
