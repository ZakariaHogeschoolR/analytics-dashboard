using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

/// <summary>
/// Request voor het aanmaken van een nieuw voertuig
/// </summary>
public class CreateVehicleRequestDto
{
    [Required(ErrorMessage = "Kenteken is verplicht")]
    public string LicensePlate { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Merk is verplicht")]
    public string Make { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Model is verplicht")]
    public string Model { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Kleur is verplicht")]
    public string Color { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Bouwjaar is verplicht")]
    public int Year { get; set; }
}