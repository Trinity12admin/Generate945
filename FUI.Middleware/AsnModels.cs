using System;
using DECK.OMS.Domain.Models.API;

namespace FUI.Middleware
{
    public class AsnOrder : InboundASNInput
    {
        public int IDInterfaceShipmentConfirmationHeader { get; set; }
    }

    public class AsnOrderCollectionResult
    {
        public string CarrierCode { get; set; }
        public string CustomerPoRef { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public int IDInterfaceShipmentConfirmationHeader { get; set; }
        public string PONumber { get; set; }
        public DateTime ShipDate { get; set; }
        public string SiteCode { get; set; }
        public string SKU { get; set; }
        public string TrackingNumber { get; set; }
        public int UnitsShipped { get; set; }
    }
}
