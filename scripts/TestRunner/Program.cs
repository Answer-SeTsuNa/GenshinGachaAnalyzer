using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

// 直接使用同一 xUnit 执行器、测试程序集和断言；避免 VSTest 子进程父进程监听的环境权限问题。
var sink = new Results();
using var framework = new XunitTestFramework(sink);
using var executor = framework.GetExecutor(Assembly.Load("GenshinGachaAnalyzer.Tests").GetName());
executor.RunAll(sink, TestFrameworkOptions.ForDiscovery(), TestFrameworkOptions.ForExecution(new TestAssemblyConfiguration { ParallelizeTestCollections = false }));
if (!sink.Done.Wait(TimeSpan.FromMinutes(3))) throw new TimeoutException("xUnit 执行超时");
var rows = sink.Rows.OrderBy(t => t.Name).ToArray();
var report = new { Runner = "xUnit 2 in-process, original test assembly", Total = sink.Total, Passed = rows.Count(r => r.Result == "Passed"), Failed = rows.Count(r => r.Result == "Failed"), Skipped = rows.Count(r => r.Result == "Skipped"), Errors = sink.Errors.ToArray(), Tests = rows };
var reportPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"xUnit: {report.Passed} passed, {report.Failed} failed, {report.Skipped} skipped, {report.Total} total; report: {reportPath}");
return report.Total > 0 && report.Total == rows.Length && report.Failed == 0 && report.Errors.Length == 0 ? 0 : 1;

sealed record Row(string Name, string Result, string? Message);
sealed class Results : Xunit.Sdk.LongLivedMarshalByRefObject, IMessageSink
{
    public readonly ConcurrentQueue<Row> Rows = new();
    public readonly ConcurrentQueue<string> Errors = new();
    public readonly ManualResetEventSlim Done = new();
    public int Total;
    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestPassed passed: Rows.Enqueue(new(passed.Test.DisplayName, "Passed", null)); break;
            case ITestFailed failed: Rows.Enqueue(new(failed.Test.DisplayName, "Failed", string.Join("\n", failed.Messages))); break;
            case ITestSkipped skipped: Rows.Enqueue(new(skipped.Test.DisplayName, "Skipped", skipped.Reason)); break;
            case IErrorMessage error: Errors.Enqueue(string.Join("\n", error.Messages)); break;
            case ITestAssemblyFinished finished: Total = finished.TestsRun; Done.Set(); break;
        }
        return true;
    }
}
