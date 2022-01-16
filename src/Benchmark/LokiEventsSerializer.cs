using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using NLog.Loki.Model;

namespace Benchmark;

[MemoryDiagnoser]
public class LokiEventsSerializer
{
    private readonly MemoryStream stream = new();
    private readonly JsonSerializerOptions serializerOptionsWithoutOrdering = new();
    private readonly JsonSerializerOptions serializerOptionsWithOrdering = new();

    private readonly LokiEvent @event;
    private readonly IEnumerable<LokiEvent> manyLokiEvents;

    public LokiEventsSerializer()
    {
        @event = new LokiEvent(
            new LokiLabels(new LokiLabel("env", "benchmark"), new LokiLabel("job", "WriteLogEventsAsync")),
            DateTime.Now,
            "Info|Receive message from \"A\" with destination \"B\".");
        var events = new List<LokiEvent>(100);
        for(var i = 0; i < 100; i++)
            events.Add(new LokiEvent(@event.Labels, DateTime.Now, @event.Line));
        manyLokiEvents = events;

        serializerOptionsWithoutOrdering = new JsonSerializerOptions();
        serializerOptionsWithoutOrdering.Converters.Add(new NLog.Loki.LokiEventsSerializer(orderWrites: false));
        serializerOptionsWithoutOrdering.Converters.Add(new NLog.Loki.LokiEventSerializer());

        serializerOptionsWithOrdering = new JsonSerializerOptions();
        serializerOptionsWithOrdering.Converters.Add(new NLog.Loki.LokiEventsSerializer(orderWrites: true));
        serializerOptionsWithOrdering.Converters.Add(new NLog.Loki.LokiEventSerializer());
    }

    [Benchmark]
    public void SerializeManyEventsWithoutOrdering()
    {
        stream.Position = 0;
        JsonSerializer.Serialize(stream, manyLokiEvents, serializerOptionsWithoutOrdering);
    }

    [Benchmark]
    public void SerializeManyEventsWithOrdering()
    {
        stream.Position = 0;
        JsonSerializer.Serialize(stream, manyLokiEvents, serializerOptionsWithOrdering);
    }

    [Benchmark]
    public void SerializeSingleEvent()
    {
        stream.Position = 0;
        JsonSerializer.Serialize(stream, @event, serializerOptionsWithoutOrdering);
    }
}
