using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BubbleSortMvc.Data;
using BubbleSortMvc.Models;
using BubbleSortMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BubbleSortMvc.Controllers;

[Authorize]
public class TrainingController(ApplicationDbContext db, TrainingService training, ILogger<TrainingController> logger) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public IActionResult Index() => View(new TrainingUploadViewModel());

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(1_100_000)]
    public async Task<IActionResult> Index(TrainingUploadViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.File!.Length is 0 or > 1_000_000 || !string.Equals(Path.GetExtension(model.File.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.File), "Upload a nonempty CSV file of at most 1 MB.");
            return View(model);
        }

        try
        {
            using var reader = new StreamReader(model.File.OpenReadStream(), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            var inspection = await training.InspectAsync(csv, cancellationToken);
            var dataset = new TrainingDataset
            {
                UserId = UserId,
                FileName = Path.GetFileName(model.File.FileName),
                CsvContent = csv,
                ColumnsJson = JsonSerializer.Serialize(inspection.Columns),
                RowCount = inspection.Rows,
                CreatedUtc = DateTime.UtcNow
            };
            db.TrainingDatasets.Add(dataset);
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Train), new { id = dataset.Id });
        }
        catch (DecoderFallbackException)
        {
            ModelState.AddModelError(nameof(model.File), "The CSV must be UTF-8 encoded.");
        }
        catch (TrainingInputException ex)
        {
            ModelState.AddModelError(nameof(model.File), ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or JsonException or TimeoutException)
        {
            logger.LogError(ex, "Dataset inspection failed");
            ModelState.AddModelError("", "The dataset could not be inspected. Check the Python configuration.");
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Train(int id, CancellationToken cancellationToken)
    {
        var dataset = await db.TrainingDatasets.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.UserId == UserId, cancellationToken);
        return dataset is null ? NotFound() : View(FormFor(dataset));
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("training")]
    public async Task<IActionResult> Train(int id, TrainingFormViewModel model, CancellationToken cancellationToken)
    {
        var dataset = await db.TrainingDatasets.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.UserId == UserId, cancellationToken);
        if (dataset is null) return NotFound();
        model.DatasetId = dataset.Id;
        model.FileName = dataset.FileName;
        model.RowCount = dataset.RowCount;
        model.Columns = JsonSerializer.Deserialize<string[]>(dataset.ColumnsJson)!;
        if (!ModelState.IsValid) return View(model);
        if (!model.Columns.Contains(model.ClassificationTarget) || !model.Columns.Contains(model.RegressionTarget) ||
            model.ClassificationTarget == model.RegressionTarget)
        {
            ModelState.AddModelError("", "Choose two different columns from the dataset.");
            return View(model);
        }

        try
        {
            var results = await training.TrainAsync(dataset.CsvContent, model.ClassificationTarget, model.RegressionTarget, cancellationToken);
            var run = new TrainingRun
            {
                UserId = UserId,
                TrainingDatasetId = dataset.Id,
                ClassificationTarget = model.ClassificationTarget,
                RegressionTarget = model.RegressionTarget,
                ResultsJson = JsonSerializer.Serialize(results),
                CreatedUtc = DateTime.UtcNow
            };
            db.TrainingRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Result), new { id = run.Id });
        }
        catch (TrainingInputException ex)
        {
            ModelState.AddModelError("", ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or JsonException or TimeoutException)
        {
            logger.LogError(ex, "Model training failed");
            ModelState.AddModelError("", "Training failed. Check the Python dependencies and server resources.");
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Result(int id, CancellationToken cancellationToken)
    {
        var run = await db.TrainingRuns.AsNoTracking().Include(r => r.Dataset)
            .SingleOrDefaultAsync(r => r.Id == id && r.UserId == UserId, cancellationToken);
        return run is null ? NotFound() : View(run);
    }

    [HttpGet]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var datasets = await db.TrainingDatasets.AsNoTracking().Where(d => d.UserId == UserId)
            .OrderByDescending(d => d.Id).ToListAsync(cancellationToken);
        var runs = await db.TrainingRuns.AsNoTracking().Include(r => r.Dataset).Where(r => r.UserId == UserId)
            .OrderByDescending(r => r.Id).ToListAsync(cancellationToken);
        return View(new TrainingHistoryViewModel(datasets, runs));
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var dataset = await db.TrainingDatasets.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.UserId == UserId, cancellationToken);
        return dataset is null ? NotFound() : File(Encoding.UTF8.GetBytes(dataset.CsvContent), "text/csv", $"dataset-{dataset.Id}.csv");
    }

    private static TrainingFormViewModel FormFor(TrainingDataset dataset) => new()
    {
        DatasetId = dataset.Id,
        FileName = dataset.FileName,
        RowCount = dataset.RowCount,
        Columns = JsonSerializer.Deserialize<string[]>(dataset.ColumnsJson)!
    };
}
