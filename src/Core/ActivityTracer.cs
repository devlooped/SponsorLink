using System.Diagnostics;

namespace Devlooped.Sponsors;

public static class ActivityTracer
{
    public static ActivitySource Source { get; } = new("Devlooped.Sponsors", ThisAssembly.Info.InformationalVersion);
}
