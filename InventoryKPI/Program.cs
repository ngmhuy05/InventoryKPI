using InventoryKPI.Services;

string invoicesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Invoices");
string purchaseOrdersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PurchaseOrders");

var fileProcessor = new FileProcessor();
var kpiCalculator = new KpiCalculator();
var reportWriter = new KpiReportWriter();

Console.WriteLine("=== Inventory KPI System ===");
Console.WriteLine("Starting...\n");

using var watcherService = new FileWatcherService(fileProcessor, kpiCalculator);

watcherService.OnReloadComplete = () =>
{
    PrintAndSaveKpi(kpiCalculator, reportWriter);
    PrintMenu();
};

watcherService.StartWatching(invoicesPath, purchaseOrdersPath);

while (true)
{
    var key = Console.ReadKey(intercept: true);

    switch (key.Key)
    {
        case ConsoleKey.Q:
            Console.WriteLine("\n[INFO] Shutting down...");
            return;

        case ConsoleKey.D1:
            PrintAndSaveKpi(kpiCalculator, reportWriter);
            PrintMenu();
            break;

        case ConsoleKey.D2:
            PrintTopOutOfStock(kpiCalculator);
            PrintMenu();
            break;

        case ConsoleKey.D3:
            PrintTopHighestValue(kpiCalculator);
            PrintMenu();
            break;
    }
}

// --- Helpers ---

static void PrintMenu()
{
    Console.WriteLine("+-----------------------------+");
    Console.WriteLine("|        MENU OPTIONS         |");
    Console.WriteLine("+-----------------------------+");
    Console.WriteLine("|  [1]  KPI Summary           |");
    Console.WriteLine("|  [2]  Top 10 Out-of-Stock   |");
    Console.WriteLine("|  [3]  Top 10 Highest Value  |");
    Console.WriteLine("|  [Q]  Quit                  |");
    Console.WriteLine("+-----------------------------+");
    Console.WriteLine();
}

static void PrintAndSaveKpi(KpiCalculator calculator, KpiReportWriter writer)
{
    var result = calculator.Calculate();

    Console.WriteLine("\n========================================");
    Console.WriteLine($"  KPI REPORT -- {result.CalculatedAt:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine("========================================");
    Console.WriteLine($"  Total SKUs              : {result.TotalSKUs}");
    Console.WriteLine($"  Cost of Inventory       : {result.CostOfInventory:C2}");
    Console.WriteLine($"  Out-of-Stock Items      : {result.OutOfStockItems}");
    Console.WriteLine($"  Average Daily Sales     : {result.AverageDailySales:N2} units/day");
    Console.WriteLine($"  Average Inventory Age   : {result.AverageInventoryAgeDays:N1} days");
    Console.WriteLine("========================================\n");

    // Await write để tránh "[INFO] KPI report saved" chen vào giữa menu
    writer.WriteAsync(result).GetAwaiter().GetResult();
}

static void PrintTopOutOfStock(KpiCalculator calculator)
{
    var items = calculator.GetTopOutOfStock(10);

    string sep = "+----------------------------------------+------------+------------+------------+";
    Console.WriteLine("\n" + sep);
    Console.WriteLine("|  TOP 10 OUT-OF-STOCK SKUs                                                     |");
    Console.WriteLine(sep);
    Console.WriteLine($"| {"SKU",-38} | {"Purchased",10} | {"Sold",10} | {"Balance",10} |");
    Console.WriteLine(sep);

    if (items.Count == 0)
        Console.WriteLine("|  (no out-of-stock items)                                                      |");
    else
        foreach (var (key, purchased, sold, balance) in items)
            Console.WriteLine($"| {key,-38} | {purchased,10:N0} | {sold,10:N0} | {balance,10:N0} |");

    Console.WriteLine(sep + "\n");
}

static void PrintTopHighestValue(KpiCalculator calculator)
{
    var items = calculator.GetTopHighestValue(10);

    string sep = "+----------------------------------------+----------+--------------+----------------+";
    Console.WriteLine("\n" + sep);
    Console.WriteLine("|  TOP 10 HIGHEST INVENTORY VALUE SKUs                                              |");
    Console.WriteLine(sep);
    Console.WriteLine($"| {"SKU",-38} | {"Unsold",8} | {"Avg Cost",12} | {"Value",14} |");
    Console.WriteLine(sep);

    if (items.Count == 0)
        Console.WriteLine("|  (no inventory data)                                                           |");
    else
        foreach (var (key, unsold, avgCost, value) in items)
            Console.WriteLine($"| {key,-38} | {unsold,8:N0} | {avgCost,12:C2} | {value,14:C2} |");

    Console.WriteLine(sep + "\n");
}