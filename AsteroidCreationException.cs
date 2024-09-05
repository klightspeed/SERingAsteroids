using System;

namespace SERingAsteroids
{
    public class AsteroidCreationException : Exception
    {
        public AsteroidCreationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
