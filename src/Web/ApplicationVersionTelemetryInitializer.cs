using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Devlooped.Sponsors;

public class ApplicationVersionTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry) =>
        telemetry.Context.Component.Version = ThisAssembly.Info.InformationalVersion;
}
