using BenchmarkDotNet.Attributes;
using Ryn.Core;

namespace Ryn.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class EscapeForJsBenchmarks
{
    private string _clean = null!;
    private string _withSpecials = null!;
    private string _longClean = null!;
    private string _longWithSpecials = null!;
    private string _backslashHeavy = null!;

    [GlobalSetup]
    public void Setup()
    {
        _clean = "hello world simple text";
        _withSpecials = "line1\nline2\rend with 'quotes' and \\backslash";
        _longClean = new string('a', 1024);
        _longWithSpecials = string.Create(1024, 0, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = i % 10 == 0 ? '\n' : (char)('a' + (i % 26));
        });
        _backslashHeavy = @"C:\Users\test\path\to\file.txt";
    }

    [Benchmark(Description = "Short clean string (no escaping)")]
    public string ShortClean()
    {
        return RynWebView.EscapeForJs(_clean);
    }

    [Benchmark(Description = "Short string with special chars")]
    public string ShortWithSpecials()
    {
        return RynWebView.EscapeForJs(_withSpecials);
    }

    [Benchmark(Description = "Long clean string (1024 chars)")]
    public string LongClean()
    {
        return RynWebView.EscapeForJs(_longClean);
    }

    [Benchmark(Description = "Long string with newlines")]
    public string LongWithSpecials()
    {
        return RynWebView.EscapeForJs(_longWithSpecials);
    }

    [Benchmark(Description = "Backslash-heavy path string")]
    public string BackslashHeavy()
    {
        return RynWebView.EscapeForJs(_backslashHeavy);
    }
}
