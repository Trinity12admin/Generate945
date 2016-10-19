using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using Dapper;
using DECK.OMS.Common.Contracts;
using DECK.OMS.Domain.Models;
using DECK.Plugins.Shipment.AzureQueue.Models;
using log4net;
using Microsoft.ServiceBus.Messaging;
using Address = DECK.Plugins.Shipment.AzureQueue.Models.Address;
using OrderAdjustmentTax = DECK.Plugins.Shipment.AzureQueue.Models.OrderAdjustmentTax;
using OrderItemAdjustmentTax = DECK.OMS.Domain.Models.OrderItemAdjustmentTax;

namespace FUI.Middleware
{
    /// <summary>
    /// Used to process new order messages from OMS that need to be sent to warehouse
    /// </summary>
    public class NewOrderProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(NewOrderProcessor));

        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["DBconnection"].ConnectionString;


        /// <summary>
        /// Process a new order from queue and send to warehouse by saving to warehouse SQL tables
        /// </summary>
        /// <param name="message"></param>
        /// <param name="uniqueId"></param>
        /// <returns></returns>
        public bool SendOrderToWarehouse(BrokeredMessage message, string uniqueId)
        {

            OrderShipment order;
            try
            {
                XmlObjectSerializer serializer = new DataContractSerializer(typeof(OrderShipment), new[]
                {
                typeof (Address), typeof (SimpleTotalInformation),
                typeof (OrderAdjustmentTax), typeof (OrderShipmentItem), typeof (OrderItemAdjustment),
                typeof (OrderItemTax), typeof (OrderItemAdjustmentPaymentTransaction), typeof (OrderItemAdjustmentTax),
                typeof (Extended), typeof(OrderSimplePayment), typeof(OrderTax)
                });
                order = message.GetBody<OrderShipment>(serializer);
            }
            catch (Exception e)
            {
                Log.Error("Error getting order data from message queue", e);

                //Abandons the lock on a peek-locked message, so it can be retried (dead letter/max retry is handled by parent)
                message.Abandon();
                return false;
            }

            string defaultItemInventoryType = "STL";

            //Save data to database for warehouse to pull from using Sql Transaction
            try
            {
                int intOrderNumber;
                if (!Int32.TryParse(order.OrderNumber, out intOrderNumber))
                    intOrderNumber = -1;

                int intShipmentNumber;
                int shipmentOffset = 0;
                if (!string.IsNullOrEmpty(order.ShipmentNumber) && order.ShipmentNumber.StartsWith("G"))
                {
                    //gift card--add 50 to value to separate from regular shipments
                    shipmentOffset = 50;
                    order.ShipmentNumber = order.ShipmentNumber.Replace("G", "");
                    order.ShippingMethod = "Gift";
                    order.OrderItems.ForEach(i => i.Quantity = 1);  //ensure quantity is set to 1
                    defaultItemInventoryType = "DROP";
                }
                if (!Int32.TryParse(order.ShipmentNumber, out intShipmentNumber))
                    intShipmentNumber = -1;
                else
                    intShipmentNumber = intShipmentNumber + shipmentOffset;

                using (SqlConnection sqlConnection = new SqlConnection(_connectionString))
                {
                    sqlConnection.Open();
                    IDbTransaction transaction = null;
                    try
                    {
                        transaction = sqlConnection.BeginTransaction();

                        sqlConnection.Execute("SaveOrderShipment",
                            new
                            {
                                #region Parameters For Save Order Shipment
                                SiteCode = "FUI",
                                OrderNumber = intOrderNumber,
                                ShipmentNumber = intShipmentNumber,
                                OrderDate = order.OrderDateUTC,
                                BillFirstName = TruncateLongString(order.BillingAddress.FirstName, 50),
                                BillLastName = TruncateLongString(order.BillingAddress.LastName, 50),
                                BillAddress =
                                    TruncateLongString(GetStringWithoutNull(order.BillingAddress.Address1), 50),
                                BillAddress2 =
                                    TruncateLongString(GetStringWithoutNull(order.BillingAddress.Address2), 50),
                                BillAddress3 =
                                    TruncateLongString(GetStringWithoutNull(order.BillingAddress.Address3), 50),
                                BillCity = TruncateLongString(order.BillingAddress.City, 50),
                                BillState = TruncateLongString(order.BillingAddress.Province, 4),
                                BillPostalCode = TruncateLongString(order.BillingAddress.PostalCode, 10),
                                BillCountry = TruncateLongString(order.BillingAddress.Country, 2),
                                BillPhone = order.BillingAddress.Phone.Length == 10 ? order.BillingAddress.Phone : "6366802750",
                                ShipFirstName = TruncateLongString(order.ShippingAddress.FirstName, 50),
                                ShipLastName = TruncateLongString(order.ShippingAddress.LastName, 50),
                                ShipAddress =
                                    TruncateLongString(GetStringWithoutNull(order.ShippingAddress.Address1), 50),
                                ShipAddress2 =
                                    TruncateLongString(GetStringWithoutNull(order.ShippingAddress.Address2), 50),
                                ShipAddress3 =
                                    TruncateLongString(GetStringWithoutNull(order.ShippingAddress.Address3), 50),
                                ShipCity = TruncateLongString(order.ShippingAddress.City, 50),
                                ShipState = TruncateLongString(order.ShippingAddress.Province, 4),
                                ShipPostalCode = TruncateLongString(order.ShippingAddress.PostalCode, 10),
                                ShipCountry = TruncateLongString(order.ShippingAddress.Country, 2),
                                ShipPhone = order.ShippingAddress.Phone.Length == 10 ? order.ShippingAddress.Phone : "6366802750",
                                EmailAddress = TruncateLongString(order.BillingAddress.Email, 50),
                                ShippingMethod = TruncateLongString(GetStringWithoutNull(order.ShippingMethod), 20),
                                SubTotal = order.Totals.SubTotal,
                                //no discount in order since all discounts are in each item
                                DiscountTotal = 0,
                                Freight = order.Totals.Shipping,
                                //TODO: Get tax totals - use a tax table
                                TaxAmount = 0,
                                OrderTotal = order.Totals.Total
                                #endregion
                            },
                            commandType: CommandType.StoredProcedure, transaction: transaction);


                        //loop to save each item
                        foreach (IOrderItemShipmentPlugin item in order.OrderItems)
                        {
                            decimal itemExtPrice = item.Price - item.TotalDiscount;

                            sqlConnection.Execute("SaveOrderShipmentItem",
                                new
                                {
                                    #region Parameters For Save Order Shipment Item
                                    SiteCode = "FUI",
                                    OrderNumber = intOrderNumber,
                                    ShipmentNumber = intShipmentNumber,
                                    ItemId = item.ItemId,
                                    ItemSku = TruncateLongString(item.GTIN, 20),
                                    ItemDescrip = TruncateLongString(item.GetFullDescription, 100),
                                    Quantity = item.Quantity,
                                    EachPrice = item.Price,
                                    ItemDiscount = item.TotalDiscount,
                                    ExtPrice = itemExtPrice,
                                    Routing = "STL",
                                    WarehouseCode = "STL"
                                    #endregion
                                },
                                commandType: CommandType.StoredProcedure, transaction: transaction);
                        } //end of item loop

                        transaction.Commit();
                        sqlConnection.Close();
                    }
                    catch (Exception e)
                    {
                        Log.Error("Error saving shipment, rolling transaction back", e);
                        if (transaction != null)
                            transaction.Rollback();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error saving shipment data to database", e);
                message.Abandon();
                return false;
            }


            //Log completion and mark message as procesed
            Log.Info("Successfully inserted order for unique ID (" + uniqueId + ")");
            message.Complete();

            return true;
        }

        /// <summary>
        /// Helper string function to prevent truncation issues.
        /// </summary>
        /// <param name="str">string to check</param>
        /// <param name="maxLength">Max field length</param>
        /// <returns></returns>
        public string TruncateLongString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            return str.Substring(0, Math.Min(str.Length, maxLength));
        }

        /// <summary>
        /// Helper string function to help with null values.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public string GetStringWithoutNull(string s)
        {
            if (s == null)
                return string.Empty;
            return s;
        }
    }
}
