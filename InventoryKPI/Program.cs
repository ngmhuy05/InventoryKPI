using InventoryKPI.Services;

string invoicesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Invoices");
string purchaseOrdersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PurchaseOrders");
string productFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "product.txt");

var fileProcessor = new FileProcessor();
var kpiCalculator = new KpiCalculator();
var reportWriter = new KpiReportWriter();

Console.WriteLine("=== Inventory KPI System ===");
Console.WriteLine("Starting...\n");

var products = await fileProcessor.ReadProductFileAsync(productFilePath);
Console.WriteLine($"[INFO] Loaded {products.Count} products\n");

using var watcherService = new FileWatcherService(fileProcessor, kpiCalculator);

watcherService.OnReloadComplete = () =>
{
    PrintAndSaveKpi(kpiCalculator, reportWriter);
    Console.WriteLine("\nPress [Q] to quit, [R] to recalculate\n");
};

watcherService.StartWatching(invoicesPath, purchaseOrdersPath);

while (true)
{
    var key = Console.ReadKey(intercept: true);

    if (key.Key == ConsoleKey.Q)
    {
        Console.WriteLine("\n[INFO] Shutting down...");
        break;
    }

    if (key.Key == ConsoleKey.R)
        PrintAndSaveKpi(kpiCalculator, reportWriter);
}

static void PrintAndSaveKpi(KpiCalculator calculator, KpiReportWriter writer)
{
    var result = calculator.Calculate();

    Console.WriteLine("\n========================================");
    Console.WriteLine($"  KPI REPORT — {result.CalculatedAt:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine("========================================");
    Console.WriteLine($"  Total SKUs              : {result.TotalSKUs}");
    Console.WriteLine($"  Cost of Inventory       : {result.CostOfInventory:C2}");
    Console.WriteLine($"  Out-of-Stock Items      : {result.OutOfStockItems}");
    Console.WriteLine($"  Average Daily Sales     : {result.AverageDailySales:N2} units/day");
    Console.WriteLine($"  Average Inventory Age   : {result.AverageInventoryAgeDays:N1} days");
    Console.WriteLine("========================================\n");

    _ = writer.WriteAsync(result);
}