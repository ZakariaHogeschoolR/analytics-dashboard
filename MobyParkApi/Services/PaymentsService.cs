using MobyParkApi.Data;
using MobyParkApi.Models;
using MobyParkApi.Models.Dto;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Identity;


namespace MobyParkApi.Services
{
    public class PaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDiscountCodeService _discountCodeService;
        private readonly IArchiveService _archiveService;

        public PaymentService(ApplicationDbContext context, IDiscountCodeService discountCodeService, IArchiveService archiveService)
        {
            _context = context;
            _discountCodeService = discountCodeService;
            _archiveService = archiveService;
        }

        public async Task<PaymentDto?> CreatePaymentAsync(int userId, CreatedPaymentDto dto)
        {
            
            bool isValid = IsValidDutchLicensePlate(dto.LicensePlate);
            if(!isValid)
                return null;

            // check of opgegeven kenteken ook in vehicles staat. Zo niet foutmelding geven
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == dto.LicensePlate && v.UserId == userId);
            if (vehicle == null)
            {
                throw new UnauthorizedAccessException("Kenteken komt niet overen met uw voertuigen.");
            }

            var parkingLot = await _context.ParkingLots.FindAsync(dto.ParkingLotId);
            if (parkingLot == null)
                return null;

            var calculatedCost = CalculateCost(parkingLot, dto.Duration);
            var startTime = DateTime.UtcNow;

            // APPLY DISCOUNT CODE IF PROVIDED
            decimal discountAmount = 0;
            int? discountCodeId = null;
            
            if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
            {
                try
                {
                    discountAmount = await _discountCodeService.ApplyDiscountCodeAsync(
                        dto.DiscountCode,
                        userId,
                        dto.ParkingLotId,
                        startTime,
                        calculatedCost,
                        reservationId: null,
                        paymentId: null // Will be set after payment is created
                    );

                    var discountCode = await _context.DiscountCodes
                        .FirstOrDefaultAsync(dc => dc.Code.ToUpper() == dto.DiscountCode.ToUpper());
                    
                    if (discountCode != null)
                    {
                        discountCodeId = discountCode.Id;
                    }

                    calculatedCost = Math.Max(0, calculatedCost - discountAmount);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Kortingscode is niet geldig: {ex.Message}");
                }
            }
  
            var payment = new Payments
            {
                UserId = userId,
                ParkingLotId = dto.ParkingLotId,
                LicensePlate = dto.LicensePlate,
                StartTime = startTime,
                EndTime = startTime.AddMinutes(dto.Duration),
                Duration = dto.Duration,
                PaymentStatus = "Pending",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                Cost = calculatedCost,
                Discount = discountAmount,
                DiscountCodeId = discountCodeId
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Update discount code usage with payment ID if discount was applied
            if (discountCodeId.HasValue && discountAmount > 0)
            {
                var usage = await _context.DiscountCodeUsage
                    .Where(u => u.DiscountCodeId == discountCodeId.Value && u.PaymentId == null)
                    .OrderByDescending(u => u.UsedAt)
                    .FirstOrDefaultAsync();
                
                if (usage != null)
                {
                    usage.PaymentId = payment.Id;
                    await _context.SaveChangesAsync();
                }
            }

            return MapToDto(payment);
        }

        public async Task<PaymentDto?> GetPaymentAsync(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            return payment == null ? null : MapToDto(payment);
        }

        public async Task<string?> GetPaymentStatusAsync(int paymentId, int userId)
        {
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null)
                return null;

            // Alleen eigenaar of admin mag status bekijken
            if (payment.UserId != userId && !await IsAdminAsync(userId))
                throw new UnauthorizedAccessException("Not allowed to view this payment status");

            // 🔥 Belangrijk: geef zuivere string terug
            return payment.PaymentStatus ?? "Unknown";
        }

        private async Task<bool> IsAdminAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            // Alleen uitvoeren als je een "Role" kolom hebt
            return user != null && user.Role == "Admin";
        }

        public async Task<PaymentDto?> UpdatePaymentStatusAsync(int userId, string userRole, int paymentId, string newStatus)
        {
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null)
                return null;

            bool isAdmin = userRole == "Admin";
            if (!isAdmin)
                throw new UnauthorizedAccessException("Alleen een admin mag de betaalstatus wijzigen.");

            var allowedStatuses = new[] { "Pending", "Paid", "Failed" };
            if (!allowedStatuses.Contains(newStatus))
                throw new ArgumentException("Ongeldige status. Gebruik: Pending, Paid of Failed.");

            payment.PaymentStatus = newStatus;
            payment.ModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // maak DTO voor de archivering anders is payment al verwijded
            var paymentDto =  MapToDto(payment);

            // als status paid is, payment archiveren en verwijderen
            if (newStatus == "Paid")
            {
                // await ArchiveAndDeletePaymentAsync(payment, userRole);

                var (success, errorMessage) = await  _archiveService.ArchiveAndDeletePaymentAsync(payment, userRole, userId);
            }
            return paymentDto;
        }

        public async Task<(bool success, string ErrorMessage)> DeletePaymentAsync(int paymentId, string role, int adminId)
        {
            // Payment ophalen en kijken of hij bestaat, als hij niet bestaat false returnen
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null)
                return (false, "Payment niet gevonden");

            // Archiveren als hij gevonden is
            return await _archiveService.ArchiveAndDeletePaymentAsync(payment, role, adminId);
        }


        private decimal CalculateCost(ParkingLots parkingLot, int duration)
        {
            if (duration <= 0) return 0m;

            var hours = Math.Ceiling((decimal)duration / 60m);
            var cost = Convert.ToDecimal(parkingLot.Tariff) * hours;
            return decimal.Round(cost, 2, MidpointRounding.AwayFromZero);
        }

        private PaymentDto MapToDto(Payments payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                LicensePlate = payment.LicensePlate,
                PaymentStatus = payment.PaymentStatus,
                Cost = payment.Cost,
                StartTime = payment.StartTime,
                EndTime = payment.EndTime
            };
        }
        public async Task<List<PaymentDto>> GetPaymentsByUserAsync(int requestedUserId, int currentUserId)
        {
            if (requestedUserId != currentUserId && !await IsAdminAsync(currentUserId))
                throw new UnauthorizedAccessException("Not allowed to view payments of another user");

            var payments = await _context.Payments
                .Where(p => p.UserId == requestedUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return payments.Select(p => new PaymentDto
            {
                Id = p.Id,
                LicensePlate = p.LicensePlate,
                PaymentStatus = p.PaymentStatus,
                Cost = p.Cost,
                StartTime = p.StartTime,
                EndTime = p.EndTime
            }).ToList();
        }
        public async Task<PaymentDto?> RefundPaymentAsync(int paymentId, int adminId)
        {
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null || payment.PaymentStatus == "Refunded")
                return null;

            // Zet originele betaling op "Refunded"
            payment.PaymentStatus = "Refunded";
            payment.ModifiedAt = DateTime.UtcNow;

            // Nieuwe negatieve refund-entry
            var refundEntry = new Payments
            {
                UserId = payment.UserId,
                ParkingLotId = payment.ParkingLotId,
                LicensePlate = payment.LicensePlate,
                Duration = payment.Duration,
                PaymentStatus = "Refund",
                Cost = -payment.Cost,
                StartTime = DateTime.SpecifyKind(payment.StartTime, DateTimeKind.Utc),
                EndTime = DateTime.SpecifyKind(payment.EndTime, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            _context.Payments.Add(refundEntry);
            await _context.SaveChangesAsync();

            return new PaymentDto
            {
                Id = refundEntry.Id,
                LicensePlate = refundEntry.LicensePlate,
                PaymentStatus = refundEntry.PaymentStatus,
                Cost = refundEntry.Cost,
                StartTime = refundEntry.StartTime,
                EndTime = refundEntry.EndTime
            };
        }
        public async Task<List<PaymentDto>> GetPaymentHistoryAsync(int userId, string role)
        {
            List<Payments> query;

            if (role == "Admin")
            {
                // Admin ziet alles
                query = await _context.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                // Normale gebruiker ziet alleen zijn eigen transacties
                query = await _context.Payments
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();
            }

            return query.Select(p => new PaymentDto
            {
                Id = p.Id,
                LicensePlate = p.LicensePlate,
                PaymentStatus = p.PaymentStatus,
                Cost = p.Cost,
                StartTime = p.StartTime,
                EndTime = p.EndTime
            }).ToList();
        }
       public async Task<object> CalculateUserTotalAsync(int userId)
        {
            var transactions = await _context.Payments
                .Where(p => p.UserId == userId)
                .ToListAsync();

            decimal total = decimal.Round(transactions.Sum(p => p.Cost), 2, MidpointRounding.AwayFromZero);
            int count = transactions.Count;

            return new
            {
                userId,
                transactionCount = count,
                total
            };
        }
        public async Task<object> CalculateAdminTotalAsync(int? userId)
        {
            IQueryable<Payments> query = _context.Payments.AsQueryable();

            if (userId.HasValue)
            {
                bool userExists = await _context.Users.AnyAsync(u => u.Id == userId.Value);
                if (!userExists)
                    throw new KeyNotFoundException($"User with ID {userId.Value} not found.");

                query = query.Where(p => p.UserId == userId.Value);
            }

            var transactions = await query.ToListAsync();

            decimal total = decimal.Round(transactions.Sum(p => p.Cost), 2, MidpointRounding.AwayFromZero);
            int count = transactions.Count;

            return new
            {
                userId = userId ?? 0,
                transactionCount = count,
                total
            };
        }

        /// <summary>
        /// Valideer Nederlands kenteken format
        /// Ondersteunt alle Nederlandse kenteken formaten (6, 7 en 8 karakters)
        /// </summary>
        private bool IsValidDutchLicensePlate(string licensePlate)
        {
            if (string.IsNullOrWhiteSpace(licensePlate))
                return false;

            // Verwijder streepjes en spaties, maak hoofdletters
            var cleaned = licensePlate.Replace("-", "").Replace(" ", "").ToUpper();
        
            // Check lengte (6-8 karakters voor Nederlandse kentekens)
            if (cleaned.Length < 6 || cleaned.Length > 8)
                return false;
        
            // Nederlandse kenteken formaten
            var patterns = new[]
            {
                // 6 karakters
                @"^[A-Z]{2}\d{2}\d{2}$",     // XX-99-99
                @"^\d{2}[A-Z]{2}\d{2}$",     // 99-XX-99
                @"^\d{2}\d{2}[A-Z]{2}$",     // 99-99-XX
                @"^[A-Z]{2}\d{2}[A-Z]{2}$",  // XX-99-XX
                @"^[A-Z]{2}[A-Z]{2}\d{2}$",  // XX-XX-99
                @"^\d{2}[A-Z]{2}[A-Z]{2}$",  // 99-XX-XX
            
                // 7 karakters
                @"^[A-Z]{2}\d{3}[A-Z]$",     // XX-999-X (bijv. AB-123-C)
                @"^[A-Z]\d{3}[A-Z]{2}$",     // X-999-XX
                @"^\d[A-Z]{2}\d{3}$",        // 9-XX-999
                @"^\d{3}[A-Z]{2}\d$",        // 999-XX-9
                @"^[A-Z]{3}\d{2}[A-Z]$",     // XXX-99-X
                @"^[A-Z]\d{2}[A-Z]{3}$",     // X-99-XXX
                @"^\d[A-Z]{3}\d{2}$",        // 9-XXX-99
                @"^\d{3}[A-Z]{3}$",          // 999-XXX
            
                // 8 karakters (oudere formaten)
                @"^[A-Z]{2}\d{4}$",          // XX-9999
                @"^\d{4}[A-Z]{2}$",          // 9999-XX
                @"^[A-Z]{3}\d{3}$",          // XXX-999
                @"^\d{3}[A-Z]{3}$"           // 999-XXX
            };
        
            return patterns.Any(p => Regex.IsMatch(cleaned, p));
        }
    }
}