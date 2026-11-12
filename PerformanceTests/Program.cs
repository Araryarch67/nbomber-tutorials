using System.Text.Json;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

// Argumen: kasus [angka]. Angka = RPS throughput atau persen gagal checkout.
string selected = args.FirstOrDefault()?.ToLowerInvariant() ?? "load";
string[] validCases = ["load", "stress", "throughput", "latency", "error-rate"];
if (!validCases.Contains(selected) || args.Length > 2)
{
    Console.Error.WriteLine("Gunakan: load | stress | throughput [RPS] | latency | error-rate [persen]");
    return 2;
}
int rate = 300;
int failurePercent = 10;
if (args.Length == 2)
{
    if (!int.TryParse(args[1], out int value)
        || (selected != "throughput" && selected != "error-rate")
        || (selected == "throughput" && (value < 1 || value > 2000))
        || (selected == "error-rate" && (value < 0 || value > 100)))
    {
        Console.Error.WriteLine("Throughput: 1..2000 RPS; error-rate: 0..100 persen.");
        return 2;
    }
    if (selected == "throughput") rate = value;
    else failurePercent = value;
}

string runId = $"{selected}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
string reportFolder = Path.GetFullPath(Path.Combine("reports", runId));
Directory.CreateDirectory(reportFolder);
string summaryPath = Path.Combine(reportFolder, "summary.json");

using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = 1000 })
{
    BaseAddress = new Uri("http://127.0.0.1:5080"),
    Timeout = TimeSpan.FromSeconds(3)
};

try
{
    using var health = await http.GetAsync("/health");
    health.EnsureSuccessStatusCode();
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine($"API belum siap: {ex.Message}");
    return 2;
}

ScenarioProps CreateScenario(string name, string path, bool post = false)
{
    return Scenario.Create(name, async _ =>
    {
        try
        {
            using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, path);
            using var response = await http.SendAsync(request);
            byte[] body = await response.Content.ReadAsByteArrayAsync();
            string status = ((int)response.StatusCode).ToString();
            if (!response.IsSuccessStatusCode)
                return Response.Fail(statusCode: status, message: "HTTP non-2xx");
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("ok", out var ok)
                || ok.ValueKind != JsonValueKind.True)
                return Response.Fail(statusCode: "invalid_body", message: "ok harus true");
            return Response.Ok(statusCode: status, sizeBytes: body.Length);
        }
        catch (TaskCanceledException)
        {
            return Response.Fail(statusCode: "timeout", message: "Timeout client 3 detik");
        }
        catch (HttpRequestException)
        {
            return Response.Fail(statusCode: "network_error", message: "Koneksi gagal");
        }
        catch (JsonException)
        {
            return Response.Fail(statusCode: "invalid_json", message: "JSON tidak valid");
        }
    }).WithWarmUpDuration(TimeSpan.FromSeconds(10)).WithMaxFailCount(100_000);
}

TimeSpan Seconds(int value) => TimeSpan.FromSeconds(value);
var interval = Seconds(1);
var scenario = selected switch
{
    // Ramp berlangsung pada warm-up agar SLO hanya menilai fase stabil.
    "load" => CreateScenario("load_catalog", "/api/catalog")
        .WithoutWarmUp()
        .WithWarmUpSimulations(Simulation.RampingInject(50, interval, Seconds(30)))
        .WithLoadSimulations(Simulation.Inject(50, interval, Seconds(120))),
    "stress" => CreateScenario("stress_inventory", "/api/inventory")
        .WithLoadSimulations(
            Simulation.Inject(50, interval, Seconds(30)),
            Simulation.Inject(100, interval, Seconds(30)),
            Simulation.Inject(150, interval, Seconds(30)),
            Simulation.Inject(250, interval, Seconds(30)),
            Simulation.Inject(50, interval, Seconds(30))),
    "throughput" => CreateScenario("throughput_prices", "/api/prices")
        .WithLoadSimulations(Simulation.Inject(rate, interval, Seconds(60))),
    "latency" => CreateScenario("latency_search", "/api/search")
        .WithLoadSimulations(Simulation.Inject(50, interval, Seconds(60))),
    "error-rate" => CreateScenario("error_rate_checkout",
            $"/api/checkout?failurePercent={failurePercent}", post: true)
        .WithLoadSimulations(Simulation.Inject(50, interval, Seconds(60))),
    _ => throw new InvalidOperationException()
};

// Runtime threshold ditampilkan di report; keputusan exit code tetap eksplisit di bawah.
scenario = scenario.WithThresholds(Threshold.Create(s => s.Fail.Request.Percent < 1));
var result = NBomberRunner.RegisterScenarios(scenario)
    .WithTestSuite("quality_attribute_tutorial").WithTestName(runId)
    .WithReportFolder(reportFolder)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Csv, ReportFormat.Md).Run();
var stats = result.ScenarioStats.Single();
long okCount = stats.Ok.Request.Count;
long failCount = stats.Fail.Request.Count;
long total = okCount + failCount;
double errorRate = total > 0 ? 100.0 * failCount / total : 100;
double successfulRps = stats.Ok.Request.RPS;
double p95 = stats.Ok.Latency.Percent95;
double p99 = stats.Ok.Latency.Percent99;

// Guard: sebuah run yang terputus/kurang sampel tidak boleh dianggap expected failure.
int plannedRequests = selected switch
{
    "load" => 50 * 120,
    "stress" => (50 + 100 + 150 + 250 + 50) * 30,
    "throughput" => rate * 60,
    _ => 50 * 60
};
bool executionValid = okCount > 0 && total >= plannedRequests * 0.95;
// HTTP 503 merupakan kegagalan yang dirancang pada target latihan.
// Timeout/koneksi/body rusak tidak boleh menyamar sebagai expected failure CI.
var failureCodes = stats.Fail.StatusCodes.Select(x => x.StatusCode).ToArray();
executionValid &= failureCodes.All(code => code == "503");
var checks = new Dictionary<string, bool> { ["error_rate"] = errorRate < 1 };
if (selected != "stress") checks["p95"] = p95 <= (selected == "latency" ? 300 : 200);
if (selected == "load") checks["throughput"] = successfulRps >= 49;
if (selected == "throughput") checks["throughput"] = successfulRps >= rate * 0.98;
if (selected == "latency") checks["p99"] = p99 <= 800;

bool sloPassed = executionValid && checks.Values.All(x => x);
var summary = new
{
    selected, rate, failurePercent, plannedRequests, executionValid, sloPassed,
    okCount, failCount, total, errorRate, successfulRps, p95, p99, failureCodes, checks
};
File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"\nall={total}, ok={okCount}, fail={failCount}, error={errorRate:F2}%");
Console.WriteLine($"Successful RPS={successfulRps:F2}, p95 OK={p95:F2} ms, p99 OK={p99:F2} ms");
foreach (var check in checks) Console.WriteLine($"{check.Key}: {(check.Value ? "PASS" : "FAIL")}");
Console.WriteLine($"Summary: {summaryPath}");
Console.WriteLine(!executionValid ? "INVALID: sampel tidak cukup." : sloPassed ? "SLO PASS" : "SLO FAIL");
return !executionValid ? 2 : sloPassed ? 0 : 1;
