namespace MobyParkApi.Services;

public interface IPaymentGenerationService
{
    /// <summary>
    /// Maakt een payment aan vanuit een afgesloten parking session
    /// </summary>
    Task<int> CreatePaymentFromParkingSessionAsync(int parkingSessionId);

    /// <summary>
    /// Maakt een payment aan vanuit een afgelopen reservation
    /// </summary>
    Task<int> CreatePaymentFromReservationAsync(int reservationId);

    /// <summary>
    /// Controleert of er al een payment bestaat voor een parking session
    /// </summary>
    Task<bool> PaymentExistsForParkingSessionAsync(int parkingSessionId);

    /// <summary>
    /// Controleert of er al een payment bestaat voor een reservation
    /// </summary>
    Task<bool> PaymentExistsForReservationAsync(int reservationId);
}