using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

// P08 PoC: does ScreenRecorderLib 7.0.1 load and record on .NET 10 / win-x64?
// Roadmap P08 checklist items probed here:
//   [x] build/load on a modern .NET target + x64 (native/mixed-mode concern)
//   [x] short MP4 recording of the primary screen (video only, audio disabled)
//   [x] basic metrics (wall time, file size)
// Not probed here (need representative hardware/consent): dual-monitor DPI,
// microphone-only, webcam, hardware encoding matrix, weakest-device perf.
// Media files are deleted after metrics; only the report survives.

var report = new PocReport();
var sw = Stopwatch.StartNew();
string? mediaPath = null;

try
{
    Console.WriteLine($"[poc] ScreenRecorderLib probe on {Environment.OSVersion.VersionString}, " +
        $".NET {Environment.Version}, x64={Environment.Is64BitProcess}");

    // 1. Load + options construction: forces the managed + native layers.
    var options = new ScreenRecorderLib.RecorderOptions
    {
        SourceOptions = new ScreenRecorderLib.SourceOptions
        {
            RecordingSources =
            {
                new ScreenRecorderLib.DisplayRecordingSource(
                    ScreenRecorderLib.DisplayRecordingSource.MainMonitor)
            }
        },
        AudioOptions = new ScreenRecorderLib.AudioOptions
        {
            IsAudioEnabled = false
        }
    };

    var recorder = ScreenRecorderLib.Recorder.CreateRecorder(options);
    report.LoadOk = true;
    report.LoadMs = sw.ElapsedMilliseconds;
    Console.WriteLine($"[poc] LOAD OK in {report.LoadMs} ms (managed + native layers resolved)");

    // 2. Short recording probe of the primary screen.
    mediaPath = Path.Combine(AppContext.BaseDirectory, $"poc-capture-{DateTime.Now:HHmmss}.mp4");
    var completed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    recorder.OnRecordingComplete += (sender, args) =>
    {
        report.CompleteFilePath = args.FilePath;
        completed.TrySetResult(args.FilePath ?? string.Empty);
    };
    recorder.OnRecordingFailed += (sender, args) =>
    {
        report.FailError = args.Error;
        completed.TrySetResult(string.Empty);
    };

    var recordStart = Stopwatch.StartNew();
    recorder.Record(mediaPath);
    Console.WriteLine("[poc] RECORD started; sleeping 4s then stopping...");

    await Task.Delay(TimeSpan.FromSeconds(4));
    recorder.Stop();

    var winner = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(30)));
    report.RecordMs = recordStart.ElapsedMilliseconds;
    report.RecordFinished = winner == completed.Task;

    if (File.Exists(mediaPath))
    {
        var fi = new FileInfo(mediaPath);
        report.FileSizeBytes = fi.Length;
        report.FileSizeMb = Math.Round(fi.Length / 1024.0 / 1024.0, 3);
        // Media is deleted after metrics; nothing is kept on disk.
        fi.Delete();
        report.MediaDeleted = true;
        Console.WriteLine($"[poc] RECORD finished: {report.RecordMs} ms, {report.FileSizeMb} MB; media deleted.");
    }
    else
    {
        Console.WriteLine($"[poc] no file produced. CompleteFilePath='{report.CompleteFilePath}' Error='{report.FailError}'");
    }

    recorder.Dispose();
}
catch (Exception ex)
{
    report.Exception = $"{ex.GetType().Name}: {ex.Message}";
    Console.WriteLine($"[poc] EXCEPTION: {report.Exception}");
    Console.WriteLine(ex.StackTrace);
}

report.TotalMs = sw.ElapsedMilliseconds;
report.Host = new HostFacts
{
    Os = Environment.OSVersion.VersionString,
    NetVersion = Environment.Version.ToString(),
    Machine = Environment.MachineName,
    Is64Bit = Environment.Is64BitProcess,
    Interactive = Environment.UserInteractive
};

var reportPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "results", "srl-report.json");
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
}));
Console.WriteLine($"[poc] report written: {reportPath}");

return (report.LoadOk == true && report.RecordFinished == true && report.FileSizeBytes > 0) ? 0 : 1;

internal sealed class PocReport
{
    public bool? LoadOk { get; set; }
    public long? LoadMs { get; set; }
    public bool? RecordFinished { get; set; }
    public long? RecordMs { get; set; }
    public string? CompleteFilePath { get; set; }
    public string? FailError { get; set; }
    public long FileSizeBytes { get; set; }
    public double FileSizeMb { get; set; }
    public bool MediaDeleted { get; set; }
    public string? Exception { get; set; }
    public long TotalMs { get; set; }
    public HostFacts? Host { get; set; }
}

internal sealed class HostFacts
{
    public string Os { get; set; } = string.Empty;
    public string NetVersion { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public bool Is64Bit { get; set; }
    public bool Interactive { get; set; }
}
