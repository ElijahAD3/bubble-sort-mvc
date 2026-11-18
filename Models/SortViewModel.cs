using System.ComponentModel.DataAnnotations;

namespace BubbleSortMvc.Models;

public class SortViewModel
{
    [Required(ErrorMessage = "Enter at least one value.")]
    [StringLength(20000)]
    [Display(Name = "Values, one per line")]
    public string Values { get; set; } = "";

    public string[]? SortedValues { get; set; }
    public string? DataType { get; set; }
}
