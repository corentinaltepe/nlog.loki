using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NLog.Loki;
using NLog.Loki.Model;

namespace Benchmark;

[MemoryDiagnoser]
public class Transport
{
    private readonly IList<LokiEvent> lokiEvents = new List<LokiEvent> {
        new(
            new LokiLabels(new LokiLabel("env", "benchmark"), new LokiLabel("job", "WriteLogEventsAsync")),
            DateTime.Now,
            "Info|Receive message from \"A\" with destination \"B\".")};
    private readonly HttpLokiTransport transport = new(new LokiHttpClient(
        new HttpClient { BaseAddress = new Uri("http://localhost:3100") }));

    [Benchmark]
    public async Task WriteLogEventsAsync()
    {
        await transport.WriteLogEventsAsync(lokiEvents).ConfigureAwait(false);
    }
}