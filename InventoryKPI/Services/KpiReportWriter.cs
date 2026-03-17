using System.Text.Json;
using InventoryKPI.Models;

namespace InventoryKPI.Services
{
    public class KpiReportWriter
    {
        private const string ReportPath = "kpi_report.json";

        public async Task WriteAsync(KpiResult result)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(result, options);
                await File.WriteAllTextAsync(ReportPath, json);
                Console.WriteLine($"[INFO] KPI report saved to {ReportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not write KPI report: {ex.Message}");
            }
        }
    }
}