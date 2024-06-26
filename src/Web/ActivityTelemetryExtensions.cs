using System.Diagnostics;
using System.Security.Claims;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Devlooped.Sponsors;

/// <summary>
/// Sets the <see cref="ClaimsPrincipal.ClaimsPrincipalSelector"/> to a function 
/// that accesses the current <see cref="FunctionContext"/> by leveraging the 
/// <see cref="IFunctionContextAccessor"/> to retrieve the principal from 
/// the <see cref="FunctionContext.Features"/>, if present.
/// </summary>
public static partial class ActivityTelemetryExtensions
{
    static readonly HashSet<string> skipProps = ["faas.execution", "az.schema_url", "session_Id"];

    /// <summary>
    /// Ensures that <see cref="ClaimsPrincipal.Current"/> accesses the principal 
    /// authenticated by the app service authentication middleware.
    /// </summary>
    public static IFunctionsWorkerApplicationBuilder UseActivityTelemetry(this IFunctionsWorkerApplicationBuilder builder)
    {
        builder.UseMiddleware<Middleware>();
        return builder;
    }

    public static IServiceCollection AddActivityTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryInitializer, Initializer>();
        services.AddSingleton<ITelemetryModule, Module>();
        return services;
    }

    class Module(Lazy<TelemetryClient> telemetry) : ITelemetryModule, IDisposable
    {
        ActivityListener? listener;

        public void Initialize(TelemetryConfiguration configuration)
        {
            listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    // For activity > operation, start only those from our sources, for now?
                    if (activity.Source.Name == ActivityTracer.Source.Name)
                        activity.SetCustomProperty(ActivityTracer.Source.Name, telemetry.Value.StartOperation<RequestTelemetry>(activity));
                },
                ActivityStopped = activity =>
                {
                    if (activity.GetCustomProperty(ActivityTracer.Source.Name) is IOperationHolder<RequestTelemetry> holder)
                    {
                        var operation = holder.Telemetry;
                        if (activity.GetBaggageItem("session_Id") is { } baggageId)
                            operation.Context.Session.Id = baggageId;
                        
                        // Populate operation details from activity.
                        foreach (var item in activity.Baggage.Where(x => !skipProps.Contains(x.Key)))
                            operation.Properties[item.Key] = item.Value ?? "";

                        foreach (var item in activity.Tags.Where(x => !skipProps.Contains(x.Key)))
                            operation.Properties[item.Key] = item.Value ?? "";

                        telemetry.Value.StopOperation(holder);
                    }

                    foreach (var ev in activity.Events)
                    {
                        var et = new EventTelemetry(ev.Name)
                        {
                            Timestamp = ev.Timestamp
                        };

                        foreach (var item in activity.Baggage.Where(x => !skipProps.Contains(x.Key)))
                            et.Properties[item.Key] = item.Value;

                        foreach (var item in activity.Tags.Where(x => !skipProps.Contains(x.Key) && x.Value != null))
                            et.Properties[item.Key] = item.Value ?? "";

                        foreach (var item in ev.Tags.Where(x => !skipProps.Contains(x.Key)))
                            et.Properties[item.Key] = item.Value?.ToString() ?? "";

                        telemetry.Value.TrackEvent(et);
                    }
#if DEBUG
                    // Makes it easier to inspect telemetry in debug mode.
                    telemetry.Value.Flush();
#endif
                },
            };

            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose()
        {
            listener?.Dispose();
            if (telemetry.IsValueCreated)
            telemetry.Value.Flush();
        }
    }

    class Middleware(Lazy<TelemetryClient> telemetry) : IFunctionsWorkerMiddleware
    {
        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            var request = await context.GetHttpRequestDataAsync();
            if (request?.Headers.TryGetValues("x-telemetry-id", out var ids) == true &&
                ids.FirstOrDefault() is { Length: > 0 } id)
            {
                // Associate with opaque installation id
                Activity.Current?.AddBaggage("session_Id", id);
                telemetry.Value.Context.Session.Id = id;
            }

            if (request?.Headers.TryGetValues("x-telemetry-operation", out var ops) == true &&
                ops.FirstOrDefault() is { Length: > 0 } op)
            {
                // Associate with opaque installation id
                Activity.Current?.AddBaggage("operation_Name", op);
                telemetry.Value.Context.Operation.Name = op;
            }

            await next(context);
        }
    }

    class Initializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            // We don't set the user id for metrics, as they are not tied to a user.
            if (telemetry is MetricTelemetry)
                return;

            if (telemetry is ISupportProperties sprops &&
                sprops.Properties.TryGetValue("session_Id", out var propId))
            {
                telemetry.Context.Session.Id = propId;
                sprops.Properties.Remove("session_Id");
            }
            else if (Activity.Current?.GetBaggageItem("session_Id") is { } baggageId)
            {
                telemetry.Context.Session.Id = baggageId;
            }

            if (telemetry is ISupportProperties opprops &&
                opprops.Properties.TryGetValue("operation_Name", out var propName))
            {
                telemetry.Context.Operation.Name = propName;
                opprops.Properties.Remove("operation_Name");
            }
            else if (Activity.Current?.GetBaggageItem("operation_Name") is { } baggageName)
            {
                telemetry.Context.Operation.Name = baggageName;
            }
        }
    }
}
