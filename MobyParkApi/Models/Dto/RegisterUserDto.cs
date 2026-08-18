using System.ComponentModel.DataAnnotations;
using MobyParkApi.Validators;

namespace MobyParkApi.Models.Dto
{
    public class RegisterUserDto
    {
        [Required]
        [MinLength(2, ErrorMessage = "Naam moet minimaal 2 letters bevatten")]
        [MaxLength(100, ErrorMessage = "Naam mag maximaal 100 tekens bevatten")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MinLength(5, ErrorMessage = "Gebruikersnaam moet minimaal 5 tekens bevatten")]
        [MaxLength(50, ErrorMessage = "Gebruikersnaam mag maximaal 50 tekens bevatten")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", 
            ErrorMessage = "Gebruikersnaam mag alleen letters, cijfers en underscores bevatten")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Wachtwoord moet minimaal 8 tekens bevatten!")]
        [MaxLength(128, ErrorMessage = "Wachtwoord mag maximaal 128 tekens bevatten!")]
        [RegularExpression("^(?=.*[A-Z])(?=.*\\d).+$",
            ErrorMessage = "Wachtwoord moet minstens 1 hoofdletter en 1 cijfer bevatten")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [EmailAddress(ErrorMessage = "Ongeldig e-mailadres")]
        [MaxLength(255, ErrorMessage = "E-mailadres mag maximaal 255 tekens bevatten")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DutchPhoneNumber]
        [MaxLength(20, ErrorMessage = "Telefoonnummer mag maximaal 20 tekens bevatten")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Range(1900, 2010, ErrorMessage = "Geboortejaar moet tussen 1900 en 2010 liggen")]
        public int BirthYear { get; set; }
    }
    
    public class RegisterResultDto
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}