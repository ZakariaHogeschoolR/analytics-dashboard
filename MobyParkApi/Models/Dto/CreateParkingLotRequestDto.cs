using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

public class CreateParkingLotRequestDto
{
    [Required(ErrorMessage = "Naam is verplicht")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Locatie is verplicht")]
    public string Location { get; set; } = string.Empty;
    
    [Required] 
    [RegularExpression(@"^[1-9][0-9]{3}\s?[A-Z]{2}$", ErrorMessage = "Ongeldige postcode")] 
    public string Postcode { get; set; } = string.Empty; 
    [Required] 
    public int HouseNumber { get; set; } = 0;
    [Required(ErrorMessage = "Capaciteit is verplicht")]
    public int Capacity { get; set; }
    
    public int Reserved { get; set; } = 0;
    
    [Required(ErrorMessage = "Tarief is verplicht")]
    public decimal Tariff { get; set; }

    public decimal DayTariff { get; set; } = 0;
    public int Beschikbaarheid
    {
        get
        {
            return this.Capacity - this.Reserved;
        }
    }
    
    // ✅ Nieuwe manier: lat en lng apart
    [Required(ErrorMessage = "Latitude is verplicht")]
    public double Lat { get; set; }
    
    [Required(ErrorMessage = "Longitude is verplicht")]
    public double Lng { get; set; }
}