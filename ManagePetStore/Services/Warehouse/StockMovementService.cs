using ManagePetStore.Repositories.Warehouse;
using ManagePetStore.Exceptions;
using ManagePetStore.Models;
using ManagePetStore.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ManagePetStore.Services.Warehouse;

public class StockMovementService : IStockMovementService
{
    private readonly IStockMovementRepository _movementRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryBatchService _batchService;
    private readonly IInventoryBatchRepository _batchRepo;
    private readonly PetStoreManagementContext _context;

    public StockMovementService(
        IStockMovementRepository movementRepo,
        IProductRepository productRepo,
        IInventoryBatchService batchService,
        IInventoryBatchRepository batchRepo,
        PetStoreManagementContext context)
    {
        _movementRepo = movementRepo;
        _productRepo = productRepo;
        _batchService = batchService;
        _batchRepo = batchRepo;
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

    public async Task ApproveMovement(int movementId, int approvedById, Dictionary<int, DateTime>? expiryDates = null, List<BatchAllocation>? allocations = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var movement = await _movementRepo.GetMovementById(movementId);
            if (movement == null) throw new ServiceException("Không tìm thấy phiếu.");
            
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
            else if (movement.Status == "Chờ kiểm hàng" && (movement.Type == "Nhập hàng" || movement.Type == "Nhập kho (Hủy đơn)"))
            {
                movement.Status = "Hoàn thành";
                await _movementRepo.UpdateMovement(movement);
                // Tiếp tục xuống dưới để cộng kho
            }
            else
            {
                throw new ServiceException($"Phiếu đang ở trạng thái '{movement.Status}' nên không thể thao tác duyệt.");
            }

            if (movement.Type == "Nhập hàng" || movement.Type == "Nhập kho (Hủy đơn)")
            {
                if (allocations != null && allocations.Any())
                {
                    // Validate allocations sum
                    foreach (var detail in movement.StockMovementDetails)
                    {
                        int sumAllocated = allocations.Where(a => a.DetailId == detail.DetailId).Sum(a => a.Quantity);
                        if (sumAllocated != detail.Quantity)
                        {
                            throw new ServiceException($"Số lượng phân bổ ({sumAllocated}) không khớp với SL cần nhập ({detail.Quantity}) của sản phẩm {detail.ProductSku}.");
                        }
                    }

                    foreach (var alloc in allocations)
                    {
                        if (alloc.Quantity <= 0) continue;

                        var detail = movement.StockMovementDetails.First(d => d.DetailId == alloc.DetailId);
                        var product = await _productRepo.GetProductBySku(detail.ProductSku);
                        int stockBefore = product?.Stock ?? 0;

                        if (alloc.BatchId > 0)
                        {
                            // Cộng dồn vào lô cũ thông qua BatchService (sẽ tự cộng Stock)
                            var batches = await _batchRepo.GetBatchesByProductSku(detail.ProductSku);
                            var batch = batches.FirstOrDefault(b => b.BatchId == alloc.BatchId);
                            if (batch == null) throw new ServiceException("Không tìm thấy lô hàng đã chọn.");
                            await _batchService.AdjustBatchQuantityAsync(batch.BatchId, alloc.Quantity);
                        }
                        else
                        {
                            // Tạo lô mới
                            DateTime expiryDate = alloc.ExpiryDate ?? DateTime.Now.AddYears(1);
                            var batch = new InventoryBatch
                            {
                                ProductSku = detail.ProductSku,
                                Quantity = alloc.Quantity,
                                CurrentQuantity = alloc.Quantity,
                                ExpiryDate = expiryDate,
                                ReceivedDate = DateTime.Now
                            };
                            await _batchService.CreateBatch(batch);
                        }

                        // Chỉ cập nhật giá vốn (CostPrice) nếu là Nhập hàng mới
                        if (movement.Type == "Nhập hàng" && product != null)
                        {
                            decimal newAvgCost = stockBefore > 0
                                ? (stockBefore * product.CostPrice + alloc.Quantity * detail.CostPrice)
                                  / (stockBefore + alloc.Quantity)
                                : detail.CostPrice;

                            product.CostPrice = Math.Round(newAvgCost, 0);
                            await _productRepo.UpdateProduct(product);
                        }
                    }
                }
                else
                {
                    // Logic cũ cho form duyệt nhập hàng cơ bản
                    foreach (var detail in movement.StockMovementDetails)
                    {
                        var product = await _productRepo.GetProductBySku(detail.ProductSku);
                        int stockBefore = product?.Stock ?? 0;

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
                        await _batchService.CreateBatch(batch);

                        if (movement.Type == "Nhập hàng" && product != null)
                        {
                            decimal newAvgCost = stockBefore > 0
                                ? (stockBefore * product.CostPrice + detail.Quantity * detail.CostPrice)
                                  / (stockBefore + detail.Quantity)
                                : detail.CostPrice;

                            product.CostPrice = Math.Round(newAvgCost, 0);
                            await _productRepo.UpdateProduct(product);
                        }
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

    public Task TriggerAutoReorderCheck(string productSku)
    {
        return Task.CompletedTask;
    }
}
