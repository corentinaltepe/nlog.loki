using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Moq;
using NLog.Loki.Model;
using NUnit.Framework;

namespace NLog.Loki.Tests;

public class HttpLokiTransportTests
{
    private static List<LokiEvent> CreateLokiEvents()
    {
        var date = new DateTime(2021, 12, 27, 9, 48, 26, DateTimeKind.Utc);
        return new List<LokiEvent> {
            new(new LokiLabels(new LokiLabel("env", "unittest"), new LokiLabel("job", "Job1")),
                date, "Info|Receive message from A with destination B."),
            new(new LokiLabels(new LokiLabel("env", "unittest"), new LokiLabel("job", "Job1")),
                date + TimeSpan.FromSeconds(2.2), "Info|Another event has occured here."),
            new(new LokiLabels(new LokiLabel("env", "unittest"), new LokiLabel("job", "Job1")),
                date - TimeSpan.FromSeconds(0.9), "Info|Event from another stream."),
        };
    }

    [Test]
    public async Task SerializeMessageToHttpLokiWithoutOrdering()
    {
        // Prepare the events to be sent to loki
        var events = CreateLokiEvents();

        // Configure the ILokiHttpClient such that we intercept the JSON content and simulate an OK response from Loki.
        string serializedJsonMessage = null;
        var httpClient = new Mock<ILokiHttpClient>();
        _ = httpClient.Setup(c => c.PostAsync("loki/api/v1/push", It.IsAny<HttpContent>()))
            .Returns<string, HttpContent>(async (s, json) =>
            {
                // Intercept the json content so that we can verify it.
                serializedJsonMessage = await json.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient.Object, orderWrites: false);
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
        var httpClient = new Mock<ILokiHttpClient>();
        _ = httpClient.Setup(c => c.PostAsync("loki/api/v1/push", It.IsAny<HttpContent>()))
            .Returns<string, HttpContent>(async (s, json) =>
            {
                // Intercept the json content so that we can verify it.
                serializedJsonMessage = await json.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient.Object, orderWrites: true);
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
        var lokiEvent = CreateLokiEvents()[2];

        // Configure the ILokiHttpClient such that we intercept the JSON content and simulate an OK response from Loki.
        string serializedJsonMessage = null;
        var httpClient = new Mock<ILokiHttpClient>();
        _ = httpClient.Setup(c => c.PostAsync("loki/api/v1/push", It.IsAny<HttpContent>()))
                .Returns<string, HttpContent>(async (s, json) =>
                {
                    // Intercept the json content so that we can verify it.
                    serializedJsonMessage = await json.ReadAsStringAsync().ConfigureAwait(false);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient.Object, false);
        await transport.WriteLogEventsAsync(lokiEvent).ConfigureAwait(false);

        // Verify the json message format
        Assert.AreEqual(
                    "{\"streams\":[{\"stream\":{\"env\":\"unittest\",\"job\":\"Job1\"},\"values\":[[\"1640598505100000000\",\"Info|Event from another stream.\"]]}]}",
                    serializedJsonMessage);
    }

    [Test]
    public void ThrowOnHttpClientException()
    {
        var httpClient = new Mock<ILokiHttpClient>();
        _ = httpClient
            .Setup(c => c.PostAsync("loki/api/v1/push", It.IsAny<HttpContent>()))
            .ThrowsAsync(new Exception("Something went wrong whem sending HTTP message."));

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient.Object, false);
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
        var httpClient = new Mock<ILokiHttpClient>();
        _ = httpClient
            .Setup(c => c.PostAsync("loki/api/v1/push", It.IsAny<HttpContent>()))
            .Returns(Task.FromResult(response));

        // Send the logging request
        var transport = new HttpLokiTransport(httpClient.Object, false);
        var exception = Assert.ThrowsAsync<HttpRequestException>(() => transport.WriteLogEventsAsync(CreateLokiEvents()));
        Assert.AreEqual("Failed pushing logs to Loki.", exception.Message);
        
        #if NET6_0_OR_GREATER
            Assert.AreEqual(HttpStatusCode.Conflict, exception.StatusCode);
        #endif
    }
}
