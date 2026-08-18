using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

/// <summary>
/// DTO voor profiel reactivatie
/// </summary>
public class ReactivateProfileDto
{
    [Required(ErrorMessage = "Gebruikersnaam is verplicht")]
    [MinLength(3, ErrorMessage = "Gebruikersnaam moet minimaal 3 tekens bevatten")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wachtwoord is verplicht")]
    [MinLength(6, ErrorMessage = "Wachtwoord moet minimaal 6 tekens bevatten")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO voor reactivatie resultaat
/// </summary>
public class ReactivateResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}