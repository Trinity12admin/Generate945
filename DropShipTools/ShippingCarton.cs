using System.Collections.Generic;

namespace DropShipShipmentConfirmations;

public class ShippingCarton
{
    public List<LineItem> LineItems { get; set; } = new();
    public string TrackingNumber { get; set; }
    public string BoxID { get; set; }
}