using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace KaraokeClub.Models
{
    // ============================================================
    //  DTO для vw_RevenueReport
    // ============================================================
    [Keyless]
    public class RevenueReportRow
    {
        [Column("BillId")] public int BillId { get; set; }
        [Column("OrderId")] public int OrderId { get; set; }
        [Column("BillDate")] public DateTime BillDate { get; set; }
        [Column("TotalAmount")] public decimal? TotalAmount { get; set; }
        [Column("Deposit")] public decimal? Deposit { get; set; }
        [Column("PaymentMethod")] public string? PaymentMethod { get; set; }
        [Column("BillStatus")] public string BillStatus { get; set; } = "";
        [Column("TableNumber")] public int TableNumber { get; set; }
        [Column("GuestCount")] public int? GuestCount { get; set; }
        [Column("OrderStatus")] public string OrderStatus { get; set; } = "";
        [Column("WorkerId")] public int WorkerId { get; set; }
        [Column("WorkerName")] public string WorkerName { get; set; } = "";
        [Column("RoleName")] public string RoleName { get; set; } = "";
    }

    // ============================================================
    //  DTO для vw_MenuSalesReport
    // ============================================================
    [Keyless]
    public class MenuSalesReportRow
    {
        [Column("OrderItemId")] public int OrderItemId { get; set; }
        [Column("OrderId")] public int OrderId { get; set; }
        [Column("ItemType")] public string ItemType { get; set; } = "";
        [Column("Quantity")] public int Quantity { get; set; }
        [Column("PriceAtOrder")] public decimal PriceAtOrder { get; set; }
        [Column("LineTotal")] public decimal LineTotal { get; set; }
        [Column("ProductId")] public int? ProductId { get; set; }
        [Column("ProductName")] public string? ProductName { get; set; }
        [Column("ProductSection")] public string? ProductSection { get; set; }
        [Column("TypeId")] public int? TypeId { get; set; }
        [Column("TypeName")] public string? TypeName { get; set; }
        [Column("OptionId")] public int? OptionId { get; set; }
        [Column("OptionName")] public string? OptionName { get; set; }
        [Column("OrderDate")] public DateTime OrderDate { get; set; }
    }

    // ============================================================
    //  DTO для vw_StaffWorkloadReport
    // ============================================================
    [Keyless]
    public class StaffWorkloadReportRow
    {
        [Column("OrderId")] public int OrderId { get; set; }
        [Column("TableNumber")] public int TableNumber { get; set; }
        [Column("OrderStatus")] public string OrderStatus { get; set; } = "";
        [Column("GuestCount")] public int? GuestCount { get; set; }
        [Column("OrderDate")] public DateTime OrderDate { get; set; }
        [Column("WorkerId")] public int WorkerId { get; set; }
        [Column("WorkerName")] public string WorkerName { get; set; } = "";
        [Column("RoleId")] public int RoleId { get; set; }
        [Column("RoleName")] public string RoleName { get; set; } = "";
        [Column("Salary")] public decimal Salary { get; set; }
        [Column("TotalAmount")] public decimal? TotalAmount { get; set; }
        [Column("BillStatus")] public string? BillStatus { get; set; }
        [Column("ItemCount")] public int ItemCount { get; set; }
    }
}
