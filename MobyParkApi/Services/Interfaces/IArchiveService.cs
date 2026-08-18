using MobyParkApi.Models;

namespace MobyParkApi.Services;

public interface IArchiveService
{
   Task<int> ArchiveReservationsAsync();

    Task<int> ArchiveVehicleAndReservationsAsync(Vehicles vehicle, string archivedBy);
  
    Task ArchiveReservationAsync(Reservations reservation, string archivedBy, string role, string status = "Cancelled");

    Task<(bool Success, string ErrorMessage)> ArchiveAndDeletePaymentAsync(Payments payment, string role, int archivedByUserId);
}

