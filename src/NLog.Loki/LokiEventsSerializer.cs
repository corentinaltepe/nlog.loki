using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog.Loki.Model;

namespace NLog.Loki;

/// <summary>
/// Serializes log events to json for loki before sending them to the push HTTP API.
/// </summary>
/// <remarks>
/// See https://grafana.com/docs/loki/latest/api/#post-lokiapiv1push
/// </remarks>
internal class LokiEventsSerializer : JsonConverter<IEnumerable<LokiEvent>>
{
    private readonly bool orderWrites;

    public LokiEventsSerializer(bool orderWrites)
    {
        this.orderWrites = orderWrites;
    }

    public override IEnumerable<LokiEvent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("This converter only supports serializing to JSON.");
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable<LokiEvent> value, JsonSerializerOptions options)
    {
        var streams = value.GroupBy(le => le.Labels);

        writer.WriteStartObject();
        writer.WriteStartArray("streams");

        foreach(var stream in streams)
        {
            writer.WriteStartObject();

            writer.WriteStartObject("stream");
            foreach(var label in stream.Key.Labels)
            {
                writer.WritePropertyName(label.Label);
                writer.WriteStringValue(label.Value);
            }
            writer.WriteEndObject();

            writer.WriteStartArray("values");

            // Order logs by timestamp only if the option is opted-in, because it costs
            // approximately 20% more allocation when serializing 100 events.
            IEnumerable<LokiEvent> orderedStream = orderWrites ? stream.OrderBy(le => le.Timestamp) : stream;
            foreach(var @event in orderedStream)
            {
                writer.WriteStartArray();
                var timestamp = UnixDateTimeConverter.ToUnixTimeNs(@event.Timestamp).ToString("g", CultureInfo.InvariantCulture);
                writer.WriteStringValue(timestamp);
                writer.WriteStringValue(@event.Line);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
