/**
 * Project: Pet Store Management System (PSMS)
 * File: StockMovementService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Last Update: July 17, 2026
 * Description: Triá»ƒn khai dá»‹ch vá»¥ quáº£n lÃ½ xuáº¥t/nháº­p kho.
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
            throw new ServiceException("ÄÆ¡n nháº­p pháº£i cÃ³ Ã­t nháº¥t 1 sáº£n pháº©m.");

        decimal totalValue = details.Sum(d => d.Quantity * d.CostPrice);

        var movement = new StockMovement
        {
            Type = "Nháº­p hÃ ng",
            Status = "Chá» quáº£n lÃ½ duyá»‡t",
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
            throw new ServiceException("Phiáº¿u xuáº¥t pháº£i cÃ³ Ã­t nháº¥t 1 sáº£n pháº©m.");

        foreach (var detail in details)
        {
            var product = await _productRepo.GetProductBySku(detail.ProductSku);
            if (product == null) throw new ServiceException($"Sáº£n pháº©m {detail.ProductSku} khÃ´ng tá»“n táº¡i.");
            if (product.Stock < detail.Quantity)
                throw new ServiceException($"Sáº£n pháº©m {product.Name} khÃ´ng Ä‘á»§ tá»“n kho (CÃ²n: {product.Stock}, YÃªu cáº§u: {detail.Quantity}).");
        }

        var movement = new StockMovement
        {
            Type = "Xuáº¥t ná»™i bá»™",
            Status = "Chá» quáº£n lÃ½ duyá»‡t",
            Supplier = note, // Táº­n dá»¥ng trÆ°á»ng Supplier Ä‘á»ƒ ghi chÃº má»¥c Ä‘Ã­ch xuáº¥t
            CreatedById = userId,
            Date = DateTime.Now,
            TotalValue = 0, // Xuáº¥t ná»™i bá»™ cÃ³ thá»ƒ khÃ´ng tÃ­nh tiá»n
            StockMovementDetails = details
        };

        await _movementRepo.AddMovement(movement);
    }

    public async Task ApproveMovement(int movementId, int approvedById, Dictionary<int, DateTime>? expiryDates = null)
    {
        // Xá»­ lÃ½ Race Condition: Sá»­ dá»¥ng Transaction vá»›i má»©c Serializable Ä‘á»ƒ lock cÃ¡c thao tÃ¡c Ä‘á»c/ghi liÃªn quan Ä‘áº¿n phiáº¿u nÃ y
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var movement = await _movementRepo.GetMovementById(movementId);
            if (movement == null) throw new ServiceException("KhÃ´ng tÃ¬m tháº¥y phiáº¿u.");
            
            string originalStatus = movement.Status;

            if (movement.Status == "Chá» quáº£n lÃ½ duyá»‡t")
            {
                if (movement.Type == "Nháº­p hÃ ng")
                {
                    movement.Status = "Chá» kiá»ƒm hÃ ng";
                    await _movementRepo.UpdateMovement(movement);
                    await transaction.CommitAsync();
                    return; // Manager duyá»‡t xong thÃ¬ dá»«ng láº¡i chá» Warehouse kiá»ƒm hÃ ng
                }
                else if (movement.Type == "Xuáº¥t ná»™i bá»™")
                {
                    movement.Status = "HoÃ n thÃ nh";
                    await _movementRepo.UpdateMovement(movement);
                    // Tiáº¿p tá»¥c xuá»‘ng dÆ°á»›i Ä‘á»ƒ trá»« kho
                }
            }
            else if (movement.Status == "Chá» kiá»ƒm hÃ ng" && movement.Type == "Nháº­p hÃ ng")
            {
                movement.Status = "HoÃ n thÃ nh";
                await _movementRepo.UpdateMovement(movement);
                // Tiáº¿p tá»¥c xuá»‘ng dÆ°á»›i Ä‘á»ƒ cá»™ng kho
            }
            else
            {
                throw new ServiceException($"Phiáº¿u Ä‘ang á»Ÿ tráº¡ng thÃ¡i '{movement.Status}' nÃªn khÃ´ng thá»ƒ thao tÃ¡c duyá»‡t.");
            }

        if (movement.Type == "Nháº­p hÃ ng")
        {
            // Táº¡o batch, cá»™ng tá»“n kho vÃ  cáº­p nháº­t giÃ¡ bÃ¬nh quÃ¢n gia quyá»n cho tá»«ng chi tiáº¿t
            foreach (var detail in movement.StockMovementDetails)
            {
                // Láº¥y sáº£n pháº©m vÃ  tá»“n kho TRÆ¯á»šC khi táº¡o batch Ä‘á»ƒ tÃ­nh giÃ¡ bÃ¬nh quÃ¢n chÃ­nh xÃ¡c
                var product = await _productRepo.GetProductBySku(detail.ProductSku);
                int stockBefore = product?.Stock ?? 0; // LÆ°u tá»“n kho cÅ© trÆ°á»›c khi CreateBatch thay Ä‘á»•i nÃ³

                // Láº¥y HSD do nhÃ¢n viÃªn kiá»ƒm hÃ ng Ä‘Ã£ Ä‘iá»n, náº¿u khÃ´ng cÃ³ thÃ¬ máº·c Ä‘á»‹nh 1 nÄƒm
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
                await _batchService.CreateBatch(batch); // Stock tÄƒng lÃªn sau bÆ°á»›c nÃ y

                // Cáº­p nháº­t giÃ¡ bÃ¬nh quÃ¢n gia quyá»n (Weighted Average Cost)
                // CÃ´ng thá»©c: (Tá»“n cÅ© Ã— GiÃ¡ cÅ© + SL má»›i Ã— GiÃ¡ má»›i) / (Tá»“n cÅ© + SL má»›i)
                if (product != null)
                {
                    decimal newAvgCost = stockBefore > 0
                        ? (stockBefore * product.CostPrice + detail.Quantity * detail.CostPrice)
                          / (stockBefore + detail.Quantity)
                        : detail.CostPrice; // Náº¿u kho trá»‘ng thÃ¬ giÃ¡ má»›i = giÃ¡ lÃ´ vá»«a nháº­p

                    product.CostPrice = Math.Round(newAvgCost, 0); // LÃ m trÃ²n Ä‘áº¿n Ä‘á»“ng
                    await _productRepo.UpdateProduct(product);
                }
            }
        }
        else if (movement.Type == "Xuáº¥t ná»™i bá»™")
        {
            // Trá»« tá»“n kho theo FIFO
            foreach (var detail in movement.StockMovementDetails)
            {
                await _batchService.DeductStockFIFO(detail.ProductSku, detail.Quantity);
                // TODO: TÃ­nh nÄƒng tá»± Ä‘á»™ng táº¡o Ä‘Æ¡n nháº­p khi sáº¯p háº¿t hÃ ng sáº½ Ä‘Æ°á»£c triá»ƒn khai trong tÆ°Æ¡ng lai
            }
        }
        
        await transaction.CommitAsync();
        }
        catch (ServiceException)
        {
            await transaction.RollbackAsync();
            throw; // Giá»¯ nguyÃªn thÃ´ng bÃ¡o lá»—i logic nghiá»‡p vá»¥
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            // ÄÃ³ng gÃ³i cÃ¡c lá»—i há»‡ thá»‘ng (nhÆ° Deadlock tá»« SQL Server) thÃ nh thÃ´ng bÃ¡o thÃ¢n thiá»‡n
            throw new ServiceException("Há»‡ thá»‘ng Ä‘ang xá»­ lÃ½ phiáº¿u nÃ y hoáº·c cÃ³ lá»—i xung Ä‘á»™t. Vui lÃ²ng táº£i láº¡i trang vÃ  thá»­ láº¡i.");
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
        if (movement == null) throw new ServiceException("KhÃ´ng tÃ¬m tháº¥y phiáº¿u.");
        if (movement.Status != "Chá» quáº£n lÃ½ duyá»‡t" && movement.Status != "Chá» kiá»ƒm hÃ ng") 
            throw new ServiceException("KhÃ´ng thá»ƒ há»§y phiáº¿u Ä‘Ã£ hoÃ n thÃ nh hoáº·c Ä‘Ã£ há»§y.");

        movement.Status = "ÄÃ£ há»§y";
        await _movementRepo.UpdateMovement(movement);
    }

    // TODO: TÃ­nh nÄƒng tá»± Ä‘á»™ng táº¡o Ä‘Æ¡n nháº­p khi tá»“n kho xuá»‘ng dÆ°á»›i MinStock
    // Sáº½ Ä‘Æ°á»£c triá»ƒn khai trong tÆ°Æ¡ng lai vá»›i cáº¥u hÃ¬nh per-product (báº­t/táº¯t, SL Ä‘áº·t, nhÃ  cung cáº¥p)
    public Task TriggerAutoReorderCheck(string productSku)
    {
        return Task.CompletedTask;
    }
}
