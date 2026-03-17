using System.Text.Json.Serialization;

namespace InventoryKPI.Models
{
    public class InvoiceFile
    {
        [JsonPropertyName("Invoices")]
        public List<Invoice> Invoices { get; set; } = new();
    }

    public class Invoice
    {
        [JsonPropertyName("Type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("InvoiceID")]
        public string InvoiceID { get; set; } = "";

        [JsonPropertyName("InvoiceNumber")]
        public string InvoiceNumber { get; set; } = "";

        [JsonPropertyName("Status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("DateString")]
        public string DateString { get; set; } = "";

        [JsonPropertyName("LineItems")]
        public List<LineItem> LineItems { get; set; } = new();
    }

    public class LineItem
    {
        [JsonPropertyName("Description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("ItemCode")]
        public string? ItemCode { get; set; }

        [JsonPropertyName("Item")]
        public LineItemRef? Item { get; set; }

        [JsonPropertyName("Quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("UnitAmount")]
        public decimal UnitAmount { get; set; }

        [JsonPropertyName("LineItemID")]
        public string LineItemID { get; set; } = "";
    }

    public class LineItemRef
    {
        [JsonPropertyName("ItemID")]
        public string ItemID { get; set; } = "";

        [JsonPropertyName("Name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("Code")]
        public string Code { get; set; } = "";
    }
}