namespace BubbleSortMvc.Models;

public class TrainingDataset
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string CsvContent { get; set; } = "";
    public string ColumnsJson { get; set; } = "";
    public int RowCount { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class TrainingRun
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int TrainingDatasetId { get; set; }
    public TrainingDataset Dataset { get; set; } = null!;
    public string ClassificationTarget { get; set; } = "";
    public string RegressionTarget { get; set; } = "";
    public string ResultsJson { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}
