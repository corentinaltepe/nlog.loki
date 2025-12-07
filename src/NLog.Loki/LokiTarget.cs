using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
//using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using NLog.Loki.Model;
using NLog.Targets;

namespace NLog.Loki;

[Target("loki")]
public class LokiTarget : AsyncTaskTarget
{
    private static readonly LokiLabels EmptyLabels = new LokiLabels(new HashSet<LokiLabel>());
    private readonly Lazy<ILokiTransport> _lazyLokiTransport;

    [RequiredParameter]
    public Layout Endpoint { get; set; }

    public Layout Username { get; set; }

    public Layout Password { get; set; }

    public Layout Tenant { get; set; }

    public bool EventPropertiesAsLabels { get; set; }

    /// <summary>
    /// Orders the logs by timestamp before sending them to Loki. False by default.
    /// Required as <see langword="true"/> before Loki v2.4. Leave as <see langword="false"/> if you are running Loki v2.4 or higher.
    /// See <see href="https://grafana.com/docs/loki/latest/configuration/#accept-out-of-order-writes"/>.
    /// </summary>
    public bool OrderWrites { get; set; } = false;

    /// <summary>
    /// Defines if the HTTP messages sent to Loki must be gzip compressed, and with which compression level.
    /// Possible values: NoCompression, Optimal (default), Fastest and SmallestSize (.NET 6 support only).
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    public Layout ProxyUrl { get; set; }
    public Layout ProxyUser { get; set; }
    public Layout ProxyPassword { get; set; }

    [ArrayParameter(typeof(LokiTargetLabel), "label")]
    public IList<LokiTargetLabel> Labels { get; }

    private static Func<Uri, string, string, string, Uri, string, string, ILokiHttpClient> LokiHttpClientFactory { get; } = CreateLokiHttpClient;

    private const string TenantHeader = "X-Scope-OrgID";

    public LokiTarget()
    {
        Labels = new List<LokiTargetLabel>();

        _lazyLokiTransport = new Lazy<ILokiTransport>(
            () => GetLokiTransport(Endpoint, Tenant, Username, Password, OrderWrites, ProxyUrl, ProxyUser, ProxyPassword),
            LazyThreadSafetyMode.ExecutionAndPublication);

        InitializeTarget();
    }

    protected override Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
    {
        var @event = GetLokiEvent(logEvent);
        return _lazyLokiTransport.Value.WriteLogEventsAsync(@event);
    }

    protected override Task WriteAsyncTask(IList<LogEventInfo> logEvents, CancellationToken cancellationToken)
    {
        if (logEvents.Count == 1)
            return WriteAsyncTask(logEvents[0], cancellationToken); // Skip allocating yield engine

        var events = GetLokiEvents(logEvents);
        return _lazyLokiTransport.Value.WriteLogEventsAsync(events);
    }

    private IEnumerable<LokiEvent> GetLokiEvents(IEnumerable<LogEventInfo> logEvents)
    {
        foreach(var e in logEvents)
            yield return GetLokiEvent(e);
    }

    private LokiEvent GetLokiEvent(LogEventInfo logEvent)
    {
        var labels = RenderAndMapLokiLabels(Labels, logEvent, EventPropertiesAsLabels);
        return new LokiEvent(labels, logEvent.TimeStamp, RenderLogEvent(Layout, logEvent));
    }

    private LokiLabels RenderAndMapLokiLabels(
    IList<LokiTargetLabel> lokiTargetLabels,
    LogEventInfo logEvent,
    bool eventPropertiesAsLabels)
    {
        var labelCount = lokiTargetLabels.Count;
        if(eventPropertiesAsLabels && logEvent.HasProperties)
            labelCount += logEvent.Properties.Count;
        if(labelCount == 0)
            return EmptyLabels;

#if NETSTANDARD || NETFRAMEWORK
        var set = new HashSet<LokiLabel>();
#else
        var set = new HashSet<LokiLabel>(labelCount);
#endif
        for(var i = 0; i < lokiTargetLabels.Count; i++)
            _ = set.Add(new LokiLabel(lokiTargetLabels[i].Name, RenderLogEvent(lokiTargetLabels[i].Layout, logEvent)));

        // programmer might also want to create labels in loki using event properties
        // This goes against Loki best pratices as it tends to create too many streams.
        // But the feature was requested twice in a short span so it is included in the library,
        // with warnings in the readme.
        if(eventPropertiesAsLabels && logEvent.HasProperties)
        {
            foreach(var property in logEvent.Properties)
                _ = set.Add(new LokiLabel(property.Key.ToString(), property.Value?.ToString() ?? ""));
        }

        return new LokiLabels(set);
    }

    internal ILokiTransport GetLokiTransport(
        Layout endpoint, Layout tenant, Layout username, Layout password, bool orderWrites,
        Layout proxyUrl, Layout proxyUser, Layout proxyPassword)
    {
        var endpointUri = RenderLogEvent(endpoint, LogEventInfo.CreateNullEvent());
        var tnt = RenderLogEvent(tenant, LogEventInfo.CreateNullEvent());
        var usr = RenderLogEvent(username, LogEventInfo.CreateNullEvent());
        var pwd = RenderLogEvent(password, LogEventInfo.CreateNullEvent());
        var pxUser = RenderLogEvent(proxyUser, LogEventInfo.CreateNullEvent());
        var pxPassword = RenderLogEvent(proxyPassword, LogEventInfo.CreateNullEvent());

        var pxUrl = RenderLogEvent(proxyUrl, LogEventInfo.CreateNullEvent());
        Uri.TryCreate(pxUrl, UriKind.Absolute, out var pxUri);

        if(Uri.TryCreate(endpointUri, UriKind.Absolute, out var uri))
        {
            if(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                return new HttpLokiTransport(
                    LokiHttpClientFactory(uri, tnt, usr, pwd, pxUri, pxUser, pxPassword),
                    orderWrites,
                    CompressionLevel);
        }

        InternalLogger.Warn("LokiTarget: Unable to create a valid Loki Endpoint URI from '{0}'", endpoint);
        return new NullLokiTransport();
    }

    internal static ILokiHttpClient CreateLokiHttpClient(
        Uri uri,
        string tenant,
        string username,
        string password,
        Uri proxyUri,
        string proxyUser,
        string proxyPassword)
    {
        // Configure handler for proxy settings
#if NETSTANDARD || NETFRAMEWORK
        var handler = new HttpClientHandler();
#else
        var handler = new SocketsHttpHandler();
#endif
        handler.UseProxy = proxyUri is not null;
        if(handler.UseProxy)
        {
            var useDefaultCredentials = string.IsNullOrWhiteSpace(proxyUser);
            handler.Proxy = new WebProxy(proxyUri)
            {
                UseDefaultCredentials = useDefaultCredentials
            };
            if(!useDefaultCredentials)
            {
                var cred = proxyUser.Split('\\');
                handler.Proxy.Credentials = cred.Length == 1 ?
                    new NetworkCredential
                    {
                        UserName = proxyUser,
                        Password = proxyPassword ?? string.Empty
                    }
                    : new NetworkCredential
                    {
                        Domain = cred[0],
                        UserName = cred[1],
                        Password = proxyPassword ?? string.Empty
                    };
            }
        }

        // Here, inject http proxy settings
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = uri
        };
        if(!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }
        if(!string.IsNullOrEmpty(tenant))
        {
            if(!httpClient.DefaultRequestHeaders.Any(h => h.Key == TenantHeader))
            {
                httpClient.DefaultRequestHeaders.Add(TenantHeader, tenant);
            }
        }
        return new LokiHttpClient(httpClient);
    }

    private bool _isDisposed;
    protected override void Dispose(bool isDisposing)
    {
        if(!_isDisposed)
        {
            if(isDisposing)
            {
                if(_lazyLokiTransport.IsValueCreated)
                    _lazyLokiTransport.Value.Dispose();
            }
            _isDisposed = true;
        }
        base.Dispose(isDisposing);
    }
}
