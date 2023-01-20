using System.Globalization;
using Azure.Messaging.EventGrid;
using Azure;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Devlooped.SponsorLink;

public interface IEventStream
{
    Task PushAsync<T>(T item, CancellationToken cancellationToken = default) where T : class;
}

[Service]
public class EventStream : IEventStream
{
    static readonly string dataVersion = new Version(ThisAssembly.Info.Version).ToString(2);
    static readonly JsonSerializerSettings settings = new JsonSerializerSettings
    {
        Converters =
        {
            new IsoDateTimeConverter
            {
                DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK",
                DateTimeStyles = DateTimeStyles.AdjustToUniversal
            },
            new StringEnumConverter()
        },
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateFormatString = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK",
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
    };

    readonly IConfiguration configuration;
    readonly TelemetryClient telemetry;

    public EventStream(IConfiguration configuration, TelemetryConfiguration telemetry)
    {
        this.configuration = configuration;
        this.telemetry = new TelemetryClient(telemetry);
    }

    public async Task PushAsync<T>(T item, CancellationToken cancellationToken = default)
        where T : class
    {
        var name = typeof(T).Name ?? throw new ArgumentException("Type must have a name.");
        
        // Given AppInstalled: Subject=App, EventType=Installed
        var subject = name[0] + new string(name.Skip(1).TakeWhile(char.IsLower).ToArray());
        var type = name[subject.Length..];
        var topic = "Devlooped.SponsorLink";
        var id = Guid.NewGuid().ToString();
        var now = DateTimeOffset.Now;

        var te = new EventTelemetry($"{subject}.{type}")
        {
            Properties =
            {
                { "Topic", topic },
                { "Subject", subject },
                { "Event", type },
                { "Id", id },
            },
            Timestamp = now,
        };
        // Lifts this property in AppInsights/Log Workspace for easier querying/filtering
        te.Context.Operation.SyntheticSource = topic;

        var serializer = new JsonSerializer();

        foreach (var prop in typeof(T).GetProperties().Where(t => t.CanRead))
        {
            te.Properties[prop.Name] = JsonConvert.SerializeObject(prop.GetValue(item), settings).Trim('"');
        }

        telemetry.TrackEvent(te);

        var domain = configuration["EventGrid:Domain"];
        var key = configuration["EventGrid:AccessKey"];
        if (domain == null || key == null)
            return;

        var client = new EventGridPublisherClient(
            new Uri($"https://{domain}/api/events"),
            new AzureKeyCredential(key));

        var ge = new EventGridEvent(
            subject,
            type,
            dataVersion,
            BinaryData.FromString(JsonConvert.SerializeObject(item, settings)))
        {
            Topic = topic
        };

        await client.SendEventAsync(ge, cancellationToken);
    }
}
