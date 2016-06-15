using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using DECK.OMS.Domain.Models.API;
using log4net;

namespace FUI.Middleware
{
    public class ShipmentRepository
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["DBconnection"].ConnectionString;
        private static readonly ILog Log = LogManager.GetLogger(typeof(ShipmentRepository));

        /// <summary>
        /// Get ship confirmation items for a specific order
        /// </summary>
        /// <param name="customerPoRef"></param>
        /// <returns>List of items for specified ASN</returns>
        public List<ASNInputItem> GetShipConfirmationItems(string customerPoRef)
        {
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
                {
                    List<ASNInputItem> asnItems = new List<ASNInputItem>();
                    sqlConnection.Open();

                    const string sql = @"select ltrim(rtrim(SKU)) AS GTIN, qty AS UnitsShipped, ltrim(rtrim(tracking_number)) AS TrackingNumber, a.GP_order AS CustomerPoRef, a.auto_id AS IDInterfaceShipmentConfirmationHeader
                                                FROM FUI_ship_confirmations a
                                                inner join [dbo].FUI_ship_confirmation_detail b
                                                on a.GP_order = b.GP_order 
                                                    Where a.GP_order = @CustomerPoRef AND IsNull(b.admin_status, '') = '' AND IsNull(a.admin_status, '') = ''";

                    asnItems = sqlConnection.Query<ASNInputItem>(sql, new { CustomerPoRef = customerPoRef }, commandType: CommandType.Text).ToList();
                    return asnItems;
                }

            }
            catch (Exception e)
            {
                Log.Error("Error querying ASNs from database", e);
                return new List<ASNInputItem>();
            }
        }

        /// <summary>
        /// Get all the header records for new ship confirmations
        /// </summary>
        /// <returns>List of ASN header records</returns>
        public IEnumerable<AsnOrderCollectionResult> GetShipconfirmationData()
        {
            try
            {
                IEnumerable<AsnOrderCollectionResult> asnResults;
                using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
                {
                    sqlConnection.Open();

                    //Get any new ship confirmations that are at least 30 seconds old (30 seconds is to make sure all item
                    //data has been written to database as well and we aren't reading only partial information
                    const string sql = @"select a.auto_id AS IDInterfaceShipmentConfirmationHeader, a.GP_order AS CustomerPoRef, a.GP_order AS PONumber,
                                                   ship_date AS ShipDate, shipping_method AS CarrierCode
                                                    FROM [dbo].FUI_ship_confirmations a WHERE IsNull(admin_status, '') = '' AND DATEDIFF(second, auto_date, GETDATE()) > 30";

                    asnResults = sqlConnection.Query<AsnOrderCollectionResult>(sql, commandType: CommandType.Text).ToList();
                }
                return asnResults;
            }
            catch (Exception e)
            {
                Log.Error("Error querying ASN items from database", e);
                return new List<AsnOrderCollectionResult>();
            }
        }

        /// <summary>
        /// Update the status of the ship confirmation so we don't process this record again
        /// </summary>
        /// <param name="interfaceId"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public bool UpdateDbStatus(int interfaceId, string status)
        {
            bool returnStatus = false;
            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
                {
                    sqlConnection.Open();

                    const string sql = @"update FUI_ship_confirmations set admin_status = @status where auto_id = @id; " +
                            "update b set admin_status = @status from FUI_ship_confirmations a inner join FUI_ship_confirmation_detail b on a.GP_order = b.GP_order where a.auto_id = @id and isnull(b.admin_status, '') = ''";

                    sqlConnection.Execute(sql, new { status, id = interfaceId }, commandType: CommandType.Text);

                    returnStatus = true;
                }
            }
            catch (Exception e)
            {
                Log.Error("Error updating asn status", e);
            }

            return returnStatus;
        }
    }
}
