using System.Text.Json;

namespace BubbleSortMvc.Services;

public class PythonSortService(PythonRunner runner)
{
    public async Task<string[]> SortAsync(string dataType, string[] values, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync("bubble_sort.py", new { type = dataType, values }, TimeSpan.FromSeconds(5), cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Python exited with code {result.ExitCode}: {result.Error}");
        var sorted = JsonSerializer.Deserialize<string[]>(result.Output);
        if (sorted is null || sorted.Length != values.Length)
            throw new InvalidOperationException("Python returned an invalid result.");
        return sorted;
    }
}
