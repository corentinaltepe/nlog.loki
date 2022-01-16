using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
              .WithOptions(ConfigOptions.JoinSummary)
              .WithOptions(ConfigOptions.DisableLogFile);

            var summary = BenchmarkRunner.Run(new[]{
                BenchmarkConverter.TypeToBenchmarks(typeof(Benchmarks), config),
                BenchmarkConverter.TypeToBenchmarks(typeof(Transport), config),
                BenchmarkConverter.TypeToBenchmarks(typeof(LokiEventsSerializer), config),
            });
        }
    }
}
