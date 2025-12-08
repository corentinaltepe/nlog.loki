using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NLog;
using NLog.Loki;
using NLog.Loki.Model;

namespace Benchmark;

[MemoryDiagnoser]
public class LokiTargetBenchmark
{
    private readonly IList<LokiTargetLabel> _labels = new List<LokiTargetLabel> {
        new() { Name = "Label1", Layout = "MyLabel1Value" },
        new() { Name = "Label2", Layout = "MyLabel2Value-${level}" },
        new() { Name = "Label3", Layout = "MyLabel3Value" },
    };
    private List<LogEventInfo> _logs;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Generate up to N messages for logevent infos
        _logs = new(N);
        for(var i = 0; i < N; i++)
        {
            var level = (i % 4) switch
            {
                0 => LogLevel.Debug,
                1 => LogLevel.Info,
                2 => LogLevel.Warn,
                _ => LogLevel.Error
            };
            _logs.Add(new LogEventInfo(level, "MyLogger", RandomString(55)));
        }
    }

    private static readonly Random Random = new();
    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    [Params(/*1, 10,*/ 100, 1000)]
    public int N { get; set; }

    [Benchmark]
    public async Task WriteAsyncTaskList_Optimized()
    {
        var lokiEvents = GetLokiEventsWithoutLinq(_logs);
        using var jsonStreamContent = JsonContent.Create(lokiEvents);
        _ = await jsonStreamContent.ReadAsByteArrayAsync();
    }

    private IEnumerable<LokiEvent> GetLokiEventsWithoutLinq(IEnumerable<LogEventInfo> logEvents)
    {
        foreach(var e in logEvents)
            yield return GetLokiEventWithoutLinq(e);
    }

    private LokiEvent GetLokiEventWithoutLinq(LogEventInfo logEvent)
    {
        var labels = new LokiLabels(RenderAndMapLokiLabels(_labels, logEvent));
        return new LokiEvent(labels, logEvent.TimeStamp, logEvent.ToString());
    }

    private static HashSet<LokiLabel> RenderAndMapLokiLabels(
        IList<LokiTargetLabel> lokiTargetLabels,
        LogEventInfo logEvent)
    {
        var set = new HashSet<LokiLabel>();
        for(var i = 0; i < lokiTargetLabels.Count; i++)
            _ = set.Add(new LokiLabel(lokiTargetLabels[i].Name, lokiTargetLabels[i].Layout.Render(logEvent)));
        return set;
    }
}

