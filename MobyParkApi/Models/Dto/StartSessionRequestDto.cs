using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

/// <summary>
/// Request voor het starten van een parking sessie
/// </summary>
public class StartSessionRequestDto
{
    [Required(ErrorMessage = "Kenteken is verplicht")]
    public string LicensePlate { get; set; } = string.Empty;
}