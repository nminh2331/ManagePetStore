using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Models;

public partial class PetStoreManagementContext
{
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
            entity.Property(log => log.OccurredAt).HasColumnType("datetime");

            entity.HasOne(log => log.HotelBooking)
                .WithMany(booking => booking.FoodDiaryLogs)
                .HasForeignKey(log => log.HotelBookingId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_FoodDiaryLogs_HotelBookings");
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
