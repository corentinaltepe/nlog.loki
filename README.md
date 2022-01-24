# NLog Loki Target

![CI](https://github.com/corentinaltepe/nlog.loki/workflows/CI/badge.svg?branch=master)
[![NuGet](https://img.shields.io/nuget/v/NLog.Targets.Loki)](https://www.nuget.org/packages/NLog.Targets.Loki)
[![codecov](https://codecov.io/gh/corentinaltepe/nlog.loki/branch/master/graph/badge.svg?token=84N5XB4J09)](https://codecov.io/gh/corentinaltepe/nlog.loki)

This is an [NLog](https://nlog-project.org/) target that sends messages to [Loki](https://grafana.com/oss/loki/) using Loki's HTTP Push API. Available on .NET Standard 2.0 (.NET Core 2.0 and .NET Framework 4.6.1, above).

> Loki is a horizontally-scalable, highly-available, multi-tenant log aggregation system inspired by Prometheus. It is designed to be very cost effective and easy to operate.

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
      layout="${level}|${message}${onexception:|${exception:format=type,message,method:maxInnerExceptionLevel=5:innerFormat=shortType,message,method}}|source=${logger}">
      <!-- Loki requires at least one label associated with the log stream. 
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

`username` and `password` are optional fields, used for basic authentication with Loki.

`orderWrites` - Orders the logs by timestamp before sending them to loki when logs are batched in a single HTTP call. This is required if you use Loki v2.3 or below. But it is not required if you use Loki v2.4 or above (see [out-of-order writes](https://grafana.com/docs/loki/next/configuration/#accept-out-of-order-writes)). You are strongly advised to set this value to `false` when using Loki v2.4+ since it reduces allocations by about 20% by the serializer (default `true`).

`label` elements can be used to enrich messages with additional [labels](https://grafana.com/docs/loki/latest/design-documents/labels/). `label/@layout` support usual NLog layout renderers.

### Async Target
`NLog.Loki` is an [async target](https://github.com/NLog/NLog/wiki/How-to-write-a-custom-async-target#asynctasktarget-features). You should **not** wrap it in an [AsyncWrapper target](https://github.com/NLog/NLog/wiki/AsyncWrapper-target). The following configuration options are supported. Make sure to adjust them to the expected throughput and criticality of your application's logs, especially the batch size, retry count and task delay.

`taskTimeoutSeconds` - How many seconds a Task is allowed to run before it is cancelled (default 150 secs).

`retryDelayMilliseconds` - How many milliseconds to wait before next retry (default 500ms, and will be doubled on each retry).

`retryCount` - How many attempts to retry the same Task, before it is aborted (default 0, meaning no retry).

`batchSize` - Gets or sets the number of log events that should be processed in a batch by the lazy writer thread (default 1).

`taskDelayMilliseconds` - How many milliseconds to delay the actual write operation to optimize for batching (default 1 ms).

`queueLimit` - Gets or sets the limit on the number of requests in the lazy writer thread request queue (default 10000).

`overflowAction` - Gets or sets the action to be taken when the lazy writer thread request queue count exceeds the set limit (default Discard).