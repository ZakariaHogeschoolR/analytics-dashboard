using System.ComponentModel.DataAnnotations;
namespace MobyParkApi.Models.Dto
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public required string LicensePlate { get; set; }
        public required string PaymentStatus { get; set; }
        public decimal Cost { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class CreatedPaymentDto
    {
        [Required(ErrorMessage = "ParkingLotId is verplicht.")]
        [Range(1, int.MaxValue, ErrorMessage = "ParkingLotId moet groter zijn dan 0.")]
        public int ParkingLotId { get; set; }

        [Required(ErrorMessage = "Kenteken is verplicht.")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Kenteken moet tussen 2 en 20 tekens lang zijn.")]
        public string LicensePlate { get; set; } = string.Empty;

        [Required(ErrorMessage = "Duur is verplicht.")]
        [Range(1, 1440, ErrorMessage = "Duur moet tussen 1 en 1440 minuten liggen.")]
        public int Duration { get; set; }

        [MaxLength(50, ErrorMessage = "Kortingscode mag maximaal 50 tekens bevatten")]
        public string? DiscountCode { get; set; }
    }
    public class UpdatePaymentStatusDto
    {
        public required string NewStatus { get; set; } // "Paid" of "Failed"
    }
}