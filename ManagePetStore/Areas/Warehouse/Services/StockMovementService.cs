/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Triển khai dịch vụ quản lý xuất/nhập kho.
 */
using ManagePetStore.Areas.Warehouse.Repositories;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;

namespace ManagePetStore.Areas.Warehouse.Services;

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

    public async Task<IEnumerable<StockMovement>> GetAllMovements()
    {
        return await _movementRepo.GetAllMovements();
    }

    public async Task<StockMovement?> GetMovementById(int id)
    {
        return await _movementRepo.GetMovementById(id);
    }

    public async Task CreateImportOrder(int userId, string supplier, List<StockMovementDetail> details)
    {
        if (details == null || !details.Any())
            throw new ServiceException("Đơn nhập phải có ít nhất 1 sản phẩm.");

        decimal totalValue = details.Sum(d => d.Quantity * d.CostPrice);

        var movement = new StockMovement
        {
            Type = "Nhập hàng",
            Status = "Chờ duyệt",
            Supplier = string.IsNullOrEmpty(supplier) ? "Không xác định" : supplier,
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
            Status = "Chờ duyệt",
            Supplier = note, // Tận dụng trường Supplier để ghi chú mục đích xuất
            CreatedById = userId,
            Date = DateTime.Now,
            TotalValue = 0, // Xuất nội bộ có thể không tính tiền
            StockMovementDetails = details
        };

        await _movementRepo.AddMovement(movement);
    }

    public async Task ApproveMovement(int movementId, int approvedById)
    {
        var movement = await _movementRepo.GetMovementById(movementId);
        if (movement == null) throw new ServiceException("Không tìm thấy phiếu.");
        if (movement.Status != "Chờ duyệt") throw new ServiceException("Chỉ có thể duyệt phiếu ở trạng thái Chờ duyệt.");

        movement.Status = "Hoàn thành";
        await _movementRepo.UpdateMovement(movement);

        if (movement.Type == "Nhập hàng")
        {
            // Tạo batch và cộng tồn kho cho từng chi tiết
            foreach (var detail in movement.StockMovementDetails)
            {
                var batch = new InventoryBatch
                {
                    ProductSku = detail.ProductSku,
                    Quantity = detail.Quantity,
                    CurrentQuantity = detail.Quantity,
                    // Mặc định HSD 1 năm nếu không có thông tin (thủ kho có thể sửa sau trong quản lý lô)
                    ExpiryDate = DateTime.Now.AddYears(1), 
                    ReceivedDate = DateTime.Now
                };
                await _batchService.CreateBatch(batch);
            }
        }
        else if (movement.Type == "Xuất nội bộ")
        {
            // Trừ tồn kho theo FIFO
            foreach (var detail in movement.StockMovementDetails)
            {
                await _batchService.DeductStockFIFO(detail.ProductSku, detail.Quantity);
                
                // Kiểm tra auto-reorder sau khi trừ
                await TriggerAutoReorderCheck(detail.ProductSku);
            }
        }
    }

    public async Task CancelMovement(int movementId)
    {
        var movement = await _movementRepo.GetMovementById(movementId);
        if (movement == null) throw new ServiceException("Không tìm thấy phiếu.");
        if (movement.Status != "Chờ duyệt") throw new ServiceException("Chỉ có thể hủy phiếu đang Chờ duyệt.");

        movement.Status = "Đã hủy";
        await _movementRepo.UpdateMovement(movement);
    }

    public async Task TriggerAutoReorderCheck(string productSku)
    {
        var product = await _productRepo.GetProductBySku(productSku);
        if (product == null) return;

        // Nếu dưới ngưỡng MinStock
        if (product.Stock < product.MinStock)
        {
            // Kiểm tra xem đã có đơn nhập nào đang chờ duyệt cho sản phẩm này chưa
            var pendingOrder = await _movementRepo.GetPendingImportByProduct(productSku);
            if (pendingOrder == null)
            {
                // Chưa có -> Tạo đơn tự động
                int quantityToOrder = product.MinStock > 0 ? product.MinStock * 2 : 10;
                
                var details = new List<StockMovementDetail>
                {
                    new StockMovementDetail
                    {
                        ProductSku = productSku,
                        Quantity = quantityToOrder,
                        CostPrice = product.Price * 0.7m // Tạm ước tính giá nhập = 70% giá bán
                    }
                };

                // Lấy ID của tài khoản Admin đầu tiên để làm CreatedById
                var adminUser = _context.Users.FirstOrDefault(u => u.RoleId == 1);
                int sysUserId = adminUser?.UserId ?? 1;

                await CreateImportOrder(sysUserId, "Auto Reorder - Tự động tạo", details);
            }
        }
    }
}
