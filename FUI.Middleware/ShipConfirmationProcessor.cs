using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DECK.OMS.Domain.Models.API;
using log4net;

namespace FUI.Middleware
{
    public class ShipConfirmationProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ShipConfirmationProcessor));
        private readonly ShipmentRepository _repository;
        private readonly string _siteId = ConfigurationManager.AppSettings["siteCode"];
        private readonly string _apiKey = ConfigurationManager.AppSettings["apiKey"];

        /// <summary>
        /// Constructor -- instantiate repository
        /// </summary>
        public ShipConfirmationProcessor()
        {
            _repository = new ShipmentRepository();
        }

        /// <summary>
        /// Query new ship confirmations by accessing database, and convert them into appropriate model
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AsnOrder> GetNewShipConfirmations()
        {
            List<AsnOrder> asns = new List<AsnOrder>();
            IEnumerable<AsnOrderCollectionResult> data = _repository.GetShipconfirmationData();

            foreach (var temp in data)
            {
                try
                {
                    string[] orderParts = temp.CustomerPoRef.Split('-');
                    var asnOrder = new AsnOrder
                    {
                        IDInterfaceShipmentConfirmationHeader = temp.IDInterfaceShipmentConfirmationHeader,
                        CarrierCode = temp.CarrierCode,
                        CustomerPoRef = orderParts[0],
                        PONumber = orderParts[0],
                        ShipDate = temp.ShipDate,
                        SiteCode = _siteId,
                        Items = _repository.GetShipConfirmationItems(temp.CustomerPoRef)
                    };
                    asns.Add(asnOrder);
                }
                catch (Exception e)
                {
                    Log.Error("Error getting new ship confirmations", e);
                }
            }
            return asns;
        }

        /// <summary>
        /// Convert ASN data model into the format needed for the OMS API
        /// </summary>
        /// <param name="asn"></param>
        /// <returns></returns>
        public InboundASNInput ConvertAsn(AsnOrder asn)
        {
            asn.TimestampUTC = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);

            return new InboundASNInput
            {
                CarrierCode = asn.CarrierCode,
                CustomerPoRef = asn.CustomerPoRef,
                ExpectedDeliveryDate = asn.ExpectedDeliveryDate,
                Items = asn.Items,
                PONumber = asn.PONumber,
                ShipDate = asn.ShipDate,
                ShipmentNumber = asn.ShipmentNumber,
                SiteCode = asn.SiteCode,
                TimestampUTC = asn.TimestampUTC,
                VerificationKey = asn.GetVerificationKey("api/OmsInboundAsn", _apiKey)
            };
        }

        /// <summary>
        /// Mark an ASN as acknowledged so we don't process again
        /// </summary>
        /// <param name="id"></param>
        public void AcknowledgeAsn(int id)
        {
            _repository.UpdateDbStatus(id, "DONE");
        }
    }
}
