using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Model;

public partial class PetStoreManagementContext : DbContext
{
    public PetStoreManagementContext()
    {
    }

    public PetStoreManagementContext(DbContextOptions<PetStoreManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Blog> Blogs { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<DailyLog> DailyLogs { get; set; }

    public virtual DbSet<EmployeeSchedule> EmployeeSchedules { get; set; }

    public virtual DbSet<HotelBooking> HotelBookings { get; set; }

    public virtual DbSet<InternalConsumption> InternalConsumptions { get; set; }

    public virtual DbSet<InventoryRecord> InventoryRecords { get; set; }

    public virtual DbSet<InventoryRecordDetail> InventoryRecordDetails { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Pet> Pets { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomType> RoomTypes { get; set; }

    public virtual DbSet<SpaService> SpaServices { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VaccinationSchedule> VaccinationSchedules { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        IConfigurationRoot configuration = builder.Build();
        optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>(entity =>
        {
            entity.HasKey(e => e.BlogId).HasName("PK__Blogs__54379E306BF01E30");

            entity.HasIndex(e => e.Slug, "UQ__Blogs__BC7B5FB6E98F4125").IsUnique();

            entity.Property(e => e.CoverImage)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsPublished).HasDefaultValue(false);
            entity.Property(e => e.MetaDescription).HasMaxLength(250);
            entity.Property(e => e.MetaTitle).HasMaxLength(150);
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Author).WithMany(p => p.Blogs)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Blogs__AuthorId__22751F6C");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Bookings__73951AEDED48FD51");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.PriceCharged).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.AssignedStaff).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.AssignedStaffId)
                .HasConstraintName("FK__Bookings__Assign__5EBF139D");

            entity.HasOne(d => d.Customer).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bookings__Custom__5BE2A6F2");

            entity.HasOne(d => d.Pet).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bookings__PetId__5CD6CB2B");

            entity.HasOne(d => d.Service).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Bookings__Servic__5DCAEF64");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Carts__51BCD7B74CC48935");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.Carts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Carts__CustomerI__7C4F7684");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A2ED0BEA0");

            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CartItems__CartI__01142BA1");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CartItems__Produ__02084FDA");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0B86B9EFEC");

            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64D8478C9D5D");

            entity.HasIndex(e => e.Phone, "UQ__Customer__5C7E359E35E82908").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.LoyaltyPoints).HasDefaultValue(0);
            entity.Property(e => e.MembershipTier)
                .HasMaxLength(20)
                .HasDefaultValue("Đồng");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");
        });

        modelBuilder.Entity<DailyLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__DailyLog__5E54864859C669DC");

            entity.Property(e => e.EatingStatus).HasMaxLength(255);
            entity.Property(e => e.LogDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.MediaUrl)
                .HasMaxLength(550)
                .IsUnicode(false);
            entity.Property(e => e.PottyStatus).HasMaxLength(255);

            entity.HasOne(d => d.HotelBooking).WithMany(p => p.DailyLogs)
                .HasForeignKey(d => d.HotelBookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DailyLogs__Hotel__6FE99F9F");

            entity.HasOne(d => d.Staff).WithMany(p => p.DailyLogs)
                .HasForeignKey(d => d.StaffId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DailyLogs__Staff__70DDC3D8");
        });

        modelBuilder.Entity<EmployeeSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("PK__Employee__9C8A5B4930B4E77A");

            entity.Property(e => e.IsAvailable).HasDefaultValue(true);

            entity.HasOne(d => d.User).WithMany(p => p.EmployeeSchedules)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EmployeeS__UserI__571DF1D5");
        });

        modelBuilder.Entity<HotelBooking>(entity =>
        {
            entity.HasKey(e => e.HotelBookingId).HasName("PK__HotelBoo__36538152756808AE");

            entity.Property(e => e.ActualCheckIn).HasColumnType("datetime");
            entity.Property(e => e.ActualCheckOut).HasColumnType("datetime");
            entity.Property(e => e.BaseDailyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.InitialBehaviourNotes).HasMaxLength(500);
            entity.Property(e => e.InitialCoatStatus).HasMaxLength(255);
            entity.Property(e => e.InitialInjuries).HasMaxLength(500);
            entity.Property(e => e.InitialWeight).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TotalCost)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.HotelBookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HotelBook__Custo__6A30C649");

            entity.HasOne(d => d.Pet).WithMany(p => p.HotelBookings)
                .HasForeignKey(d => d.PetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HotelBook__PetId__6B24EA82");

            entity.HasOne(d => d.Room).WithMany(p => p.HotelBookings)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HotelBook__RoomI__6C190EBB");
        });

        modelBuilder.Entity<InternalConsumption>(entity =>
        {
            entity.HasKey(e => e.ConsumptionId).HasName("PK__Internal__E3A1C417B65AAAF0");

            entity.Property(e => e.ConsumedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Booking).WithMany(p => p.InternalConsumptions)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK__InternalC__Booki__76969D2E");

            entity.HasOne(d => d.HotelBooking).WithMany(p => p.InternalConsumptions)
                .HasForeignKey(d => d.HotelBookingId)
                .HasConstraintName("FK__InternalC__Hotel__75A278F5");

            entity.HasOne(d => d.Product).WithMany(p => p.InternalConsumptions)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__InternalC__Produ__778AC167");

            entity.HasOne(d => d.Record).WithMany(p => p.InternalConsumptions)
                .HasForeignKey(d => d.RecordId)
                .HasConstraintName("FK__InternalC__Recor__74AE54BC");
        });

        modelBuilder.Entity<InventoryRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId).HasName("PK__Inventor__FBDF78E9977CE3A9");

            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.RecordType).HasMaxLength(50);
            entity.Property(e => e.TotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.InventoryRecordApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__Inventory__Appro__4BAC3F29");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryRecordCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Creat__4AB81AF0");
        });

        modelBuilder.Entity<InventoryRecordDetail>(entity =>
        {
            entity.HasKey(e => e.DetailId).HasName("PK__Inventor__135C316D7086CA3D");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryRecordDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Produ__4F7CD00D");

            entity.HasOne(d => d.Record).WithMany(p => p.InventoryRecordDetails)
                .HasForeignKey(d => d.RecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Inventory__Recor__4E88ABD4");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCF5CA8B400");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.OrderType).HasMaxLength(20);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.PointsDiscount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.VoucherDiscount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK__Orders__Customer__0E6E26BF");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Orders__UserId__0F624AF8");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D36C619305BE");

            entity.Property(e => e.ComboDescription).HasMaxLength(255);
            entity.Property(e => e.IsCombo).HasDefaultValue(false);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Booking).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK__OrderDeta__Booki__160F4887");

            entity.HasOne(d => d.HotelBooking).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.HotelBookingId)
                .HasConstraintName("FK__OrderDeta__Hotel__17036CC0");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderDeta__Order__14270015");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__OrderDeta__Produ__151B244E");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A38BDB59B40");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Notes).HasMaxLength(255);
            entity.Property(e => e.PaymentDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Success");
            entity.Property(e => e.TransactionNo)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Payments__OrderI__1BC821DD");
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasKey(e => e.PetId).HasName("PK__Pets__48E53862D9D0AEC1");

            entity.Property(e => e.Breed).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PetName).HasMaxLength(100);
            entity.Property(e => e.Species).HasMaxLength(50);
            entity.Property(e => e.Weight).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.Pets)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Pets__CustomerId__34C8D9D1");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CDA3D7A316");

            entity.HasIndex(e => e.Barcode, "UQ__Products__177800D3F5A94CEB").IsUnique();

            entity.Property(e => e.Barcode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinStockThreshold).HasDefaultValue(5);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Products__Catego__440B1D61");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1AFB3BB40B");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160FD1CAD1C").IsUnique();

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__Rooms__32863939C422D6F6");

            entity.Property(e => e.RoomCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RoomType)
                .HasMaxLength(100);
            entity.Property(e => e.DailyRate)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Available");
            entity.Property(e => e.Dimensions)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<RoomType>(entity =>
        {
            entity.HasKey(e => e.RoomTypeId).HasName("PK__RoomType__BCC89631E1C976E3");

            entity.Property(e => e.DailyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.TypeName).HasMaxLength(100);
        });

        modelBuilder.Entity<SpaService>(entity =>
        {
            entity.HasKey(e => e.ServiceId).HasName("PK__SpaServi__C51BB00A884CEF85");

            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DurationMinutes).HasDefaultValue(60);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ServiceName).HasMaxLength(150);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C9A9419CC");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4F7B00AF3").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__2A4B4B5E");
        });

        modelBuilder.Entity<VaccinationSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("PK__Vaccinat__9C8A5B4966907F21");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.VaccineName).HasMaxLength(150);

            entity.HasOne(d => d.Pet).WithMany(p => p.VaccinationSchedules)
                .HasForeignKey(d => d.PetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Vaccinati__PetId__398D8EEE");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("PK__Vouchers__3AEE792155A537F2");

            entity.HasIndex(e => e.VoucherCode, "UQ__Vouchers__7F0ABCA96A203601").IsUnique();

            entity.Property(e => e.DiscountType).HasMaxLength(20);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderValue)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.VoucherCode)
                .HasMaxLength(50)
                .IsUnicode(false);
        });
        // Chặn lỗi vòng lặp khóa ngoại
        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.Booking)
            .WithMany()
            .HasForeignKey(od => od.BookingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<OrderDetail>()
            .HasOne(od => od.HotelBooking)
            .WithMany()
            .HasForeignKey(od => od.HotelBookingId)
            .OnDelete(DeleteBehavior.NoAction);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
