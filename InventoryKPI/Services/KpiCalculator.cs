using InventoryKPI.Models;

namespace InventoryKPI.Services
{
    public class KpiCalculator
    {
        private readonly Dictionary<string, decimal> _totalPurchased = new();
        private readonly Dictionary<string, decimal> _totalSold = new();

        private readonly Dictionary<string, (decimal TotalCost, decimal TotalQty)> _purchaseCostData = new();
        private readonly Dictionary<string, List<(DateTime Date, decimal Qty)>> _purchaseLots = new();

        // FIX KPI 4: lưu min/max date thay vì HashSet
        private DateTime _minSaleDate = DateTime.MaxValue;
        private DateTime _maxSaleDate = DateTime.MinValue;

        private readonly object _lock = new();

        public void AddInvoices(List<Invoice> invoices)
        {
            lock (_lock)
            {
                foreach (var invoice in invoices)
                {
                    DateTime invoiceDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(invoice.DateString))
                        DateTime.TryParse(invoice.DateString, out invoiceDate);

                    foreach (var line in invoice.LineItems)
                    {
                        // Chỉ xử lý LineItem có Item.ItemID — giống Python notebook
                        if (string.IsNullOrWhiteSpace(line.Item?.ItemID))
                            continue;

                        string key = line.Item.ItemID;  // dùng ItemID làm key

                        if (line.Quantity <= 0) continue;

                        if (invoice.Type == "ACCPAY")
                        {
                            if (!_totalPurchased.ContainsKey(key))
                                _totalPurchased[key] = 0;
                            _totalPurchased[key] += line.Quantity;

                            if (!_purchaseCostData.ContainsKey(key))
                                _purchaseCostData[key] = (0, 0);
                            var (prevCost, prevQty) = _purchaseCostData[key];
                            _purchaseCostData[key] = (
                                prevCost + line.UnitAmount * line.Quantity,
                                prevQty + line.Quantity
                            );

                            if (!_purchaseLots.ContainsKey(key))
                                _purchaseLots[key] = new List<(DateTime, decimal)>();
                            _purchaseLots[key].Add((invoiceDate, line.Quantity));
                        }
                        else if (invoice.Type == "ACCREC")
                        {
                            if (!_totalSold.ContainsKey(key))
                                _totalSold[key] = 0;
                            _totalSold[key] += line.Quantity;

                            if (invoiceDate.Date < _minSaleDate) _minSaleDate = invoiceDate.Date;
                            if (invoiceDate.Date > _maxSaleDate) _maxSaleDate = invoiceDate.Date;
                        }
                    }
                }
            }
        }

        public KpiResult Calculate()
        {
            lock (_lock)
            {
                var allKeys = _totalPurchased.Keys
                    .Union(_totalSold.Keys)
                    .ToHashSet();

                // --- KPI 1: Total SKUs ---
                int totalSKUs = allKeys.Count;

                // --- KPI 2: Cost of Inventory ---
                decimal costOfInventory = 0;
                foreach (var key in allKeys)
                {
                    decimal purchased = _totalPurchased.GetValueOrDefault(key, 0);
                    decimal sold = _totalSold.GetValueOrDefault(key, 0);
                    decimal unsold = purchased - sold;

                    if (unsold > 0 && _purchaseCostData.TryGetValue(key, out var costData) && costData.TotalQty > 0)
                    {
                        decimal avgUnitCost = costData.TotalCost / costData.TotalQty;
                        costOfInventory += unsold * avgUnitCost;
                    }
                }

                // --- KPI 3: Out-of-Stock ---
                int outOfStock = allKeys.Count(key =>
                {
                    decimal purchased = _totalPurchased.GetValueOrDefault(key, 0);
                    decimal sold = _totalSold.GetValueOrDefault(key, 0);
                    return (purchased - sold) <= 0;
                });

                // --- KPI 4: Average Daily Sales ---
                // FIX: dùng (maxDate - minDate).Days + 1 đúng công thức assignment
                decimal totalQtySold = _totalSold.Values.Sum();
                int salesDays = _minSaleDate == DateTime.MaxValue ? 1
                    : Math.Max(1, (_maxSaleDate - _minSaleDate).Days + 1);
                decimal avgDailySales = totalQtySold / salesDays;

                // --- KPI 5: Average Inventory Age ---
                // FIX: simple average per SKU (earliest purchase date of unsold SKUs)
                // đúng công thức: Average(Current Date − Purchase Date of Unsold Items)
                DateTime today = DateTime.Now;
                var ages = new List<double>();

                foreach (var key in allKeys)
                {
                    decimal purchased = _totalPurchased.GetValueOrDefault(key, 0);
                    decimal sold = _totalSold.GetValueOrDefault(key, 0);
                    if (purchased - sold <= 0) continue;
                    if (!_purchaseLots.ContainsKey(key)) continue;

                    DateTime earliestPurchase = _purchaseLots[key].Min(l => l.Date);
                    double age = (today - earliestPurchase).TotalDays;
                    ages.Add(age);
                }

                double avgInventoryAge = ages.Count > 0 ? ages.Average() : 0;

                return new KpiResult
                {
                    TotalSKUs = totalSKUs,
                    CostOfInventory = Math.Round(costOfInventory, 2),
                    OutOfStockItems = outOfStock,
                    AverageDailySales = Math.Round(avgDailySales, 2),
                    AverageInventoryAgeDays = Math.Round(avgInventoryAge, 1),
                    CalculatedAt = DateTime.Now
                };
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _totalPurchased.Clear();
                _totalSold.Clear();
                _purchaseCostData.Clear();
                _purchaseLots.Clear();
                // FIX KPI 4: reset min/max date
                _minSaleDate = DateTime.MaxValue;
                _maxSaleDate = DateTime.MinValue;
            }
        }
    }
}