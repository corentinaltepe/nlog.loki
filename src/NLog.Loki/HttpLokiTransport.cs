using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using NLog.Common;
using NLog.Loki.Model;

namespace NLog.Loki
{
    /// <remarks>
    /// See https://grafana.com/docs/loki/latest/api/#examples-4
    /// </remarks>
    internal class HttpLokiTransport : ILokiTransport
    {
        private readonly JsonSerializerOptions jsonOptions;
        private readonly ILokiHttpClient lokiHttpClient;

        public HttpLokiTransport(ILokiHttpClient lokiHttpClient, bool orderWrites)
        {
            this.lokiHttpClient = lokiHttpClient;

            jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new LokiEventsSerializer(orderWrites));
            jsonOptions.Converters.Add(new LokiEventSerializer());
        }

        public async Task WriteLogEventsAsync(IEnumerable<LokiEvent> lokiEvents)
        {
            using var jsonStreamContent = JsonContent.Create(lokiEvents, options: jsonOptions);
            using var response = await lokiHttpClient.PostAsync("loki/api/v1/push", jsonStreamContent).ConfigureAwait(false);
            await ValidateHttpResponse(response);
        }

        public async Task WriteLogEventsAsync(LokiEvent lokiEvent)
        {
            using var jsonStreamContent = JsonContent.Create(lokiEvent, options: jsonOptions);
            using var response = await lokiHttpClient.PostAsync("loki/api/v1/push", jsonStreamContent).ConfigureAwait(false);
            await ValidateHttpResponse(response);
        }

        private static async ValueTask ValidateHttpResponse(HttpResponseMessage response)
        {
            if(response.IsSuccessStatusCode)
                return;

            // Read the response's content
            string content = response.Content == null ? null :
                await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            InternalLogger.Error("Failed pushing logs to Loki. Code: {Code}. Reason: {Reason}. Message: {Message}.",
                response.StatusCode, response.ReasonPhrase, content);
            
            #if NET6_0_OR_GREATER
                throw new HttpRequestException("Failed pushing logs to Loki.", inner: null, response.StatusCode);
            #else
                throw new HttpRequestException("Failed pushing logs to Loki.");
            #endif
        }
    }
}
