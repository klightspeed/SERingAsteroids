using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BylenRingAsteroids
{
    public class RingZone
    {
        public double InnerRadius { get; set; }
        public double OuterRadius { get; set; }
        public int? MaxAsteroidsPerSector { get; set; }
        public double? MinAsteroidSize { get; set; }
        public double? MaxAsteroidSize { get; set; }
    }
}
