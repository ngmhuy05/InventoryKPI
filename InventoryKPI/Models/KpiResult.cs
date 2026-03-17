namespace InventoryKPI.Models
{
    public class KpiResult
    {
        public int TotalSKUs { get; set; }
        public decimal CostOfInventory { get; set; }
        public int OutOfStockItems { get; set; }
        public decimal AverageDailySales { get; set; }
        public double AverageInventoryAgeDays { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.Now;
    }
}