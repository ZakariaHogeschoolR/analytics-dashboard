using System.ComponentModel.DataAnnotations;
using MobyParkApi.Models.Dto;


namespace MobyParkApi.Models.Dto
{
    public class LoginUserDto
    {
        [Required(ErrorMessage = "Gebruikesnaam is verplicht!")]
        [MinLength(5, ErrorMessage = "Gebruikersnaam moet minimaal 5 tekens bevatten")]
        [MaxLength(50, ErrorMessage = "Gebruikersnaam mag maximaal 50 tekens bevatten")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Wachtwoord is verplicht")]
        [MaxLength(128, ErrorMessage = "Wachtwoord mag maximaal 128 tekens bevatten")]
        public string Password { get; set; } = string.Empty;
    }
}
