using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto
{
    public class WalkUpDto
    {
        [Required(ErrorMessage = "Kenteken is verplicht")]
        [MinLength(6, ErrorMessage = "Kenteken moet minimaal 6 tekens bevatten")]
        [MaxLength(9, ErrorMessage = "Kenteken moet maximaal 9 tekens bevatten")]
        [RegularExpression(@"^[A-Z0-9\- ]+$", ErrorMessage = "Kenteken mag alleen letters, cijfers, streepjes en spaties bevatten")]
        public string LicensePlate { get; set; } = string.Empty;

        [Required(ErrorMessage = "Startdatum is verplicht")]
        [RegularExpression(@"^\d{4}-\d{1,2}-\d{1,2} \d{1,2}:\d{2}(:\d{2})?$", ErrorMessage = "Startdatum moet in het formaat YYYY-MM-DD HH:MM:SS zijn")]
        public string StartDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parkeerplaats ID is verplicht")]
        [Range(1, int.MaxValue, ErrorMessage = "Parkeerplaats ID moet groter zijn dan 0")]
        public int ParkingLotId { get; set; }
    }
}