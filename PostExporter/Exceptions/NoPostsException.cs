using System;
using System.Runtime.Serialization;

namespace PostExporter.Exceptions;

public class NoPostsException : Exception
{
    public NoPostsException()
    {
    }

    protected NoPostsException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public NoPostsException(string? message) : base(message)
    {
    }

    public NoPostsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}