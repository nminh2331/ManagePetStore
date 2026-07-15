using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Models;

public partial class PetStoreManagementContext
{
    public virtual DbSet<CustomerNotification> CustomerNotifications => Set<CustomerNotification>();
    public virtual DbSet<HotelFoodOption> HotelFoodOptions => Set<HotelFoodOption>();
    public virtual DbSet<HotelBookingFoodPlan> HotelBookingFoodPlans => Set<HotelBookingFoodPlan>();
    public virtual DbSet<HotelCheckoutStatement> HotelCheckoutStatements => Set<HotelCheckoutStatement>();
    public virtual DbSet<HotelCheckoutItem> HotelCheckoutItems => Set<HotelCheckoutItem>();
    public virtual DbSet<HotelStaySpaLink> HotelStaySpaLinks => Set<HotelStaySpaLink>();

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
            entity.Property(log => log.MealType).HasMaxLength(30);
            entity.Property(log => log.ServedGrams).HasColumnType("decimal(10,2)");
            entity.Property(log => log.ExtraChargeAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(log => log.HotelBooking)
                .WithMany(booking => booking.FoodDiaryLogs)
                .HasForeignKey(log => log.HotelBookingId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FoodDiaryLogs_HotelBookings");

            entity.HasOne(log => log.FoodPlan)
                .WithMany(plan => plan.FoodDiaryLogs)
                .HasForeignKey(log => log.FoodPlanId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_FoodDiaryLogs_HotelBookingFoodPlans");
        });

        modelBuilder.Entity<HotelFoodOption>(entity =>
        {
            entity.HasKey(option => option.FoodOptionId);
            entity.Property(option => option.Name).HasMaxLength(150);
            entity.Property(option => option.Description).HasMaxLength(500);
            entity.Property(option => option.TargetSpecies).HasMaxLength(30);
            entity.Property(option => option.PricePerDay).HasColumnType("decimal(18,2)");
            entity.Property(option => option.ImageUrl).HasMaxLength(500);
            entity.Property(option => option.ProductSku).HasMaxLength(50);
            entity.HasOne(option => option.Product)
                .WithMany()
                .HasForeignKey(option => option.ProductSku)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HotelFoodOptions_Products");
        });

        modelBuilder.Entity<HotelBookingFoodPlan>(entity =>
        {
            entity.HasKey(plan => plan.FoodPlanId);
            entity.HasIndex(plan => plan.HotelBookingId).IsUnique();
            entity.Property(plan => plan.PlanType).HasMaxLength(30);
            entity.Property(plan => plan.FoodNameSnapshot).HasMaxLength(150);
            entity.Property(plan => plan.ProductSku).HasMaxLength(50);
            entity.Property(plan => plan.ProductUnitSnapshot).HasMaxLength(30);
            entity.Property(plan => plan.PricePerDaySnapshot).HasColumnType("decimal(18,2)");
            entity.Property(plan => plan.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(plan => plan.FeedingInstructions).HasMaxLength(1000);
            entity.Property(plan => plan.AllergyNotes).HasMaxLength(1000);
            entity.Property(plan => plan.CreatedAt).HasColumnType("datetime");
            entity.HasOne(plan => plan.HotelBooking)
                .WithOne(booking => booking.FoodPlan)
                .HasForeignKey<HotelBookingFoodPlan>(plan => plan.HotelBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelBookingFoodPlans_HotelBookings");
            entity.HasOne(plan => plan.FoodOption)
                .WithMany(option => option.BookingFoodPlans)
                .HasForeignKey(plan => plan.FoodOptionId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HotelBookingFoodPlans_HotelFoodOptions");
            entity.HasOne(plan => plan.Product)
                .WithMany()
                .HasForeignKey(plan => plan.ProductSku)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HotelBookingFoodPlans_Products");
        });

        modelBuilder.Entity<HotelCheckoutStatement>(entity =>
        {
            entity.HasKey(statement => statement.CheckoutStatementId);
            entity.HasIndex(statement => statement.HotelBookingId).IsUnique();
            entity.HasIndex(statement => new { statement.Status, statement.PreparedAt });
            entity.Property(statement => statement.Status).HasMaxLength(30);
            entity.Property(statement => statement.CheckoutAt).HasColumnType("datetime");
            entity.Property(statement => statement.RoomAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.FoodAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.AddonAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.LateFeeAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.OtherAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(statement => statement.PreparedByName).HasMaxLength(100);
            entity.Property(statement => statement.PreparedAt).HasColumnType("datetime");
            entity.Property(statement => statement.PaidAt).HasColumnType("datetime");
            entity.Property(statement => statement.OrderId).HasMaxLength(50);
            entity.HasOne(statement => statement.HotelBooking)
                .WithOne(booking => booking.CheckoutStatement)
                .HasForeignKey<HotelCheckoutStatement>(statement => statement.HotelBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelCheckoutStatements_HotelBookings");
            entity.HasOne(statement => statement.Order)
                .WithMany(order => order.HotelCheckoutStatements)
                .HasForeignKey(statement => statement.OrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HotelCheckoutStatements_Orders");
        });

        modelBuilder.Entity<HotelCheckoutItem>(entity =>
        {
            entity.HasKey(item => item.CheckoutItemId);
            entity.Property(item => item.ChargeType).HasMaxLength(30);
            entity.Property(item => item.Description).HasMaxLength(250);
            entity.Property(item => item.Quantity).HasColumnType("decimal(10,2)");
            entity.Property(item => item.Unit).HasMaxLength(30);
            entity.Property(item => item.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(item => item.Amount).HasColumnType("decimal(18,2)");
            entity.Property(item => item.SourceType).HasMaxLength(30);
            entity.Property(item => item.SourceId).HasMaxLength(50);
            entity.HasOne(item => item.CheckoutStatement)
                .WithMany(statement => statement.Items)
                .HasForeignKey(item => item.CheckoutStatementId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelCheckoutItems_HotelCheckoutStatements");
        });

        modelBuilder.Entity<HotelStaySpaLink>(entity =>
        {
            entity.HasKey(link => new { link.HotelBookingId, link.SpaBookingId });
            entity.HasIndex(link => link.SpaBookingId).IsUnique();
            entity.Property(link => link.LinkedAt).HasColumnType("datetime");
            entity.HasOne(link => link.HotelBooking)
                .WithMany(booking => booking.SpaLinks)
                .HasForeignKey(link => link.HotelBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelStaySpaLinks_HotelBookings");
            entity.HasOne(link => link.SpaBooking)
                .WithOne(booking => booking.HotelStayLink)
                .HasForeignKey<HotelStaySpaLink>(link => link.SpaBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelStaySpaLinks_SpaBookings");
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
