namespace TransportTests;

using System;

class EnvironmentHelper
{
    public static string GetEnvironmentVariable(string variable)
    {
        string candidate = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return Environment.GetEnvironmentVariable(variable);
        }

        return candidate;
    }
}