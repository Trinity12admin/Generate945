namespace DropShipShipmentConfirmations;

public class LineItem
{
    public int W1202 { get; set; } //Quantity Ordered
    public int W1203 { get; set; } //Number of Units Shipped
    public string W1205 { get; set; } //Unit of measure
    public string W1207 { get; set; } //Product/Service ID Qualifier
    public string W1208 { get; set; } //SKU
    public string W1221 { get; set; } //Product/Service ID Qualifier
    public string W1222 { get; set; } //Seller’s Style Number
    public int N902 { get; set; } //PO line numberSeller’s Style Number
    public string TrackingNumber { get; set; }
    public string BOXID { get; set; } //Carton number
    public string SKU { get; set; } //SKU
    public int QtyShipped { get; set; } //Qty Shipped
}