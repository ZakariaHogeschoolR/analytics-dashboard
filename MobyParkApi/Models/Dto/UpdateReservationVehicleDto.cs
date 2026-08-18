using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

public class UpdateReservationVehicleDto
{
    [Required(ErrorMessage = "Kenteken is verplicht")]
    [MinLength(6, ErrorMessage = "Kenteken moet minimaal 6 tekens bevatten")]
    [MaxLength(9, ErrorMessage = "Kenteken moet maximaal 9 tekens bevatten")]
    [RegularExpression(@"^[A-Z0-9\- ]+$",
        ErrorMessage = "Kenteken mag alleen letters, cijfers, streepjes en spaties bevatten")]
    public string LicensePlate { get; set; } = string.Empty;
}

