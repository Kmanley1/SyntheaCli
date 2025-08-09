using System;

namespace Synthea.Cli.IntegrationTests;

/// <summary>
/// Exception thrown to indicate that a test should be skipped due to missing prerequisites.
/// </summary>
public class SkipTestException : Exception
{
    public SkipTestException() : base()
    {
    }

    public SkipTestException(string message) : base(message)
    {
    }

    public SkipTestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
