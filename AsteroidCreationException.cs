using System;

public class AsteroidCreationException : Exception
{
    public AsteroidCreationException(string message, Exception innerException) : base(message, innerException) { }
}

