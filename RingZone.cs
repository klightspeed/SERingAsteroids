namespace SERingAsteroids
{
    public class RingZone
    {
        public double InnerRadius { get; set; }
        public double OuterRadius { get; set; }
        public double? RingHeight { get; set; }
        public double? InnerRingHeight { get; set; }
        public double? OuterRingHeight { get; set; }
        public int? MaxAsteroidsPerSector { get; set; }
        public double? MinAsteroidSize { get; set; }
        public double? MaxAsteroidSize { get; set; }
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
