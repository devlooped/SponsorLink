using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Devlooped.Sponsors;

public interface IPushover
{
    Task PostAsync(PushoverMessage message);
}

public class PushoverOptions
{
    public string? Token { get; set; }
    public string? Key { get; set; }

    public PushoverPriority IssuePriority { get; set; } = PushoverPriority.High;
    public PushoverPriority IssueCommentPriority { get; set; } = PushoverPriority.High;
}


public enum PushoverPriority
{
    Lowest = -2,
    Low = -1,
    Normal = 0,
    High = 1,
    Emergency = 2
}

public class PushoverMessage
{
    /// <summary>
    /// The message's title, otherwise the app's name is used
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The message to send.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///  An image attachment to send with the message.
    /// </summary>
    public string? Attachment { get; set; }

    /// <summary>
    /// A supplementary URL to show with the message
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// A title for the supplementary URL, otherwise just the URL is shown
    /// </summary>
    [JsonPropertyName("url_title")]
    public string? UrlTitle { get; set; }

    /// <summary>
    /// The priority of the message
    /// </summary>
    public PushoverPriority? Priority { get; set; }

    /// <summary>
    /// The name of the sound to use with 
    /// </summary>
    public string Sound { get; set; } = "pushover";

    public PushoverMessage() { }

    public PushoverMessage(string message) => Message = message;
}

public static class PushoverSounds
{
    /// <summary>Pushover (default)</summary>
    public const string Pushover = "pushover";
    /// <summary>Bike</summary>
    public const string Bike = "bike";
    /// <summary>Bugle</summary>
    public const string Bugle = "bugle";
    /// <summary>Cash Register</summary>
    public const string Cashregister = "cashregister";
    /// <summary>Classical</summary>
    public const string Classical = "classical";
    /// <summary>Cosmic</summary>
    public const string Cosmic = "cosmic";
    /// <summary>Falling</summary>
    public const string Falling = "falling";
    /// <summary>Gamelan</summary>
    public const string Gamelan = "gamelan";
    /// <summary>Incoming</summary>
    public const string Incoming = "incoming";
    /// <summary>Intermission</summary>
    public const string Intermission = "intermission";
    /// <summary>Magic</summary>
    public const string Magic = "magic";
    /// <summary>Mechanical</summary>
    public const string Mechanical = "mechanical";
    /// <summary>Piano Bar</summary>
    public const string Pianobar = "pianobar";
    /// <summary>Siren</summary>
    public const string Siren = "siren";
    /// <summary>Space Alarm</summary>
    public const string Spacealarm = "spacealarm";
    /// <summary>Tug Boat</summary>
    public const string Tugboat = "tugboat";
    /// <summary>Alien Alarm (long)</summary>
    public const string Alien = "alien";
    /// <summary>Climb (long)</summary>
    public const string Climb = "climb";
    /// <summary>Persistent (long)</summary>
    public const string Persistent = "persistent";
    /// <summary>Pushover Echo (long)</summary>
    public const string Echo = "echo";
    /// <summary>Up Down (long)</summary>
    public const string Updown = "updown";
    /// <summary>Vibrate Only</summary>
    public const string Vibrate = "vibrate";
    /// <summary>None (silent)</summary>
    public const string None = "none";
}

public class Pushover(IHttpClientFactory factory, IOptions<PushoverOptions> options) : IPushover
{
    static JsonSerializerOptions json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        //Converters =
        //{
        //    new JsonStringEnumConverter(allowIntegerValues: false),
        //}
    };

    readonly PushoverOptions options = options.Value;

    public async Task PostAsync(PushoverMessage message)
    {
        if (options.Token == null || options.Key == null)
            return;

        using var http = factory.CreateClient();

        var node = JsonNode.Parse(JsonSerializer.Serialize(message, json));
        Debug.Assert(node != null);

        node["token"] = options.Token;
        node["user"] = options.Key;

        var response = await http.PostAsJsonAsync("https://api.pushover.net/1/messages.json", node);

        response.EnsureSuccessStatusCode();
    }
}