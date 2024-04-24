using System;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets.Wrappers;
using NUnit.Framework;

namespace NLog.Loki.Tests;

[TestFixture]
public class LokiTargetTests
{
    [Test]
    public void Write()
    {
        var configuration = new LoggingConfiguration();

        using var lokiTarget = new LokiTarget
        {
            Endpoint = "http://grafana.lvh.me:3100",
            IncludeScopeProperties = true,
            Labels = {
                new LokiTargetLabel {
                    Name = "env",
                    Layout = Layout.FromString("${basedir}")
                },
                new LokiTargetLabel {
                    Name = "server",
                    Layout = Layout.FromString("${machinename:lowercase=true}")
                },
                new LokiTargetLabel {
                    Name = "level",
                    Layout = Layout.FromString("${level:lowercase=true}")
                }
            }
        };

        var target = new BufferingTargetWrapper(lokiTarget)
        {
            BufferSize = 500
        };

        configuration.AddTarget("loki", target);

        var rule = new LoggingRule("*", LogLevel.Debug, target);
        configuration.LoggingRules.Add(rule);

        LogManager.Configuration = configuration;

        var log = LogManager.GetLogger(typeof(LokiTargetTests).FullName);

        for(var n = 0; n < 100; ++n)
        {
            using(ScopeContext.PushProperty("env", "dev"))
            {
                log.Fatal("Hello world");
            }

            using(ScopeContext.PushProperty("server", Environment.MachineName))
            {
                log.Info($"hello again {n}");

                log.Info($"hello again {n * 2}");
                log.Warn($"hello again {n * 3}");
            }

            using(ScopeContext.PushProperty("cfg", "v1"))
                log.Error($"hello again {n * 4}");

            try
            {
                throw new InvalidOperationException();
            }
            catch(Exception e)
            {
                log.Error(e);
            }
        }

        LogManager.Shutdown();
    }

    [Test]
    [TestCase("${environment:SCHEME}://${environment:HOST}:3100/", ExpectedResult = typeof(HttpLokiTransport))]
    [TestCase("udp://${environment:HOST}:3100/", ExpectedResult = typeof(NullLokiTransport))]
    [TestCase("", ExpectedResult = typeof(NullLokiTransport))]
    [TestCase(null, ExpectedResult = typeof(NullLokiTransport))]
    public Type GetLokiTransport(string endpointLayout)
    {
        Environment.SetEnvironmentVariable("SCHEME", "https");
        Environment.SetEnvironmentVariable("HOST", "loki.lvh.me");

        var endpoint = Layout.FromString(endpointLayout);
        using var target = new LokiTarget();
        using var lokiTargetTransport = target.GetLokiTransport(
            endpoint, "1", null, null, false,
            "https://myproxy.com", "proxyDomain\\proxyUserA", "proxyPasswordA");
        return lokiTargetTransport.GetType();
    }
}

