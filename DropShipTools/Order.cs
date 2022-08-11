using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DropShipShipmentConfirmations
{
    internal class Order
    {
        public List<ShippingCarton> Cartons { get; private set; }
        private List<LineItem> OrderedItems;
        private readonly List<LineItem> PackedItems;
        private readonly string _orderNumber;
        private readonly bool _IsB2B;

        public Order(string orderNumber, bool b2b)
        {
            Cartons = new List<ShippingCarton>();
            OrderedItems = new List<LineItem>();
            PackedItems = new List<LineItem>();
            _orderNumber = orderNumber;
            //_orderType = orderType;
            _IsB2B = b2b;

            GetLineItems();
            ConsolidateLineItems();
            CreateCartons();
        }

        public int TotalItems
        {
            get
            {
                return OrderedItems.Sum(li => li.QtyShipped);
            }
        }

        public int TotalWeight
        {
            get
            {
                int boxWeight = (int)(TotalCartons * 0.3M);
                int itemWeight = TotalItems * 2;
                return itemWeight + boxWeight;
            }
        }
        public int TotalCartons
        {
            get
            {
                return Cartons.Count;
            }
        }

        //public string OrderNumber
        //{
        //    get { return _orderNumber; }
        //}

        public bool IsB2B => _IsB2B;

        //public string TrackingNumber;

        public string OriginalOrderNumber => Regex.Replace(_orderNumber, @"-FUI(\d){3,5}", "");

        private bool GetLineItems()
        {
            try
            {
                string SqlConnectionString = "Server=10.3.1.250; User ID=sa; Password=9ure3atEbexe; Database=EDI;";
                SqlConnection connection1 = new SqlConnection(SqlConnectionString);
                //using (connection1)
                //{
                SqlCommand commandHeader = new SqlCommand("Drop_Ship_945_Export_Detail", connection1);
                commandHeader.CommandType = CommandType.StoredProcedure;
                commandHeader.Parameters.AddWithValue("@OrderNumber", this._orderNumber);

                connection1.Open();

                SqlDataReader reader = commandHeader.ExecuteReader();
                while (reader.Read())
                {
                    IEnumerable<int> orderedqty = Enumerable.Range(1, (int)reader["W1202"]);          //convert qty's to singles
                    foreach (int _ in orderedqty)
                    {
                        LineItem lineItem = new LineItem
                        {
                            W1202 = 1,                                  //Quantity Ordered
                            W1203 = 1,                                  //Number of Units Shipped  //TODO:Change to actual qty (B2B vs B2C)
                            W1205 = reader["W1205"].ToString().Trim(),  //Unit of measure
                            W1207 = reader["W1207"].ToString().Trim(),  //Product/Service ID Qualifier
                            W1208 = reader["W1208"].ToString().Trim(),  //SKU
                            W1221 = reader["W1221"].ToString().Trim(),  //Product/Service ID Qualifier
                            W1222 = reader["W1222"].ToString().Trim(),  //Seller’s Style Number
                            N902 = Int32.Parse(reader["N902"].ToString().Trim(), CultureInfo.InvariantCulture),  //PO line numberSeller’s Style Number
                            TrackingNumber = reader["MAN05"].ToString().Trim()
                        };
                        OrderedItems.Add(lineItem);
                    }
                }
                //}
                connection1.Close();
                //}

                if (_IsB2B)   //get packed items
                {

                    SqlConnectionString = "Server=10.3.0.12; User ID=sa; Password=9ure3atEbexe; Database=WMS2";
                    SqlConnection connection2 = new SqlConnection(SqlConnectionString);
                    //using (connection2)
                    //{
                        connection2.Open();

                        //GET ORDER HEADER
                        SqlCommand commandHeader2 = new SqlCommand("usp_B2BShipmentDetails", connection2);
                        //using (SqlCommand commandHeader = sqlCommand)
                        //{
                        commandHeader2.CommandType = CommandType.StoredProcedure;
                        commandHeader2.Parameters.AddWithValue("@OrderNumber", this._orderNumber);
                        //          commandHeader.ExecuteNonQuery();

                        SqlDataReader reader2 = commandHeader2.ExecuteReader();
                        while (reader2.Read())
                        {
                            IEnumerable<int> shippedQty = Enumerable.Range(1, (int)reader2["QTY"]);          //convert qty's to singles
                            foreach (int qty in shippedQty)                                                 //create single line items from qtys
                            {
                                LineItem lineItem = new LineItem
                                {
                                    BOXID = reader2["BoxID"].ToString().Trim(),
                                    SKU = reader2["SKU"].ToString().Trim(),
                                    QtyShipped = 1,
                                    TrackingNumber = reader2["TrackingNumber"].ToString().Trim(),
                                };
                                PackedItems.Add(lineItem);
                            }
                        }
                        //}
                        connection2.Close();
                    //}

                }
                else
                {

                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
            return true;
        }

        private void ConsolidateLineItems()
        {

            ///Apply Packed Items to Ordered Items

            if (IsB2B)
            {
                //Update the line item with the ship qty
                PackedItems.ForEach(s_item =>
                {
                    LineItem currentItem = OrderedItems.Find(l_item => l_item.W1208 == s_item.SKU && l_item.BOXID == null);
                    currentItem.QtyShipped = s_item.QtyShipped;
                    currentItem.TrackingNumber = s_item.TrackingNumber ?? currentItem.TrackingNumber;
                    currentItem.BOXID = s_item.BOXID;

                });
            }
            else
            {
                string boxid = "00006802750" + BoxLabel.GetLabelID().ToString("00000000", CultureInfo.InvariantCulture).AppendCheckDigit();
                OrderedItems.ForEach(currentItem =>
                {
                    currentItem.BOXID = boxid;
                    currentItem.QtyShipped = currentItem.W1203;   //TODO: Change Qty Shipped to line items shipped (not W1203)
                });
            }

            //Roll up line items by Box and W1208 (SKU)
            List<LineItem> consolidatedLineItems = OrderedItems.GroupBy(line => new { line.W1208, line.BOXID, line.N902 }).Select(line => new LineItem
            {
                BOXID = line.Key.BOXID,
                W1208 = line.Key.W1208,
                N902 = line.Key.N902,
                W1202 = line.Sum(q => q.W1202),             //Qty ordered
                W1203 = line.Sum(q => q.W1203),             //Qty Shipped //TODO: Change to QtyShipped
                W1205 = line.Min(m => m.W1205),             //UOM
                W1207 = line.Min(m => m.W1207),             //ID
                W1221 = line.Min(m => m.W1221),             //ID
                SKU = line.Min(m => m.SKU),
                W1222 = line.Min(m => m.W1222),
//                N902 = line.Min(m => m.N902),
                TrackingNumber = line.Min(t => t.TrackingNumber),
                QtyShipped = line.Sum(q => q.QtyShipped)
            }).ToList();

            OrderedItems = consolidatedLineItems;
        }

        private void CreateCartons()
        {
            //            var boxes = LineItems.GroupBy(item => item.BOXID).ToList();             //get list of unique boxes
            var boxes = OrderedItems.GroupBy(item => item.BOXID).ToList();
            boxes.ForEach(box =>
            {
                ShippingCarton carton = new ShippingCarton
                {
                    BoxID = IsB2B ? box.Key : box.ToList().First().BOXID            //set box ID from line items
                };
                Console.WriteLine(box.Key);
                carton.LineItems.AddRange(box.ToList());                   //add line items to the carton
                carton.TrackingNumber = carton.LineItems[0].TrackingNumber;
                carton.LineItems.ForEach(l => Console.WriteLine($"   {l.W1208} {l.QtyShipped} LINE: {l.N902} "));
                if (carton.LineItems.Sum(s => s.QtyShipped) > 0)
                {
                    Cartons.Add(carton);  //add tracking number to carton if it has a shipped qty of items.
                }                               
                                                              
            });
        }

    }

}
