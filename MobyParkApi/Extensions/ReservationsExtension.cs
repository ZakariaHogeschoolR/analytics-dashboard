using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Extensions
{
    public static class ReservationExtensions
    {
        public static ReservationResponseDto ToResponseDto(this Reservations reservation)
        {
            return new ReservationResponseDto
            {
                Id = reservation.Id,
                StartTime = reservation.StartTime,
                EndTime = reservation.EndTime ?? reservation.StartTime.AddHours(1), // Fallback voor oude data
                Status = reservation.Status,
                Cost = (decimal)reservation.Cost,
                CreatedAt = reservation.CreatedAt,
                ModifiedAt = reservation.ModifiedAt,
                ParkingLot = new ParkingLotSummaryDto
                {
                    Id = reservation.ParkingLot?.Id ?? 0,
                    Name = reservation.ParkingLot?.Name ?? string.Empty,
                    Location = reservation.ParkingLot?.Location ?? string.Empty,
                    Address = reservation.ParkingLot?.Address ?? string.Empty,
                    Tariff = (decimal)(reservation.ParkingLot?.Tariff ?? 0),
                    DayTariff = (decimal)(reservation.ParkingLot?.DayTariff ?? 0),
                    Capacity = reservation.ParkingLot?.Capacity ?? 0,
                    Coordinates = reservation.ParkingLot?.Coordinates
                },
                Vehicle = new VehicleSummaryDto
                {
                    Id = reservation.Vehicle?.Id ?? 0,
                    LicensePlate = reservation.Vehicle?.LicensePlate ?? string.Empty,
                    Make = reservation.Vehicle?.Make ?? string.Empty,
                    Model = reservation.Vehicle?.Model ?? string.Empty,
                    Color = reservation.Vehicle?.Color ?? string.Empty,
                    Year = reservation.Vehicle?.Year ?? 0
                }
            };
        }

        public static List<ReservationResponseDto> ToResponseDtoList(this List<Reservations> reservations)
        {
            return reservations.Select(r => r.ToResponseDto()).ToList();
        }
    }
}