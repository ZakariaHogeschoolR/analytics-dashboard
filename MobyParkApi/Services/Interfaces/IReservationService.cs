using MobyParkApi.Models;
using MobyParkApi.Models.Dto;

namespace MobyParkApi.Services
{
    public interface IReservationService
    {
        Task<ReservationResponseDto?> GetReservationByIdAsync(int id, int currentUserId, string userRole);
        Task<List<ReservationResponseDto>> GetAllUserReservationsAsync(int currentUserId);
        Task<ReservationResponseDto> CreateReservationAsync(ReservationDto dto, int currentUserId);
        Task<ReservationResponseDto> UpdateReservationAsync(int id, ReservationDto dto, int currentUserId, string userRole);
        Task<ReservationResponseDto> UpdateReservationTimeAsync(int id, UpdateReservationTimeDto dto, int currentUserId, string userRole);
        Task<ReservationResponseDto> UpdateReservationVehicleAsync(int id, UpdateReservationVehicleDto dto, int currentUserId, string userRole);
        Task DeleteReservationAsync(int id, int currentUserId, string userRole, string username);
    }
}