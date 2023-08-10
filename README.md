# NLog Loki Target

![Build](https://github.com/corentinaltepe/nlog.loki/workflows/Build/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/NLog.Targets.Loki)](https://www.nuget.org/packages/NLog.Targets.Loki)
[![codecov](https://codecov.io/gh/corentinaltepe/nlog.loki/branch/master/graph/badge.svg?token=84N5XB4J09)](https://codecov.io/gh/corentinaltepe/nlog.loki)

This is an [NLog](https://nlog-project.org/) target that sends messages to [Grafana Loki](https://grafana.com/oss/loki/) using Grafana Loki's HTTP Push API. Available on .NET Standard 2.0 (.NET Core 2.0, .NET Framework 4.6.1 and higher). For a gRPC client implementation, please use [this target](https://github.com/corentinaltepe/nlog.loki.grpc) instead.

> Grafana Loki is a horizontally-scalable, highly-available, multi-tenant log aggregation system inspired by Prometheus. It is designed to be very cost effective and easy to operate.

## Installation

The NLog.Loki NuGet package can be found [here](https://www.nuget.org/packages/NLog.Targets.Loki). You can install it via one of the following commands below:

NuGet command:

    Install-Package NLog.Targets.Loki

.NET Core CLI command:

    dotnet add package NLog.Targets.Loki

## Usage

Under .NET Core, [remember to register](https://github.com/nlog/nlog/wiki/Register-your-custom-component) `NLog.Loki` as an extension assembly with NLog. You can now add a Loki target [to your configuration file](https://github.com/nlog/nlog/wiki/Tutorial#Configure-NLog-Targets-for-output):

```xml
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  
  <extensions>
    <add assembly="NLog.Loki" />
  </extensions>

  <!-- Loki target is async, so there is no need to wrap it in an async target wrapper. -->
  <targets>
    <target 
      name="loki" 
      xsi:type="loki"
      batchSize="200"
      taskDelayMilliseconds="500"
      endpoint="http://localhost:3100"
      username="myusername"
      password="secret"
      orderWrites="true"
      compressionLevel="noCompression"
      layout="${level}|${message}${onexception:|${exception:format=type,message,method:maxInnerExceptionLevel=5:innerFormat=shortType,message,method}}|source=${logger}"
      eventPropertiesAsLabels="false"
      sendLastFormatParameter="false"
      proxyUrl="http://proxy:8888"
      proxyUser="username"
      proxyPassword="secret">

      <!-- Prefer using a JsonLayout as defined below, instead of using a layout as defined above -->
      <layout xsi:type="JsonLayout" includeEventProperties="true">
        <attribute name="level" layout="${level} "/>
        <attribute name="source" layout="${logger}" />
        <attribute name="message" layout="${message}" />
        <attribute name="exception" encode="false">
          <layout xsi:type="JsonLayout">
            <attribute name="type" layout="${exception:format=type}" />
            <attribute name="message" layout="${exception:format=message}" />
            <attribute name="stacktrace" layout="${exception:format=tostring}" />
          </layout>
        </attribute>
      </layout>

      <!-- Grafana Loki requires at least one label associated with the log stream. 
      Make sure you specify at least one label here. -->
      <label name="app" layout="my-app-name" />
      <label name="server" layout="${hostname:lowercase=true}" />
    </target>
  </targets>

  <rules>
    <logger name="*" minlevel="Info" writeTo="loki" />
  </rules>

</nlog>
```

`endpoint` must resolve to a fully-qualified absolute URL of the Loki Server when running in a [Single Proccess Mode](https://grafana.com/docs/loki/latest/overview/#modes-of-operation) or of the Loki Distributor when running in [Microservices Mode](https://grafana.com/docs/loki/latest/overview/#distributor).

`username` and `password` are optional fields, used for basic authentication with Grafana Loki.

`orderWrites` - Orders the logs by timestamp before sending them to Grafana loki when logs are batched in a single HTTP call. This is required if you use Loki v2.3 or lower. But it is not required if you use Grafana Loki v2.4 or higher (see [out-of-order writes](https://grafana.com/docs/loki/next/configuration/#accept-out-of-order-writes)). You are strongly advised to set this value to `false` when using Grafana Loki v2.4+ since it reduces allocations by about 20% by the serializer (default `false`).

`compressionLevel` - Gzip compression level applied if any when sending messages to Grafana Loki (default `optimal`). Possible values:

- `noCompression`: no compression applied, HTTP header will not specify a Content-Encoding with gzip value.
- `fastest`: the compression operation should complete as quickly as possible, even if the resulting file is not optimally compressed.
- `optimal`: the compression operation should be optimally compressed, even if the operation takes a longer time to complete.
- `smallestSize`: supported by .NET 6 or greater only. The compression operation should create output as small as possible, even if the operation takes a longer time to complete.

`label` elements can be used to enrich messages with additional [labels](https://grafana.com/docs/loki/latest/design-documents/labels/). `label/@layout` support usual NLog layout renderers.

`layout` - While it is possible to define a simple layout structure in the attributes of the target configuration,
  prefer using a JsonLayout to structure your logs. This will allow better parsing in Grafana Loki.

`eventPropertiesAsLabels`: creates one Grafana Loki's label per event property. Beware, this goes against [Grafana Loki's best practices](https://grafana.com/docs/loki/latest/best-practices/) since _Too many label value combinations leads to too many streams._ In order to structure your logs, you are advised to keep away from this feature and to use the `JsonLayout` provided in the example (default `false`).

`sendLastFormatParameter`: enables the last parameter of a log message format to be sent to Grafana Loki as separate fields per property. Feature `eventPropertiesAsLabels` must be `true` as well (default `false`). Example:

```csharp
// using simple anonymous type object to create custom fields for a log entry
log.Info($"Testing sendLastFormatParameter", new { publisher = "ACME Publisher", releaseDate = DateTime.Now });
```

`proxyUrl` - URL to the proxy server to use. Must include protocol (http|https) and port. If not specified, then no proxy is used (default `null`).

`proxyUser` - username to use for authentication with proxy.

`proxyPassword` - password to use for authentication with proxy.

### Async Target

`NLog.Loki` is an [async target](https://github.com/NLog/NLog/wiki/How-to-write-a-custom-async-target#asynctasktarget-features). You should **not** wrap it in an [AsyncWrapper target](https://github.com/NLog/NLog/wiki/AsyncWrapper-target). The following configuration options are supported. Make sure to adjust them to the expected throughput and criticality of your application's logs, especially the batch size, retry count and task delay.

`taskTimeoutSeconds` - How many seconds a Task is allowed to run before it is cancelled (default 150 secs).

`retryDelayMilliseconds` - How many milliseconds to wait before next retry (default 500ms, and will be doubled on each retry).

`retryCount` - How many attempts to retry the same Task, before it is aborted (default 0, meaning no retry).

`batchSize` - Gets or sets the number of log events that should be processed in a batch by the lazy writer thread (default 1).

`taskDelayMilliseconds` - How many milliseconds to delay the actual write operation to optimize for batching (default 1 ms).

`queueLimit` - Gets or sets the limit on the number of requests in the lazy writer thread request queue (default 10000).

`overflowAction` - Gets or sets the action to be taken when the lazy writer thread request queue count exceeds the set limit (default Discard).

### Upgrading From v1 To v2

The v2 of the library applies _better_ default values for common usage. Check your current configuration and compare it against the breaking
changes below to adapt to your use case.

#### Breaking Changes

- `compressionLevel` set to `optimal` by default, instead of `noCompression`. If you are certain you do no want compression,
  specify the value as `noCompression` in your configuration.
- `orderWrites` set to `false` by default. Set it to `true` only if you run on Grafana Loki v2.3 or lower, or if your logs come largely unordered
  and need to be reordered for Grafana Loki to process them.
- Dropped support for NLog v4, upgraded to NLog v5.

### JSON Layout & Loki Query

The configuration example above structures log messages and event properties in JSON.
This allows parsing messages in Grafana Loki and then applying advanced filtering and querying.

The following query would filter log messages which have a property `JobName` and a value of `A`
in Grafana Loki:

```logql
{app="my-app-name"}
| json
| JobName="A"
```

These raw messages would look like the following in Grafana Loki :

```json
{ "level": "Info ", "source": "Program", "message": "Executing job \"A\"", "JobName": "A" }
```

See [Log Queries and Parser Expressions in Loki](https://grafana.com/docs/loki/latest/logql/log_queries/#parser-expression)
for more details.

### Benchmark

See [NLog.Loki.Benchmarks](https://github.com/corentinaltepe/nlog.loki.benchmark) for benchmark between HTTP and gRPC clients for NLog targets for Grafana Loki.
