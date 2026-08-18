using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto
{
    // Main response DTO
    public class ReservationResponseDto
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        
        public ParkingLotSummaryDto ParkingLot { get; set; } = new();
        public VehicleSummaryDto Vehicle { get; set; } = new();
    }

    // Nested DTOs - only essential information
    public class ParkingLotSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Tariff { get; set; }
        public decimal DayTariff { get; set; }
        public int Capacity { get; set; }
        public string? Coordinates { get; set; }
    }

    public class VehicleSummaryDto
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Year { get; set; }
    }
}