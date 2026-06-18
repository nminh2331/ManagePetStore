using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Models;

public partial class PetStoreManagementContext : DbContext
{
    public PetStoreManagementContext()
    {
    }

    public PetStoreManagementContext(DbContextOptions<PetStoreManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<Blog> Blogs { get; set; }

    public virtual DbSet<BookingAddon> BookingAddons { get; set; }

    public virtual DbSet<Cage> Cages { get; set; }

    public virtual DbSet<Consumable> Consumables { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<FoodDiaryLog> FoodDiaryLogs { get; set; }

    public virtual DbSet<HotelBooking> HotelBookings { get; set; }

    public virtual DbSet<InventoryBatch> InventoryBatches { get; set; }
    public virtual DbSet<InventoryBatch> InventoryBatchs { get => InventoryBatches; set => InventoryBatches = value; }

    public virtual DbSet<MedicalRecord> MedicalRecords { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderTracking> OrderTrackings { get; set; }

    public virtual DbSet<Pet> Pets { get; set; }

    public virtual DbSet<PetBioTimeline> PetBioTimelines { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductCategory> ProductCategories { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RoomType> RoomTypes { get; set; }

    public virtual DbSet<SpaBooking> SpaBookings { get; set; }

    public virtual DbSet<SpaQueue> SpaQueues { get; set; }

    public virtual DbSet<SpaService> SpaServices { get; set; }

    public virtual DbSet<StaffProfile> StaffProfiles { get; set; }

    public virtual DbSet<StaffShift> StaffShifts { get; set; }

    public virtual DbSet<StaffTask> StaffTasks { get; set; }

    public virtual DbSet<StockMovement> StockMovements { get; set; }

    public virtual DbSet<StockMovementDetail> StockMovementDetails { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<SupplierCategory> SupplierCategories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

    }
//#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
      //  => optionsBuilder.UseSqlServer("Server=DESKTOP-0NF6T35 ;Database= PetStoreManagement;Trusted_Connection=True; TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Banner>(entity =>
        {
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TargetUrl)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(150);
        });

        modelBuilder.Entity<Blog>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UQ_Blogs_Slug").IsUnique();

            entity.Property(e => e.CoverImage)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Author).WithMany(p => p.Blogs)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Blogs_Users");
        });

        modelBuilder.Entity<BookingAddon>(entity =>
        {
            entity.HasKey(e => e.AddonId);

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.HotelBooking).WithMany(p => p.BookingAddons)
                .HasForeignKey(d => d.HotelBookingId)
                .HasConstraintName("FK_BookingAddons_HotelBookings");
        });

        modelBuilder.Entity<Cage>(entity =>
        {
            entity.Property(e => e.CageId).HasMaxLength(20);
            entity.Property(e => e.FeedSchedule).HasMaxLength(100);
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Portion).HasDefaultValue(60);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Tr?ng");

            entity.HasOne(d => d.RoomType).WithMany(p => p.Cages)
                .HasForeignKey(d => d.RoomTypeId)
                .HasConstraintName("FK_Cages_RoomTypes");
        });

        modelBuilder.Entity<Consumable>(entity =>
        {
            entity.Property(e => e.ConsumableId).HasMaxLength(50);
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Unit).HasMaxLength(30);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => e.Phone, "IX_Customers_Phone");

            entity.HasIndex(e => e.Phone, "UQ_Customers_Phone").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ_Customers_UserId_NonNull")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.MembershipTier)
                .HasMaxLength(50)
                .HasDefaultValue("Bronze");
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithOne(p => p.Customer)
                .HasForeignKey<Customer>(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Customers_Users");
        });

        modelBuilder.Entity<FoodDiaryLog>(entity =>
        {
            entity.HasKey(e => e.LogId);

            entity.Property(e => e.LogId).HasMaxLength(50);
            entity.Property(e => e.Amount).HasMaxLength(50);
            entity.Property(e => e.CageId).HasMaxLength(20);
            entity.Property(e => e.FoodType).HasMaxLength(100);
            entity.Property(e => e.PetName).HasMaxLength(50);
            entity.Property(e => e.PhotoUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.StaffName).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Time).HasMaxLength(30);

            entity.HasOne(d => d.Cage).WithMany(p => p.FoodDiaryLogs)
                .HasForeignKey(d => d.CageId)
                .HasConstraintName("FK_FoodDiaryLogs_Cages");
        });

        modelBuilder.Entity<HotelBooking>(entity =>
        {
            entity.HasIndex(e => e.CageId, "IX_HotelBookings_CageId");

            entity.Property(e => e.BaseDailyPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CageId).HasMaxLength(20);
            entity.Property(e => e.CheckInDate).HasColumnType("datetime");
            entity.Property(e => e.CheckOutDate).HasColumnType("datetime");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cage).WithMany(p => p.HotelBookings)
                .HasForeignKey(d => d.CageId)
                .HasConstraintName("FK_HotelBookings_Cages");

            entity.HasOne(d => d.Customer).WithMany(p => p.HotelBookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HotelBookings_Customers");

            entity.HasOne(d => d.Pet).WithMany(p => p.HotelBookings)
                .HasForeignKey(d => d.PetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HotelBookings_Pets");
        });

        modelBuilder.Entity<InventoryBatch>(entity =>
        {
            entity.HasKey(e => e.BatchId).HasName("PK__Inventor__5D55CE58B72515AF");

            entity.ToTable("InventoryBatchs");

            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.Property(e => e.ProductSku).HasMaxLength(50);
            entity.Property(e => e.ReceivedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ProductSkuNavigation).WithMany(p => p.InventoryBatches)
                .HasForeignKey(d => d.ProductSku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryBatch_Product");
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK__MedicalR__FBDF78E9E466D61C");

            entity.Property(e => e.AbnormalSymptoms).HasMaxLength(500);
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DigestiveSigns).HasMaxLength(500);
            entity.Property(e => e.FurSkinCheck).HasMaxLength(100);
            entity.Property(e => e.HealthStatus).HasMaxLength(100);
            entity.Property(e => e.IncisorCheck).HasMaxLength(100);
            entity.Property(e => e.ParasitePrevention).HasMaxLength(500);
            entity.Property(e => e.PhysicalCheck).HasMaxLength(100);
            entity.Property(e => e.RearingConditions).HasMaxLength(100);
            entity.Property(e => e.ShellStatus).HasMaxLength(100);
            entity.Property(e => e.VaccinationStatus).HasMaxLength(100);
            entity.Property(e => e.Weight).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Pet).WithMany(p => p.MedicalRecords)
                .HasForeignKey(d => d.PetId)
                .HasConstraintName("FK_MedicalRecords_Pets");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.Date, "IX_Orders_Date");

            entity.Property(e => e.OrderId).HasMaxLength(50);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CanceledAt).HasColumnType("datetime");
            entity.Property(e => e.CanceledBy).HasMaxLength(255);
            entity.Property(e => e.Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderStatus).HasDefaultValue(0);
            entity.Property(e => e.PaymentMethod).HasMaxLength(30);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Đã thanh toán");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Customers");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(e => e.OrderId).HasMaxLength(50);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductSku).HasMaxLength(50);
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderItems_Orders");

            entity.HasOne(d => d.ProductSkuNavigation).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductSku)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_OrderItems_Products");

            entity.HasOne(d => d.RoomType).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.RoomTypeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_OrderItems_RoomTypes");

            entity.HasOne(d => d.SpaService).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.SpaServiceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_OrderItems_SpaServices");
        });

        modelBuilder.Entity<OrderTracking>(entity =>
        {
            entity.HasKey(e => e.TrackingId);

            entity.ToTable("OrderTracking");

            entity.Property(e => e.ChangedBy).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.OrderId).HasMaxLength(50);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderTrackings)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderTracking_Orders");
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasIndex(e => e.CustomerId, "IX_Pets_CustomerId");

            entity.Property(e => e.Age).HasMaxLength(30);
            entity.Property(e => e.Breed).HasMaxLength(50);
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Species).HasMaxLength(30);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Weight).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Pets)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Pets_Customers");
        });

        modelBuilder.Entity<PetBioTimeline>(entity =>
        {
            entity.HasKey(e => e.TimelineId);

            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Type).HasMaxLength(30);

            entity.HasOne(d => d.Pet).WithMany(p => p.PetBioTimelines)
                .HasForeignKey(d => d.PetId)
                .HasConstraintName("FK_PetBioTimelines_Pets");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Sku);

            entity.Property(e => e.Sku).HasMaxLength(50);
            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ShelfLocation).HasMaxLength(50);
            entity.Property(e => e.Unit).HasMaxLength(30);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_Products_ProductCategories");
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId);

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.RoleName, "UQ_Roles_RoleName").IsUnique();

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.Property(e => e.Capacity).HasDefaultValue(1);
            entity.Property(e => e.DailyPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.HasAc).HasColumnName("HasAC");
            entity.Property(e => e.HourlyPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Size).HasMaxLength(50);
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.Type).HasMaxLength(100);
        });

        modelBuilder.Entity<SpaBooking>(entity =>
        {
            entity.HasKey(e => e.BookingId);

            entity.HasIndex(e => e.DateTime, "IX_SpaBookings_DateTime");

            entity.Property(e => e.DateTime).HasColumnType("datetime");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SpaStatus).HasMaxLength(30);
            entity.Property(e => e.Status).HasMaxLength(30);

            entity.HasOne(d => d.Customer).WithMany(p => p.SpaBookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SpaBookings_Customers");

            entity.HasOne(d => d.Groomer).WithMany(p => p.SpaBookings)
                .HasForeignKey(d => d.GroomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SpaBookings_Users");

            entity.HasOne(d => d.Pet).WithMany(p => p.SpaBookings)
                .HasForeignKey(d => d.PetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SpaBookings_Pets");

            entity.HasOne(d => d.Service).WithMany(p => p.SpaBookings)
                .HasForeignKey(d => d.ServiceId)
                .HasConstraintName("FK_SpaBookings_SpaServices");
        });

        modelBuilder.Entity<SpaQueue>(entity =>
        {
            entity.HasKey(e => e.QueueId);

            entity.Property(e => e.ArrivalTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.OwnerName).HasMaxLength(100);
            entity.Property(e => e.PetName).HasMaxLength(50);
            entity.Property(e => e.QueueNumber).HasMaxLength(20);
            entity.Property(e => e.ServiceDescription).HasMaxLength(200);
        });

        modelBuilder.Entity<SpaService>(entity =>
        {
            entity.HasKey(e => e.ServiceId);

            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.DurationMinutes).HasDefaultValue(30);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<StaffProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.PerformanceScore).HasDefaultValue(100);
            entity.Property(e => e.RatingsAverage)
                .HasDefaultValue(5.00m)
                .HasColumnType("decimal(3, 2)");

            entity.HasOne(d => d.User).WithOne(p => p.StaffProfile)
                .HasForeignKey<StaffProfile>(d => d.UserId)
                .HasConstraintName("FK_StaffProfiles_Users");
        });

        modelBuilder.Entity<StaffShift>(entity =>
        {
            entity.HasKey(e => e.ShiftId);

            entity.HasIndex(e => new { e.WeekOffset, e.StaffId }, "IX_StaffShifts_WeekOffset_StaffId");

            entity.Property(e => e.DayName).HasMaxLength(30);
            entity.Property(e => e.ShiftType).HasMaxLength(30);

            entity.HasOne(d => d.Staff).WithMany(p => p.StaffShifts)
                .HasForeignKey(d => d.StaffId)
                .HasConstraintName("FK_StaffShifts_Users");
        });

        modelBuilder.Entity<StaffTask>(entity =>
        {
            entity.HasKey(e => e.TaskId);

            entity.Property(e => e.TaskId).HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.Priority).HasMaxLength(30);
            entity.Property(e => e.ScheduledTime).HasMaxLength(50);
            entity.Property(e => e.ServiceDescription).HasMaxLength(250);
            entity.Property(e => e.Status).HasMaxLength(30);

            entity.HasOne(d => d.AssignedStaff).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.AssignedStaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffTasks_Users");

            entity.HasOne(d => d.Customer).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StaffTasks_Customers");

            entity.HasOne(d => d.Pet).WithMany(p => p.StaffTasks)
                .HasForeignKey(d => d.PetId)
                .HasConstraintName("FK_StaffTasks_Pets");
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasKey(e => e.MovementId);

            entity.Property(e => e.Date)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Supplier).HasMaxLength(150);
            entity.Property(e => e.TotalValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Type).HasMaxLength(30);

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockMovements_Users");

            entity.HasOne(d => d.SupplierNavigation).WithMany(p => p.StockMovements)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_StockMovement_Supplier");
        });

        modelBuilder.Entity<StockMovementDetail>(entity =>
        {
            entity.HasKey(e => e.DetailId);

            entity.Property(e => e.BatchNumber).HasMaxLength(50);
            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductSku).HasMaxLength(50);

            entity.HasOne(d => d.ProductSkuNavigation).WithMany(p => p.StockMovementDetails)
                .HasForeignKey(d => d.ProductSku)
                .HasConstraintName("FK_StockMovementDetails_Products");

            entity.HasOne(d => d.StockMovement).WithMany(p => p.StockMovementDetails)
                .HasForeignKey(d => d.StockMovementId)
                .HasConstraintName("FK_StockMovementDetails_StockMovements");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("PK__Supplier__4BE666B4E6FC80CF");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(50);
        });

        modelBuilder.Entity<SupplierCategory>(entity =>
        {
            entity.ToTable("SupplierCategories");
            entity.HasKey(e => new { e.SupplierId, e.CategoryId });

            entity.HasOne(d => d.Supplier)
                .WithMany(p => p.SupplierCategories)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_SupplierCategory_Supplier");

            entity.HasOne(d => d.Category)
                .WithMany()
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_SupplierCategory_Category");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.AvatarPath).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Password)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Code);

            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.Property(e => e.MinOrder).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status).HasDefaultValue(true);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.Value).HasColumnType("decimal(18, 2)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
