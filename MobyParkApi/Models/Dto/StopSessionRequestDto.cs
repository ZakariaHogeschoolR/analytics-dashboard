using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

/// <summary>
/// Request voor het stoppen van een parking sessie
/// </summary>
public class StopSessionRequestDto
{
    [Required(ErrorMessage = "Kenteken is verplicht")]
    public string LicensePlate { get; set; } = string.Empty;
}