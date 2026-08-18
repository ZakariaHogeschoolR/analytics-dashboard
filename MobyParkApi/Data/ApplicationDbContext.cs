using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MobyParkApi.Models;

namespace MobyParkApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        // ValueConverter om DateTime UTC automatisch te converteren naar Unspecified voor PostgreSQL
        private static readonly ValueConverter<DateTime, DateTime> DateTimeConverter =
            new ValueConverter<DateTime, DateTime>(
                v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                v => v);
        
        private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeConverter =
            new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue && v.Value.Kind == DateTimeKind.Utc 
                    ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) 
                    : v,
                v => v);

        // Main tables
        public DbSet<Users> Users { get; set; }
        public DbSet<Vehicles> Vehicles { get; set; }
        public DbSet<Reservations> Reservations { get; set; }
        public DbSet<Payments> Payments { get; set; }
        public DbSet<ParkingSessions> ParkingSessions { get; set; }
        public DbSet<ParkingLots> ParkingLots { get; set; }
        public DbSet<Invoices> Invoices { get; set; }
        public DbSet<DiscountCodes> DiscountCodes { get; set; }
        public DbSet<DiscountCodeUsage> DiscountCodeUsage { get; set; }
        
        // Archived tables
        public DbSet<ArchivedUsers> ArchivedUsers { get; set; }
        public DbSet<ArchivedVehicles> ArchivedVehicles { get; set; }
        public DbSet<ArchivedReservations> ArchivedReservations { get; set; }
        public DbSet<ArchivedPayments> ArchivedPayments { get; set; }
        public DbSet<ArchivedParkingSessions> ArchivedParkingSessions { get; set; }
        public DbSet<ArchivedParkingLots> ArchivedParkingLots { get; set; }
        public DbSet<ArchivedInvoices> ArchivedInvoices { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Users>().ToTable("users");
            // Configure Users timestamps
            modelBuilder.Entity<Users>()
                .Property(u => u.Created_At)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<Users>()
                .Property(u => u.Modified_At)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            
            modelBuilder.Entity<Vehicles>().ToTable("vehicles");
            // Configure Vehicles timestamps
            modelBuilder.Entity<Vehicles>()
                .Property(v => v.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Vehicles>()
                .Property(v => v.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<Reservations>().ToTable("reservations");
            // Configure Reservations timestamps explicitly
            modelBuilder.Entity<Reservations>()
                .Property(r => r.StartTime)
                .HasColumnName("start_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Reservations>()
                .Property(r => r.EndTime)
                .HasColumnName("end_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<Reservations>()
                .Property(r => r.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Reservations>()
                .Property(r => r.ModifiedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);

            modelBuilder.Entity<Payments>().ToTable("payments");
            modelBuilder.Entity<Payments>().Property(p => p.Id).HasColumnName("id");
            modelBuilder.Entity<Payments>().Property(p => p.ParkingLotId).HasColumnName("parking_lot_id");
            modelBuilder.Entity<Payments>().Property(p => p.UserId).HasColumnName("user_id");
            modelBuilder.Entity<Payments>().Property(p => p.LicensePlate).HasColumnName("license_plate");
            modelBuilder.Entity<Payments>().Property(p => p.Duration).HasColumnName("duration");
            modelBuilder.Entity<Payments>().Property(p => p.PaymentStatus).HasColumnName("payment_status");
            modelBuilder.Entity<Payments>().Property(p => p.StartTime)
                .HasColumnName("start_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Payments>().Property(p => p.EndTime)
                .HasColumnName("end_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Payments>().Property(p => p.Cost)
                .HasColumnName("cost")
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Payments>().Property(p => p.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Payments>().Property(p => p.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Payments>().Property(p => p.Discount)
                .HasColumnName("discount")
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ParkingSessions>().ToTable("parking_sessions");
            // Configure ParkingSessions timestamps explicitly
            modelBuilder.Entity<ParkingSessions>()
                .Property(ps => ps.Started)
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ParkingSessions>()
                .Property(ps => ps.Stopped)
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<ParkingSessions>()
                .Property(ps => ps.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ParkingSessions>()
                .Property(ps => ps.ModifiedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            
            modelBuilder.Entity<ParkingLots>().ToTable("parking_lots");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Id).HasColumnName("id");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Name).HasColumnName("name");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Location).HasColumnName("location");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Address).HasColumnName("address");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Capacity).HasColumnName("capacity");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Reserved).HasColumnName("reserved");
            modelBuilder.Entity<ParkingLots>().Property(p => p.Tariff)
                .HasColumnName("tariff")
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ParkingLots>().Property(p => p.DayTariff)
                .HasColumnName("day_tariff")
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<ParkingLots>().Property(p => p.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<ParkingLots>().Property(p => p.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<ParkingLots>().Property(p => p.Coordinates).HasColumnName("coordinates");
            
            modelBuilder.Entity<Invoices>().ToTable("invoices");
            // Configure Invoices timestamps
            modelBuilder.Entity<Invoices>()
                .Property(i => i.InvoiceDate)
                .HasColumnName("invoice_date")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<Invoices>()
                .Property(i => i.DueDate)
                .HasColumnName("due_date")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<Invoices>()
                .Property(i => i.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<Invoices>()
                .Property(i => i.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            
            // DiscountCodes configuration
            modelBuilder.Entity<DiscountCodes>().ToTable("discount_codes");
            modelBuilder.Entity<DiscountCodes>()
                .Property(dc => dc.StartDate)
                .HasColumnName("start_date")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<DiscountCodes>()
                .Property(dc => dc.EndDate)
                .HasColumnName("end_date")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<DiscountCodes>()
                .Property(dc => dc.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<DiscountCodes>()
                .Property(dc => dc.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<DiscountCodes>()
                .Property(dc => dc.DiscountValue)
                .HasColumnName("discount_value")
                .HasColumnType("decimal(18,2)");

            // DiscountCodeUsage configuration
            modelBuilder.Entity<DiscountCodeUsage>().ToTable("discount_code_usage");
            modelBuilder.Entity<DiscountCodeUsage>()
                .Property(dcu => dcu.UsedAt)
                .HasColumnName("used_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<DiscountCodeUsage>()
                .Property(dcu => dcu.DiscountAmount)
                .HasColumnName("discount_amount")
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DiscountCodeUsage>()
                .Property(dcu => dcu.OriginalCost)
                .HasColumnName("original_cost")
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DiscountCodeUsage>()
                .Property(dcu => dcu.FinalCost)
                .HasColumnName("final_cost")
                .HasColumnType("decimal(18,2)");

            // Add discount_code_id to Reservations if not already configured
            modelBuilder.Entity<Reservations>()
                .Property(r => r.DiscountCodeId)
                .HasColumnName("discount_code_id");

            // Add discount_code_id to Payments if not already configured
            modelBuilder.Entity<Payments>()
                .Property(p => p.DiscountCodeId)
                .HasColumnName("discount_code_id");
            
            // Archived tables configuration
            modelBuilder.Entity<ArchivedUsers>().ToTable("archived_users");
            modelBuilder.Entity<ArchivedVehicles>().ToTable("archived_vehicles");
            // Configure ArchivedVehicles timestamps
            modelBuilder.Entity<ArchivedVehicles>()
                .Property(av => av.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ArchivedVehicles>()
                .Property(av => av.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ArchivedVehicles>()
                .Property(av => av.ArchivedAt)
                .HasColumnName("archived_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            
            // Configure ArchivedReservations - map DateOnly and TimeOnly correctly
            modelBuilder.Entity<ArchivedReservations>().ToTable("archived_reservations");
            modelBuilder.Entity<ArchivedReservations>()
                .Property(ar => ar.StartTime)
                .HasColumnName("start_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ArchivedReservations>()
                .Property(ar => ar.EndDate)
                .HasColumnName("end_date")
                .HasColumnType("date");
            modelBuilder.Entity<ArchivedReservations>()
                .Property(ar => ar.EndTime)
                .HasColumnName("end_time")
                .HasColumnType("time");
            modelBuilder.Entity<ArchivedReservations>()
                .Property(ar => ar.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ArchivedReservations>()
                .Property(ar => ar.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<ArchivedReservations>()
                .Property(ar => ar.ArchivedAt)
                .HasColumnName("archived_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            
            modelBuilder.Entity<ArchivedPayments>().ToTable("archived_payments");
            modelBuilder.Entity<ArchivedParkingSessions>().ToTable("archived_parking_sessions");
            modelBuilder.Entity<ArchivedParkingLots>().ToTable("archived_parking_lots");
            // Configure ArchivedParkingLots timestamps
            modelBuilder.Entity<ArchivedParkingLots>()
                .Property(ap => ap.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<ArchivedParkingLots>()
                .Property(ap => ap.ModifiedAt)
                .HasColumnName("modified_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(NullableDateTimeConverter);
            modelBuilder.Entity<ArchivedParkingLots>()
                .Property(ap => ap.ArchivedAt)
                .HasColumnName("archived_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(DateTimeConverter);
            modelBuilder.Entity<ArchivedInvoices>().ToTable("archived_invoices");
        
        }
    }
    
}
