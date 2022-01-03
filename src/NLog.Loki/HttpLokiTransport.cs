using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using NLog.Loki.Model;
using NLog.Common;

namespace NLog.Loki
{
    /// <remarks>
    /// See https://grafana.com/docs/loki/latest/api/#examples-4
    /// </remarks>
    internal class HttpLokiTransport : ILokiTransport
    {
        private static readonly JsonSerializerOptions JsonOptions;

        static HttpLokiTransport()
        {
            JsonOptions = new JsonSerializerOptions();
            JsonOptions.Converters.Add(new LokiEventsSerializer());
            JsonOptions.Converters.Add(new LokiEventSerializer());
        }

        private readonly ILokiHttpClient lokiHttpClient;

        public HttpLokiTransport(ILokiHttpClient lokiHttpClient)
        {
            this.lokiHttpClient = lokiHttpClient;
        }

        public async Task WriteLogEventsAsync(IEnumerable<LokiEvent> lokiEvents)
        {
            using var jsonStreamContent = JsonContent.Create(lokiEvents, options: JsonOptions);
            using var response = await lokiHttpClient.PostAsync("loki/api/v1/push", jsonStreamContent).ConfigureAwait(false);
            await ValidateHttpResponse(response);
        }

        public async Task WriteLogEventsAsync(LokiEvent lokiEvent)
        {
            using var jsonStreamContent = JsonContent.Create(lokiEvent, options: JsonOptions);
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
            throw new HttpRequestException("Failed pushing logs to Loki.", inner: null, response.StatusCode);
        }
    }
}
