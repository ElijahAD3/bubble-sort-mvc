using System.Text.Json;
using BubbleSortMvc.Models;

namespace BubbleSortMvc.Services;

public class TrainingInputException(string message) : Exception(message);
public record DatasetInspection(string[] Columns, int Rows);

public class TrainingService(PythonRunner runner)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DatasetInspection> InspectAsync(string csv, CancellationToken cancellationToken)
    {
        return await RunAsync<DatasetInspection>(new { operation = "inspect", csv }, TimeSpan.FromSeconds(15), cancellationToken);
    }

    public async Task<TrainingResults> TrainAsync(string csv, string classificationTarget, string regressionTarget, CancellationToken cancellationToken)
    {
        return await RunAsync<TrainingResults>(new { operation = "train", csv, classificationTarget, regressionTarget },
            TimeSpan.FromMinutes(2), cancellationToken);
    }

    private async Task<T> RunAsync<T>(object input, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync("train_models.py", input, timeout, cancellationToken);
        if (result.ExitCode == 2)
        {
            using var error = JsonDocument.Parse(result.Output);
            if (error.RootElement.TryGetProperty("error", out var message) && message.ValueKind == JsonValueKind.String)
                throw new TrainingInputException(message.GetString()!);
        }
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Training process exited with code {result.ExitCode}: {result.Error}");
        return JsonSerializer.Deserialize<T>(result.Output, JsonOptions) ?? throw new InvalidOperationException("Python returned an invalid result.");
    }
}
