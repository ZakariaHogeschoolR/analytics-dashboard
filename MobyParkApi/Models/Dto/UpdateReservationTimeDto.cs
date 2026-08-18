using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto;

public class UpdateReservationTimeDto
{
    [Required(ErrorMessage = "Startdatum is verplicht")]
    [RegularExpression(@"^\d{4}-\d{1,2}-\d{1,2} \d{1,2}:\d{2}(:\d{2})?$",
        ErrorMessage = "Startdatum moet in het formaat YYYY-MM-DD HH:MM:SS zijn")]
    public string StartDate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Einddatum is verplicht")]
    [RegularExpression(@"^\d{4}-\d{1,2}-\d{1,2} \d{1,2}:\d{2}(:\d{2})?$",
        ErrorMessage = "Einddatum moet in het formaat YYYY-MM-DD HH:MM:SS zijn")]
    public string EndDate { get; set; } = string.Empty;
}

