using System.ComponentModel.DataAnnotations;

namespace BubbleSortMvc.Models;

public class LoginViewModel
{
    [Required, StringLength(64), Display(Name = "Username")]
    public string Username { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(64, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, underscores or hyphens.")]
    public string Username { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
