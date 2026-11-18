using System.Diagnostics;
using System.Text.Json;

namespace BubbleSortMvc.Services;

public record PythonResult(int ExitCode, string Output, string Error);

public class PythonRunner(IWebHostEnvironment environment, IConfiguration configuration)
{
    public async Task<PythonResult> RunAsync(string script, object input, TimeSpan duration, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = configuration["PythonExecutable"] ?? "python3",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(Path.Combine(environment.ContentRootPath, "python", script));

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Python could not be started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(input).AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            return new PythonResult(process.ExitCode, await outputTask, await errorTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Python timed out.");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
