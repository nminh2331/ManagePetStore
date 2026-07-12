/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Triển khai dịch vụ quản lý xuất/nhập kho.
 */
using ManagePetStore.Repositories.Warehouse;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;

namespace ManagePetStore.Services.Warehouse;

public class StockMovementService : IStockMovementService
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryBatchService _batchService;
    private readonly PetStoreManagementContext _context;

    public StockMovementService(
        IStockMovementRepository movementRepo,
        IProductRepository productRepo,
        IInventoryBatchService batchService,
        PetStoreManagementContext context)
    {
        _movementRepo = movementRepo;
        _productRepo = productRepo;
        _batchService = batchService;
        _context = context;
    }

    public async Task<IEnumerable<StockMovement>> GetAllMovements(DateTime? fromDate = null, DateTime? toDate = null, string? search = null)
    {
        return await _movementRepo.GetAllMovements(fromDate, toDate, search);
    }

    public async Task<StockMovement?> GetMovementById(int id)
    {
        return await _movementRepo.GetMovementById(id);
    }

    public async Task CreateImportOrder(int userId, int? supplierId, List<StockMovementDetail> details)
    {
        if (details == null || !details.Any())
            throw new ServiceException("Đơn nhập phải có ít nhất 1 sản phẩm.");

        decimal totalValue = details.Sum(d => d.Quantity * d.CostPrice);

        var movement = new StockMovement
        {
            Type = "Nhập hàng",
            Status = "Chờ quản lý duyệt",
            SupplierId = supplierId,
            CreatedById = userId,
            Date = DateTime.Now,
            TotalValue = totalValue,
            StockMovementDetails = details
        };

        await _movementRepo.AddMovement(movement);
    }

    public async Task CreateInternalExport(int userId, string note, List<StockMovementDetail> details)
    {
        if (details == null || !details.Any())
            throw new ServiceException("Phiếu xuất phải có ít nhất 1 sản phẩm.");

        foreach (var detail in details)
        {
            var product = await _productRepo.GetProductBySku(detail.ProductSku);
            if (product == null) throw new ServiceException($"Sản phẩm {detail.ProductSku} không tồn tại.");
            if (product.Stock < detail.Quantity)
                throw new ServiceException($"Sản phẩm {product.Name} không đủ tồn kho (Còn: {product.Stock}, Yêu cầu: {detail.Quantity}).");
        }

        var movement = new StockMovement
        {
            Type = "Xuất nội bộ",
            Status = "Chờ quản lý duyệt",
            Supplier = note, // Tận dụng trường Supplier để ghi chú mục đích xuất
            CreatedById = userId,
            Date = DateTime.Now,
            TotalValue = 0, // Xuất nội bộ có thể không tính tiền
            StockMovementDetails = details
        };

        await _movementRepo.AddMovement(movement);
    }

    public async Task ApproveMovement(int movementId, int approvedById, Dictionary<int, DateTime>? expiryDates = null)
    {
        // Xử lý Race Condition: Sử dụng Transaction với mức Serializable để lock các thao tác đọc/ghi liên quan đến phiếu này
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var movement = await _movementRepo.GetMovementById(movementId);
            if (movement == null) throw new ServiceException("Không tìm thấy phiếu.");
            
            string originalStatus = movement.Status;

            if (movement.Status == "Chờ quản lý duyệt")
            {
                if (movement.Type == "Nhập hàng")
                {
                    movement.Status = "Chờ kiểm hàng";
                    await _movementRepo.UpdateMovement(movement);
                    await transaction.CommitAsync();
                    return; // Manager duyệt xong thì dừng lại chờ Warehouse kiểm hàng
                }
                else if (movement.Type == "Xuất nội bộ")
                {
                    movement.Status = "Hoàn thành";
                    await _movementRepo.UpdateMovement(movement);
                    // Tiếp tục xuống dưới để trừ kho
                }
            }
            else if (movement.Status == "Chờ kiểm hàng" && movement.Type == "Nhập hàng")
            {
                movement.Status = "Hoàn thành";
                await _movementRepo.UpdateMovement(movement);
                // Tiếp tục xuống dưới để cộng kho
            }
            else
            {
                throw new ServiceException($"Phiếu đang ở trạng thái '{movement.Status}' nên không thể thao tác duyệt.");
            }

        if (movement.Type == "Nhập hàng")
        {
            // Tạo batch, cộng tồn kho và cập nhật giá bình quân gia quyền cho từng chi tiết
            foreach (var detail in movement.StockMovementDetails)
            {
                // Lấy sản phẩm và tồn kho TRƯỚC khi tạo batch để tính giá bình quân chính xác
                var product = await _productRepo.GetProductBySku(detail.ProductSku);

                // Lấy HSD do nhân viên kiểm hàng đã điền, nếu không có thì mặc định 1 năm
                DateTime expiryDate = expiryDates != null && expiryDates.TryGetValue(detail.DetailId, out var d)
                    ? d
                    : DateTime.Now.AddYears(1);

                var batch = new InventoryBatch
                {
                    ProductSku = detail.ProductSku,
                    Quantity = detail.Quantity,
                    CurrentQuantity = detail.Quantity,
                    ExpiryDate = expiryDate,
                    ReceivedDate = DateTime.Now
                };
                await _batchService.CreateBatch(batch); // Stock tăng lên sau bước này

                // Cập nhật giá bình quân gia quyền (Weighted Average Cost)
                // Công thức: (Tồn cũ × Giá cũ + SL mới × Giá mới) / (Tồn cũ + SL mới)
                if (product != null)
                {
                    int stockBefore = product.Stock; // Tồn kho trước khi nhập lô này
                    decimal newAvgCost = stockBefore > 0
                        ? (stockBefore * product.CostPrice + detail.Quantity * detail.CostPrice)
                          / (stockBefore + detail.Quantity)
                        : detail.CostPrice; // Nếu kho trống thì giá mới = giá lô vừa nhập

                    product.CostPrice = Math.Round(newAvgCost, 0); // Làm tròn đến đồng
                    await _productRepo.UpdateProduct(product);
                }
            }
        }
        else if (movement.Type == "Xuất nội bộ")
        {
            // Trừ tồn kho theo FIFO
            foreach (var detail in movement.StockMovementDetails)
            {
                await _batchService.DeductStockFIFO(detail.ProductSku, detail.Quantity);
                // TODO: Tính năng tự động tạo đơn nhập khi sắp hết hàng sẽ được triển khai trong tương lai
            }
        }
        
        await transaction.CommitAsync();
        }
        catch (ServiceException)
        {
            await transaction.RollbackAsync();
            throw; // Giữ nguyên thông báo lỗi logic nghiệp vụ
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            // Đóng gói các lỗi hệ thống (như Deadlock từ SQL Server) thành thông báo thân thiện
            throw new ServiceException("Hệ thống đang xử lý phiếu này hoặc có lỗi xung đột. Vui lòng tải lại trang và thử lại.");
        }
    }

    public async Task CreateSystemMovement(int systemUserId, string type, string status, string? supplier, decimal totalValue, List<StockMovementDetail> details)
    {
        var movement = new StockMovement
        {
            Type = type,
            CreatedById = systemUserId,
            Status = status,
            Supplier = supplier,
            TotalValue = totalValue,
            Date = DateTime.Now,
            StockMovementDetails = details
        };

        await _movementRepo.AddMovement(movement);
    }

    public async Task CancelMovement(int movementId)
    {
        var movement = await _movementRepo.GetMovementById(movementId);
        if (movement == null) throw new ServiceException("Không tìm thấy phiếu.");
        if (movement.Status != "Chờ quản lý duyệt" && movement.Status != "Chờ kiểm hàng") 
            throw new ServiceException("Không thể hủy phiếu đã hoàn thành hoặc đã hủy.");

        movement.Status = "Đã hủy";
        await _movementRepo.UpdateMovement(movement);
    }

    // TODO: Tính năng tự động tạo đơn nhập khi tồn kho xuống dưới MinStock
    // Sẽ được triển khai trong tương lai với cấu hình per-product (bật/tắt, SL đặt, nhà cung cấp)
    public Task TriggerAutoReorderCheck(string productSku)
    {
        return Task.CompletedTask;
    }
}
