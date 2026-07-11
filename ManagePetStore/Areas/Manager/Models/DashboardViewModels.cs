using System;
using System.Collections.Generic;

namespace ManagePetStore.Areas.Manager.Models;

public class DashboardSummaryViewModel
{
    public decimal TotalRevenue { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal NetCashFlow { get; set; }
    public int TotalOrders { get; set; }
    public int TotalBlogViews { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal Value2 { get; set; }
}

public class RevenueDetailItem
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CashFlowDetailItem
{
    public string ReferenceId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "In" hoặc "Out"
    public string Source { get; set; } = string.Empty; // "Đơn hàng", "Nhập kho", v.v.
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class TopServiceItem
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Spa" hoặc "Hotel"
    public int UsageCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class TopBlogItem
{
    public int BlogId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
