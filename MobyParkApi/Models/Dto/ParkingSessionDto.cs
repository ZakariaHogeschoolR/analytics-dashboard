using System;

namespace MobyParkApi.Models.Dto;

/// <summary>
/// DTO voor parking session data
/// </summary>
public class ParkingSessionDto
{
    public int id { get; set; }
    public int parkingLotId { get; set; }
    public string licensePlate { get; set; } = string.Empty;
    public DateTime started { get; set; }
    public DateTime stopped { get; set; }
    public int userId { get; set; }
    public bool isWalkUp { get; set; }
    public int durationMinutes { get; set; }
    public decimal cost { get; set; }
    public string? paymentStatus { get; set; }
    public DateTime createdAt { get; set; }
    public int originalSessionId { get; set; }
}

