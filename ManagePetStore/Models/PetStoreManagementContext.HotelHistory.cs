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
    public virtual DbSet<HotelCheckInAssessment> HotelCheckInAssessments => Set<HotelCheckInAssessment>();
    public virtual DbSet<HotelCageChangeRequest> HotelCageChangeRequests => Set<HotelCageChangeRequest>();
    public virtual DbSet<HotelCageStaySegment> HotelCageStaySegments => Set<HotelCageStaySegment>();

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
            entity.Property(plan => plan.BasePricePerDaySnapshot).HasColumnType("decimal(18,2)");
            entity.Property(plan => plan.PetWeightSnapshot).HasColumnType("decimal(5,2)");
            entity.Property(plan => plan.PortionMultiplierSnapshot).HasColumnType("decimal(5,2)");
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

        modelBuilder.Entity<HotelCheckInAssessment>(entity =>
        {
            entity.HasKey(assessment => assessment.AssessmentId);
            entity.HasIndex(assessment => assessment.HotelBookingId).IsUnique();
            entity.HasIndex(assessment => assessment.MedicalRecordId);
            entity.Property(assessment => assessment.Decision).HasMaxLength(30);
            entity.Property(assessment => assessment.Note).HasMaxLength(1000);
            entity.Property(assessment => assessment.AssessedByName).HasMaxLength(100);
            entity.Property(assessment => assessment.AssessedAt).HasColumnType("datetime");

            entity.HasOne(assessment => assessment.HotelBooking)
                .WithOne(booking => booking.CheckInAssessment)
                .HasForeignKey<HotelCheckInAssessment>(assessment => assessment.HotelBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelCheckInAssessments_HotelBookings");

            entity.HasOne(assessment => assessment.MedicalRecord)
                .WithMany()
                .HasForeignKey(assessment => assessment.MedicalRecordId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_HotelCheckInAssessments_MedicalRecords");

            entity.HasOne(assessment => assessment.AssessedByUser)
                .WithMany()
                .HasForeignKey(assessment => assessment.AssessedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HotelCheckInAssessments_Users");
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

        modelBuilder.Entity<HotelCageChangeRequest>(entity =>
        {
            entity.HasKey(request => request.ChangeRequestId);
            entity.HasIndex(request => new { request.HotelBookingId, request.Status }, "IX_HotelCageChangeRequests_Booking_Status");
            entity.HasIndex(request => request.RequestedAt, "IX_HotelCageChangeRequests_RequestedAt");
            entity.HasIndex(request => request.HotelBookingId, "UX_HotelCageChangeRequests_OnePendingPerBooking")
                .IsUnique()
                .HasFilter("([Status]=N'Pending')");
            entity.Property(request => request.SourceCageId).HasMaxLength(20);
            entity.Property(request => request.TargetCageId).HasMaxLength(20);
            entity.Property(request => request.Reason).HasMaxLength(500);
            entity.Property(request => request.Status).HasMaxLength(20);
            entity.Property(request => request.SourceDailyPriceSnapshot).HasColumnType("decimal(18,2)");
            entity.Property(request => request.TargetDailyPriceSnapshot).HasColumnType("decimal(18,2)");
            entity.Property(request => request.PriceDifferenceSnapshot).HasColumnType("decimal(18,2)");
            entity.Property(request => request.RequestedAt).HasColumnType("datetime");
            entity.Property(request => request.ProcessedAt).HasColumnType("datetime");
            entity.Property(request => request.ProcessedByName).HasMaxLength(100);
            entity.Property(request => request.DecisionNote).HasMaxLength(1000);
            entity.Property(request => request.AppliedAt).HasColumnType("datetime");

            entity.HasOne(request => request.HotelBooking)
                .WithMany(booking => booking.CageChangeRequests)
                .HasForeignKey(request => request.HotelBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelCageChangeRequests_HotelBookings");
            entity.HasOne(request => request.Customer)
                .WithMany()
                .HasForeignKey(request => request.CustomerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_HotelCageChangeRequests_Customers");
            entity.HasOne(request => request.SourceCage)
                .WithMany(cage => cage.SourceCageChangeRequests)
                .HasForeignKey(request => request.SourceCageId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_HotelCageChangeRequests_SourceCage");
            entity.HasOne(request => request.TargetCage)
                .WithMany(cage => cage.TargetCageChangeRequests)
                .HasForeignKey(request => request.TargetCageId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_HotelCageChangeRequests_TargetCage");
            entity.HasOne(request => request.ProcessedByUser)
                .WithMany()
                .HasForeignKey(request => request.ProcessedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_HotelCageChangeRequests_ProcessedBy");
        });

        modelBuilder.Entity<HotelCageStaySegment>(entity =>
        {
            entity.HasKey(segment => segment.StaySegmentId);
            entity.HasIndex(segment => new { segment.HotelBookingId, segment.StartedAt }, "IX_HotelCageStaySegments_Booking_StartedAt");
            entity.HasIndex(segment => segment.HotelBookingId, "UX_HotelCageStaySegments_OneOpenPerBooking")
                .IsUnique()
                .HasFilter("([EndedAt] IS NULL)");
            entity.Property(segment => segment.CageId).HasMaxLength(20);
            entity.Property(segment => segment.DailyPriceSnapshot).HasColumnType("decimal(18,2)");
            entity.Property(segment => segment.StartedAt).HasColumnType("datetime");
            entity.Property(segment => segment.EndedAt).HasColumnType("datetime");
            entity.Property(segment => segment.StartReason).HasMaxLength(30);
            entity.Property(segment => segment.EndReason).HasMaxLength(100);
            entity.Property(segment => segment.CreatedAt).HasColumnType("datetime");

            entity.HasOne(segment => segment.HotelBooking)
                .WithMany(booking => booking.CageStaySegments)
                .HasForeignKey(segment => segment.HotelBookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_HotelCageStaySegments_HotelBookings");
            entity.HasOne(segment => segment.Cage)
                .WithMany(cage => cage.HotelCageStaySegments)
                .HasForeignKey(segment => segment.CageId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_HotelCageStaySegments_Cages");
            entity.HasOne(segment => segment.RoomType)
                .WithMany(roomType => roomType.HotelCageStaySegments)
                .HasForeignKey(segment => segment.RoomTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_HotelCageStaySegments_RoomTypes");
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
