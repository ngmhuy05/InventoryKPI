using System.Text.Json.Serialization;

namespace InventoryKPI.Models
{
    public class ProductFile
    {
        [JsonPropertyName("Items")]
        public List<Product> Items { get; set; } = new();
    }

    public class Product
    {
        [JsonPropertyName("ItemID")]
        public string ItemID { get; set; } = "";

        [JsonPropertyName("Code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("Name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("PurchaseDetails")]
        public PriceDetail PurchaseDetails { get; set; } = new();

        [JsonPropertyName("SalesDetails")]
        public PriceDetail SalesDetails { get; set; } = new();
    }

    public class PriceDetail
    {
        [JsonPropertyName("UnitPrice")]
        public decimal UnitPrice { get; set; }
    }
}