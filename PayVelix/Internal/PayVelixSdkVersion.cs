using System.Reflection;

namespace PayVelix.Internal;

internal static class PayVelixSdkVersion
{
    public static readonly string Current = GetCurrentVersion();

    private static string GetCurrentVersion()
    {
        var assembly = typeof(PayVelixClient).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
