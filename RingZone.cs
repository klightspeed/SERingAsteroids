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
    }
}
