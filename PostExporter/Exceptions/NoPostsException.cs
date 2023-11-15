using System;

namespace PostExporter.Exceptions;

public class NoPostsException : Exception
{
    public NoPostsException()
    {
    }

    public NoPostsException(string? message) : base(message)
    {
    }

    public NoPostsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}