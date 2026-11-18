using System.Security.Claims;
using System.Text.Json;
using BubbleSortMvc.Data;
using BubbleSortMvc.Models;
using BubbleSortMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BubbleSortMvc.Controllers;

[Authorize]
public class SortController(
    PythonSortService sorter, AiClassifier classifier, ApplicationDbContext db, ILogger<SortController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new SortViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SortViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);

        var values = model.Values.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(value => value.Trim()).ToArray();
        if (values.Length > 100 || values.Any(value => value.Length is 0 or > 200))
        {
            ModelState.AddModelError(nameof(model.Values), "Enter 1 to 100 nonempty values, each at most 200 characters.");
            return View(model);
        }

        try
        {
            var dataType = await classifier.ClassifyAsync(values, cancellationToken);
            if (!SortInputValidator.IsValid(dataType, values))
            {
                ModelState.AddModelError(nameof(model.Values), $"The values do not match the AI's {dataType} classification. Use one consistent type and ISO dates (YYYY-MM-DD).");
                return View(model);
            }

            var sorted = await sorter.SortAsync(dataType, values, cancellationToken);
            db.SavedSorts.Add(new SavedSort
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                DataType = dataType,
                OriginalJson = JsonSerializer.Serialize(values),
                SortedJson = JsonSerializer.Serialize(sorted),
                CreatedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            model.DataType = dataType;
            model.SortedValues = sorted;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.ComponentModel.Win32Exception or IOException or JsonException or TimeoutException)
        {
            logger.LogError(ex, "Classification or sorting failed");
            ModelState.AddModelError("", "The AI classification or sorting service failed. Check that your AI provider and Python are running and configured.");
        }
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var history = await db.SavedSorts.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedUtc).ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);
        return View(history);
    }
}
