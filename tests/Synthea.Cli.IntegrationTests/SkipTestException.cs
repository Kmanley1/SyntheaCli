using System;
using Xunit;
using Xunit.Sdk;

namespace Synthea.Cli.IntegrationTests;

// Custom exception to signal a skipped test in xUnit
public class SkipTestException : XunitException
{
    public SkipTestException(string message) : base(message) { }
}
