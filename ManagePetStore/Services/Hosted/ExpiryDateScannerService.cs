/**
 * Project: Pet Store Management System (PSMS)
 * File: ExpiryDateScannerService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Background service quét hạn sử dụng lô hàng tự động mỗi ngày.
 */
using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ManagePetStore.Services.Hosted
{
    public class ExpiryDateScannerService : BackgroundService
    {
        private readonly ILogger<ExpiryDateScannerService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ExpiryDateScannerService(ILogger<ExpiryDateScannerService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExpiryDateScannerService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Bắt đầu quét hạn sử dụng các lô hàng...");
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<PetStoreManagementContext>();
                        
                        var expiringBatches = await context.InventoryBatchs
                            .Include(b => b.ProductSkuNavigation)
                            .Where(b => b.CurrentQuantity > 0 && b.ExpiryDate <= DateTime.Now.AddDays(30))
                            .ToListAsync(stoppingToken);

                        if (expiringBatches.Any())
                        {
                            _logger.LogWarning($"Phát hiện {expiringBatches.Count} lô hàng sắp hoặc đã hết hạn!");
                            foreach(var batch in expiringBatches)
                            {
                                if (batch.ExpiryDate < DateTime.Now)
                                {
                                    _logger.LogError($"- Lô #{batch.BatchId} của {batch.ProductSkuNavigation.Name} ĐÃ HẾT HẠN (HSD: {batch.ExpiryDate:dd/MM/yyyy}).");
                                }
                                else
                                {
                                    _logger.LogWarning($"- Lô #{batch.BatchId} của {batch.ProductSkuNavigation.Name} sắp hết hạn (HSD: {batch.ExpiryDate:dd/MM/yyyy}).");
                                }
                            }
                            
                            // Trong thực tế, ở đây ta có thể lưu vào bảng Notifications để hiển thị trên UI cho Admin/Warehouse
                        }
                        else
                        {
                            _logger.LogInformation("Quét hoàn tất. Không có lô hàng nào sắp hết hạn.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi quét hạn sử dụng.");
                }

                // Chờ 24 giờ rồi quét lại
                // (Trong quá trình test có thể để 1 phút: TimeSpan.FromMinutes(1))
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
