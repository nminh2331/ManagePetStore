using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Models;

public partial class PetStoreManagementContext
{
    public virtual DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HotelBooking>(entity =>
        {
            entity.Property(booking => booking.ActualCheckInAt).HasColumnType("datetime");
            entity.Property(booking => booking.ActualCheckOutAt).HasColumnType("datetime");
            entity.Property(booking => booking.ScheduledCheckInDate).HasColumnType("datetime");
            entity.Property(booking => booking.ScheduledCheckOutDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<FoodDiaryLog>(entity =>
        {
            entity.HasIndex(log => log.HotelBookingId, "IX_FoodDiaryLogs_HotelBookingId");
            entity.HasIndex(log => new { log.HotelBookingId, log.OccurredAt }, "IX_FoodDiaryLogs_Booking_OccurredAt");
            entity.Property(log => log.OccurredAt).HasColumnType("datetime");
            entity.Property(log => log.ActivityType).HasMaxLength(30);
            entity.Property(log => log.Title).HasMaxLength(150);
            entity.Property(log => log.MediaUrl).HasMaxLength(500);
            entity.Property(log => log.MediaType).HasMaxLength(30);

            entity.HasOne(log => log.HotelBooking)
                .WithMany(booking => booking.FoodDiaryLogs)
                .HasForeignKey(log => log.HotelBookingId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FoodDiaryLogs_HotelBookings");
        });

        modelBuilder.Entity<CustomerNotification>(entity =>
        {
            entity.HasKey(notification => notification.NotificationId);
            entity.HasIndex(
                notification => new { notification.CustomerId, notification.IsRead, notification.CreatedAt },
                "IX_CustomerNotifications_Customer_Unread_CreatedAt");
            entity.Property(notification => notification.Type).HasMaxLength(30);
            entity.Property(notification => notification.Title).HasMaxLength(180);
            entity.Property(notification => notification.Message).HasMaxLength(500);
            entity.Property(notification => notification.LinkUrl).HasMaxLength(500);
            entity.Property(notification => notification.CreatedAt).HasColumnType("datetime");
            entity.Property(notification => notification.ReadAt).HasColumnType("datetime");

            entity.HasOne(notification => notification.Customer)
                .WithMany(customer => customer.CustomerNotifications)
                .HasForeignKey(notification => notification.CustomerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_CustomerNotifications_Customers");

            entity.HasOne(notification => notification.HotelBooking)
                .WithMany(booking => booking.CustomerNotifications)
                .HasForeignKey(notification => notification.HotelBookingId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_CustomerNotifications_HotelBookings");
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.HasIndex(record => record.HotelBookingId, "IX_MedicalRecords_HotelBookingId");

            entity.HasOne(record => record.HotelBooking)
                .WithMany(booking => booking.MedicalRecords)
                .HasForeignKey(record => record.HotelBookingId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_MedicalRecords_HotelBookings");
        });

        modelBuilder.Entity<PetBioTimeline>(entity =>
        {
            entity.HasIndex(timeline => timeline.HotelBookingId, "IX_PetBioTimelines_HotelBookingId");

            entity.HasOne(timeline => timeline.HotelBooking)
                .WithMany(booking => booking.PetBioTimelines)
                .HasForeignKey(timeline => timeline.HotelBookingId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_PetBioTimelines_HotelBookings");
        });
    }
}
