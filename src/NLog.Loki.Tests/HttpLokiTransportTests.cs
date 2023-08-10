using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using NLog.Loki.Model;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace NLog.Loki.Tests;

public class HttpLokiTransportTests
{
    private static IEnumerable<LokiEvent> CreateLokiEvents(int numberEvents = 3)
    {
        var date = new DateTime(2021, 12, 27, 9, 48, 26, DateTimeKind.Utc);
        for(var i = 0; i < numberEvents; i++)
        {
            yield return new(new LokiLabels(new HashSet<LokiLabel> { new LokiLabel("env", "unittest"), new LokiLabel("job", "Job1") }), date, "Info|Receive message from A with destination B.");
            i++;
            yield return new(new LokiLabels(new HashSet<LokiLabel> { new LokiLabel("env", "unittest"), new LokiLabel("job", "Job1") }), date + TimeSpan.FromSeconds(2.2), "Info|Another event has occured here.");
            i++;
            yield return new(new LokiLabels(new HashSet<LokiLabel> { new LokiLabel("env", "unittest"), new LokiLabel("job", "Job1") }), date - TimeSpan.FromSeconds(0.9), "Info|Event from another stream.");
        }
    }

    [Test]
    public async Task SerializeMessageToHttpLokiWithoutOrdering()
    {
        // Prepare the events to be sent to loki
        var events = CreateLokiEvents();

        // Configure the ILokiHttpClient such that we intercept the JSON content and simulate an OK response from Loki.
        string serializedJsonMessage = null;
        var httpClient = Substitute.For<ILokiHttpClient>();
        _ = httpClient
            .PostAsync("loki/api/v1/push", Arg.Any<HttpContent>())
            .Returns(async (info) =>
            {
                // Intercept the json content so that we can verify it.
                serializedJsonMessage = await info.Arg<HttpContent>().ReadAsStringAsync().ConfigureAwait(false);
                Assert.AreEqual("application/json", info.Arg<HttpContent>().Headers.ContentType.MediaType);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient, orderWrites: false, CompressionLevel.NoCompression);
        await transport.WriteLogEventsAsync(events).ConfigureAwait(false);

        // Verify the json message format
        Assert.AreEqual(
            "{\"streams\":[{\"stream\":{\"env\":\"unittest\",\"job\":\"Job1\"},\"values\":[[\"1640598506000000000\",\"Info|Receive message from A with destination B.\"],[\"1640598508200000000\",\"Info|Another event has occured here.\"],[\"1640598505100000000\",\"Info|Event from another stream.\"]]}]}",
            serializedJsonMessage);
    }

    [Test]
    public async Task SerializeMessageToHttpLokiWithOrdering()
    {
        // Prepare the events to be sent to loki
        var events = CreateLokiEvents();

        // Configure the ILokiHttpClient such that we intercept the JSON content and simulate an OK response from Loki.
        string serializedJsonMessage = null;
        var httpClient = Substitute.For<ILokiHttpClient>();
        _ = httpClient
            .PostAsync("loki/api/v1/push", Arg.Any<HttpContent>())
            .Returns(async (info) =>
            {
                // Intercept the json content so that we can verify it.
                serializedJsonMessage = await info.Arg<HttpContent>().ReadAsStringAsync().ConfigureAwait(false);
                Assert.AreEqual("application/json", info.Arg<HttpContent>().Headers.ContentType.MediaType);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient, orderWrites: true, CompressionLevel.NoCompression);
        await transport.WriteLogEventsAsync(events).ConfigureAwait(false);

        // Verify the json message format
        Assert.AreEqual(
            "{\"streams\":[{\"stream\":{\"env\":\"unittest\",\"job\":\"Job1\"},\"values\":[[\"1640598505100000000\",\"Info|Event from another stream.\"],[\"1640598506000000000\",\"Info|Receive message from A with destination B.\"],[\"1640598508200000000\",\"Info|Another event has occured here.\"]]}]}",
            serializedJsonMessage);
    }

    [Test]
    public async Task SerializeMessageToHttpLokiSingleEvent()
    {
        // Prepare the event to be sent to loki
        var lokiEvent = CreateLokiEvents().ToList()[2];

        // Configure the ILokiHttpClient such that we intercept the JSON content and simulate an OK response from Loki.
        string serializedJsonMessage = null;
        var httpClient = Substitute.For<ILokiHttpClient>();
        _ = httpClient
            .PostAsync("loki/api/v1/push", Arg.Any<HttpContent>())
            .Returns(async (info) =>
            {
                // Intercept the json content so that we can verify it.
                serializedJsonMessage = await info.Arg<HttpContent>().ReadAsStringAsync().ConfigureAwait(false);
                Assert.AreEqual("application/json", info.Arg<HttpContent>().Headers.ContentType.MediaType);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient, false, CompressionLevel.NoCompression);
        await transport.WriteLogEventsAsync(lokiEvent).ConfigureAwait(false);

        // Verify the json message format
        Assert.AreEqual(
            "{\"streams\":[{\"stream\":{\"env\":\"unittest\",\"job\":\"Job1\"},\"values\":[[\"1640598505100000000\",\"Info|Event from another stream.\"]]}]}",
            serializedJsonMessage);
    }

    [Test]
    public void ThrowOnHttpClientException()
    {
        var httpClient = Substitute.For<ILokiHttpClient>();
        _ = httpClient
            .PostAsync("loki/api/v1/push", Arg.Any<HttpContent>())
            .ThrowsAsync(new Exception("Something went wrong whem sending HTTP message."));

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient, false, CompressionLevel.NoCompression);
        var exception = Assert.ThrowsAsync<Exception>(() => transport.WriteLogEventsAsync(CreateLokiEvents()));
        Assert.AreEqual("Something went wrong whem sending HTTP message.", exception.Message);
    }

    [Test]
    public void ThrowOnNonSuccessResponseCode()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new { reason = "A stream must have a least one label." }),
        };
        var httpClient = Substitute.For<ILokiHttpClient>();
        _ = httpClient
            .PostAsync("loki/api/v1/push", Arg.Any<HttpContent>())
            .Returns(Task.FromResult(response));

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient, false, CompressionLevel.NoCompression);
        var exception = Assert.ThrowsAsync<HttpRequestException>(() => transport.WriteLogEventsAsync(CreateLokiEvents()));
        Assert.AreEqual("Failed pushing logs to Loki.", exception.Message);

#if NET6_0_OR_GREATER
        Assert.AreEqual(HttpStatusCode.Conflict, exception.StatusCode);
#endif
    }

    [Test]
    [TestCase(CompressionLevel.Fastest)]
    [TestCase(CompressionLevel.Optimal)]
#if NET6_0_OR_GREATER
    [TestCase(CompressionLevel.SmallestSize)]
#endif
    public async Task CompressMessage(CompressionLevel level)
    {
        // Prepare the events to be sent to loki
        var events = CreateLokiEvents(3);

        // Configure the ILokiHttpClient such that we intercept the JSON content and simulate an OK response from Loki.
        string serializedJsonMessage = null;
        var httpClient = Substitute.For<ILokiHttpClient>();
        _ = httpClient
            .PostAsync("loki/api/v1/push", Arg.Any<HttpContent>())
            .Returns(async (info) =>
            {
                // Intercept the gzipped json content so that we can verify it.
                using var stream = await info.Arg<HttpContent>().ReadAsStreamAsync().ConfigureAwait(false);
                Assert.True(info.Arg<HttpContent>().Headers.ContentEncoding.Any(s => s == "gzip"));
                using var stream2 = new GZipStream(stream, CompressionMode.Decompress);
                var buffer = new byte[128000];
                var length = await stream2.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                serializedJsonMessage = Encoding.UTF8.GetString(buffer, 0, length);

                Assert.True(info.Arg<HttpContent>().Headers.ContentEncoding.Any(s => s == "gzip"));
                Assert.AreEqual("application/json", info.Arg<HttpContent>().Headers.ContentType.MediaType);

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Send the logging request
        using var transport = new HttpLokiTransport(httpClient, orderWrites: false, level);
        await transport.WriteLogEventsAsync(events).ConfigureAwait(false);

        // Verify the json message format
        Assert.AreEqual(
            "{\"streams\":[{\"stream\":{\"env\":\"unittest\",\"job\":\"Job1\"},\"values\":[[\"1640598506000000000\",\"Info|Receive message from A with destination B.\"],[\"1640598508200000000\",\"Info|Another event has occured here.\"],[\"1640598505100000000\",\"Info|Event from another stream.\"]]}]}",
            serializedJsonMessage);
    }
}
