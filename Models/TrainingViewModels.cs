using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BubbleSortMvc.Models;

public class TrainingUploadViewModel
{
    [Required, Display(Name = "CSV dataset")]
    public IFormFile? File { get; set; }
}

public class TrainingFormViewModel
{
    [ValidateNever]
    public int DatasetId { get; set; }
    [ValidateNever]
    public string FileName { get; set; } = "";
    [ValidateNever]
    public int RowCount { get; set; }
    [ValidateNever]
    public string[] Columns { get; set; } = [];

    [Required, Display(Name = "Categorical classification target")]
    public string ClassificationTarget { get; set; } = "";

    [Required, Display(Name = "Numeric regression target")]
    public string RegressionTarget { get; set; } = "";
}

public class TrainingResults
{
    [System.Text.Json.Serialization.JsonPropertyName("train_rows")]
    public int TrainRows { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("test_rows")]
    public int TestRows { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("forest_accuracy")]
    public double ForestAccuracy { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("linear_rmse")]
    public double LinearRmse { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("transformer_accuracy")]
    public double TransformerAccuracy { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("transformer_rmse")]
    public double TransformerRmse { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("transformer_optimizer")]
    public string TransformerOptimizer { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("transformer_epochs")]
    public int TransformerEpochs { get; set; }
}

public record TrainingHistoryViewModel(IReadOnlyList<TrainingDataset> Datasets, IReadOnlyList<TrainingRun> Runs);
