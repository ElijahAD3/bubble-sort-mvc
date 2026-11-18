namespace BubbleSortMvc.Models;

public class SavedSort
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string DataType { get; set; } = "";
    public string OriginalJson { get; set; } = "";
    public string SortedJson { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}
