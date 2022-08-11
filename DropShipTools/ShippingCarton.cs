using System.Collections.Generic;

namespace DropShipShipmentConfirmations
{
    public class ShippingCarton
    {
        public List<LineItem> LineItems { get; set; } = new List<LineItem>();
        public string TrackingNumber { get; set; }
        public string BoxID { get; set; }
    }
}
