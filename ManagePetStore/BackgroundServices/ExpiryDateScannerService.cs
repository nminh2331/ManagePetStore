/**
 * Project: Pet Store Management System (PSMS)
 * File: ExpiryDateScannerService.cs
 * Author: Tran Duong
 * Date: June 11, 2026
 * Description: Background service qu�t h?n s? d?ng l� h�ng t? d?ng m?i ng�y.
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

namespace ManagePetStore.BackgroundServices
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
                    _logger.LogInformation("B?t d?u qu�t h?n s? d?ng c�c l� h�ng...");
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<PetStoreManagementContext>();
                        
                        var expiringBatches = await context.InventoryBatchs
                            .Include(b => b.ProductSkuNavigation)
                            .Where(b => b.CurrentQuantity > 0 && b.ExpiryDate <= DateTime.Now.AddDays(30))
                            .ToListAsync(stoppingToken);

                        if (expiringBatches.Any())
                        {
                            _logger.LogWarning($"Ph�t hi?n {expiringBatches.Count} l� h�ng s?p ho?c d� h?t h?n!");
                            foreach(var batch in expiringBatches)
                            {
                                if (batch.ExpiryDate < DateTime.Now)
                                {
                                    _logger.LogError($"- L� #{batch.BatchId} c?a {batch.ProductSkuNavigation.Name} �� H?T H?N (HSD: {batch.ExpiryDate:dd/MM/yyyy}).");
                                }
                                else
                                {
                                    _logger.LogWarning($"- L� #{batch.BatchId} c?a {batch.ProductSkuNavigation.Name} s?p h?t h?n (HSD: {batch.ExpiryDate:dd/MM/yyyy}).");
                                }
                            }
                            
                            // Trong th?c t?, ? d�y ta c� th? luu v�o b?ng Notifications d? hi?n th? tr�n UI cho Admin/Warehouse
                        }
                        else
                        {
                            _logger.LogInformation("Qu�t ho�n t?t. Kh�ng c� l� h�ng n�o s?p h?t h?n.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "L?i khi qu�t h?n s? d?ng.");
                }

                // Ch? 24 gi? r?i qu�t l?i
                // (Trong qu� tr�nh test c� th? d? 1 ph�t: TimeSpan.FromMinutes(1))
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
