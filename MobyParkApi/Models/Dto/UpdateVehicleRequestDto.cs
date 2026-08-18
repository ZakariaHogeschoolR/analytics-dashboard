namespace MobyParkApi.Models.Dto;

/// <summary>
/// Request voor het updaten van een voertuig (alle velden optioneel)
/// </summary>
public class UpdateVehicleRequestDto
{
    public string? LicensePlate { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public int? Year { get; set; }
}