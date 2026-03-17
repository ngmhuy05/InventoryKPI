using System.Text.Json;
using InventoryKPI.Models;

namespace InventoryKPI.Services
{
    public class FileProcessor
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<List<Invoice>> ReadInvoiceFileAsync(string filePath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var invoiceFile = JsonSerializer.Deserialize<InvoiceFile>(json, _options);

                if (invoiceFile?.Invoices == null)
                    return new List<Invoice>();

                // Chỉ lấy ACCREC và ACCPAY, bỏ qua VOIDED/DELETED
                return invoiceFile.Invoices
                    .Where(i => i.Status != "VOIDED" && i.Status != "DELETED")
                    .Where(i => i.Type == "ACCREC" || i.Type == "ACCPAY")
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Cannot read file {filePath}: {ex.Message}");
                return new List<Invoice>();
            }
        }

        public async Task<List<Product>> ReadProductFileAsync(string filePath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var productFile = JsonSerializer.Deserialize<ProductFile>(json, _options);
                return productFile?.Items ?? new List<Product>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Cannot read product file: {ex.Message}");
                return new List<Product>();
            }
        }
    }
}