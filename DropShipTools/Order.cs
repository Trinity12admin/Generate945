using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data; 
//using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DropShipShipmentConfirmations;

internal class Order
{
    public List<ShippingCarton> Cartons { get; private set; }
    public string OrderNumber => _orderNumber;
    private List<LineItem> OrderedItems;
    private readonly List<LineItem> ShippedItems;
    private readonly string _orderNumber;
    private readonly bool _IsB2B;

    public Order(string orderNumber, bool b2b)
    {
        Cartons = new List<ShippingCarton>();
        OrderedItems = new List<LineItem>();
        ShippedItems = new List<LineItem>();
        _orderNumber = orderNumber;
        _IsB2B = b2b;

        GetOrderedItems();
        try
        {
            Console.WriteLine(orderNumber);
            GetShippedItems();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        ConsolidateLineItems();
        CreateCartons();
    }

    public int TotalItems => OrderedItems.Sum(li => li.QtyShipped);
    public int TotalCartons => Cartons.Count;
    public bool IsB2B => _IsB2B;
    public string OriginalOrderNumber => Regex.Replace(_orderNumber, @"-FUI(\d){3,5}", "");

    public int TotalWeight
    {
        get
        {
            int boxWeight = (int)(TotalCartons * 0.3M);
            int itemWeight = TotalItems * 2;
            return itemWeight + boxWeight;
        }
    }

    private bool GetOrderedItems()
    {
        try
        {
            string sqlConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];
            var connection1 = new SqlConnection(sqlConnectionString);
            using var commandHeader = new SqlCommand("Drop_Ship_945_Export_Detail", connection1);
            commandHeader.CommandType = CommandType.StoredProcedure;
            commandHeader.Parameters.AddWithValue("@OrderNumber", _orderNumber);

            connection1.Open();

            var reader = commandHeader.ExecuteReader();
            while (reader.Read())
            {
                var orderQty = Enumerable.Range(1, (int)reader["W1202"]); //convert QTYs to singles
                foreach (int _ in orderQty)
                {
                    var lineItem = new LineItem
                    {
                        W1202 = 1, //Quantity Ordered
                        W1203 = 0, //Number of Units Shipped - start at 0, then add the shipped quantities
                        W1205 = reader["W1205"].ToString().Trim(), //Unit of measure
                        W1207 = reader["W1207"].ToString().Trim(), //Product/Service ID Qualifier
                        W1208 = reader["W1208"].ToString().Trim(), //SKU
                        W1221 = reader["W1221"].ToString().Trim(), //Product/Service ID Qualifier
                        W1222 = reader["W1222"].ToString().Trim(), //Seller’s Style Number
                        N902 = int.Parse(reader["N902"].ToString().Trim(),
                            CultureInfo.InvariantCulture), //PO line numberSeller’s Style Number
                        TrackingNumber = reader["MAN05"].ToString().Trim()
                    };
                    OrderedItems.Add(lineItem);
                }
            }

            connection1.Close();
        }
        catch (SqlException e)
        {
            Console.WriteLine(e.ToString());
            return false;
        }

        return true;
    }

    private void GetShippedItems()
    {
        string SqlConnectionString = ConfigurationManager.AppSettings["DBConnectionStringRBI"];
        var connection2 = new SqlConnection(SqlConnectionString);
        connection2.Open();
        using var sqlCommand = new SqlCommand("usp_B2BShipmentDetails", connection2);
        sqlCommand.CommandType = CommandType.StoredProcedure;
        sqlCommand.Parameters.AddWithValue("@OrderNumber", _orderNumber);

        var reader2 = sqlCommand.ExecuteReader();
        while (reader2.Read())
        {
            var shippedQty = Enumerable.Range(1, (int)reader2["QTY"]); //convert QTYs to singles
            foreach (int _ in shippedQty) //create single line items from QTYs
            {
                var lineItem = new LineItem
                {
                    BOXID = reader2["BoxID"].ToString().Trim(),
                    SKU = reader2["SKU"].ToString().Trim(),
                    QtyShipped = 1,
                    TrackingNumber = reader2["TrackingNumber"].ToString().Trim()
                };
                ShippedItems.Add(lineItem);
            }
        }

        connection2.Close();
    }

    private void ConsolidateLineItems()
    {
        string b2CBoxId = "00006802750" + EDIControlNumbers.LabelID().ToString("00000000", CultureInfo.InvariantCulture)
            .AppendCheckDigit();

        //Update the line item with the ship qty
        ShippedItems.ForEach(shippedItem =>
        {
            var currentItem =
                OrderedItems.Find(lineItem => lineItem.W1208 == shippedItem.SKU && lineItem.BOXID == null);
            if (currentItem == null) return;

            currentItem.QtyShipped = shippedItem.QtyShipped;
            currentItem.TrackingNumber = string.IsNullOrEmpty(shippedItem.TrackingNumber)
                ? currentItem.TrackingNumber
                : shippedItem.TrackingNumber;
            currentItem.BOXID = _IsB2B
                ? shippedItem.BOXID
                : b2CBoxId;
        });


        //Roll up line items by Box and W1208 (SKU)
        var consolidatedLineItems = OrderedItems.GroupBy(line => new { line.W1208, line.BOXID, line.N902 }).Select(
            line => new LineItem
            {
                BOXID = line.Key.BOXID,
                W1208 = line.Key.W1208,
                N902 = line.Key.N902,
                W1202 = line.Sum(q => q.W1202), //Qty ordered
                W1203 = line.Sum(q => q.QtyShipped), //Qty Shipped
                W1205 = line.Min(m => m.W1205), //UOM
                W1207 = line.Min(m => m.W1207), //ID
                W1221 = line.Min(m => m.W1221), //ID
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
        var boxes = OrderedItems.GroupBy(item => item.BOXID).ToList();
        boxes.ForEach(box =>
        {
            var carton = new ShippingCarton
            {
                BoxID = IsB2B ? box.Key : box.ToList().First().BOXID //set box ID from line items
            };
            carton.LineItems.AddRange(box.ToList()); //add line items to the carton
            carton.TrackingNumber = carton.LineItems[0].TrackingNumber;
            if (carton.LineItems.Sum(s => s.QtyShipped) >
                0) Cartons.Add(carton); //add tracking number to carton if it has a shipped qty of items.
        });
    }
}