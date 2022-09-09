using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net.Mail;
using System.Security;
using System.Text;
using FluentEmail;

namespace DropShipShipmentConfirmations;

public static class Export945Shipment
{
    public static bool Generate(string query, string connectionString, string pathForExport)
    {
        const string segterm = "~";
        const string elemsep = "*";

        Console.WriteLine(query);
        using var dataAdapter = new SqlDataAdapter(query, connectionString);
        dataAdapter.SelectCommand.CommandTimeout = 0; // disable SQL timeout
        
        while(true)
        {
            List<Completed945> completed945s = new List<Completed945>();

            using var dataSet = new DataSet();
            dataSet.Locale = CultureInfo.InvariantCulture;

            dataAdapter.Fill(dataSet);

            for (int tbl = 0; tbl < dataSet.Tables.Count; tbl++)
            {
                if (dataSet.Tables[tbl].Rows.Count == 0)
                {
                    continue;
                }
                
                using var dataTable = dataSet.Tables[tbl];
                var s = new StringBuilder();
                int groupCount = 0;
                int ln; // PO item line number
                int seg; // segment line number, from and including ST to SE

                var controlNumbers = EDIControlNumbers.NextControlNumbers(dataTable.Rows.Count);
                int interchangeControlNumber = controlNumbers.InterchangeControlNumber;
                int groupControlNumber = controlNumbers.GroupControlNumber;
                int transactionControlNumber = controlNumbers.TransactionControlNumber;
                int bolNumber = controlNumbers.BOLNumber;

                string filename =
                    $"945_{interchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture)}.edi";
                bool orderB2B;
                Order order;

                s.Clear();

                s.Append("ISA"); // ISA00
                s.Append(elemsep + "00"); // ISA01
                s.Append(elemsep + "          "); // ISA02
                s.Append(elemsep + "00"); // ISA03
                s.Append(elemsep + "          "); // ISA04
                s.Append(elemsep + "12"); // ISA05
                s.Append(elemsep + "6366802750     "); // ISA06
                s.Append(elemsep + "12"); // ISA07
                s.Append(elemsep + "6363439914     "); // ISA08
                s.Append(elemsep + DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture)); // ISA09
                s.Append(elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // ISA10
                s.Append(elemsep + "U"); // ISA11
                s.Append(elemsep + "00401"); // ISA12
                s.Append(elemsep +
                         interchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture)); // ISA13
                s.Append(elemsep + "0"); // ISA14
                s.Append(elemsep + "P"); // ISA15  T=Test P=Production
                s.Append(elemsep + ">"); // ISA16
                s.Append(segterm);


                s.Append("GS"); // GS00
                s.Append(elemsep + "SW"); // GS01
                s.Append(elemsep + "6366802750     "); // GS02
                s.Append(elemsep + "6363439914     "); // GS03
                s.Append(elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // GS04
                s.Append(elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // GS05
                s.Append(elemsep + groupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GS06
                s.Append(elemsep + "X"); // GS07
                s.Append(elemsep + "004010"); // GS08
                s.Append(segterm);


                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    ln = 0; // line number counter
                    seg = 0; // segment line number
                    string orderNumber = dataTable.Rows[i][dataTable.Columns.IndexOf("W0606")].ToString().Trim();
                    orderB2B = Convert.ToBoolean(Convert.ToInt16(dataTable.Rows[i][dataTable.Columns.IndexOf("IsB2B")],
                        CultureInfo.InvariantCulture));
                    order = new Order(orderNumber, orderB2B);

                    //START OF 945 Transaction
                    groupCount++;
                    seg++;
                    s.Append("ST"); // ST00
                    s.Append(elemsep + "945"); // ST01
                    s.Append(elemsep + transactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // ST02
                    s.Append(segterm);

                    seg++;
                    s.Append("W06"); // W0600
                    s.Append(elemsep + "N"); // W0601
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0602")].ToString()
                        .Trim()); // W0602
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0603")].ToString()
                        .Trim()); // W0603
                    s.Append(elemsep + ("6366802750" + bolNumber.ToString("0000000", CultureInfo.InvariantCulture))
                        .AppendCheckDigit()); // W0604
                    s.Append(elemsep); // W0605
                    s.Append(elemsep + order.OriginalOrderNumber.Trim()); // W0606
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0607")].ToString()
                        .Trim()); // W0607
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0608")].ToString()
                        .Trim()); // W0608
                    s.Append(segterm);

                    bolNumber++;

                    seg++;
                    s.Append("N1"); // N100 ship-to segment
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST01")].ToString()
                        .Trim()); // N101
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST02")].ToString()
                        .Trim()); // N102
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST03")].ToString()
                        .Trim()); // N103
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST04")].ToString()
                        .Trim()); // N104
                    s.Append(segterm);

                    seg++;
                    s.Append("N1"); // N100 ship-from segment
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF01")].ToString()
                        .Trim()); // N101
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF02")].ToString()
                        .Trim()); // N102
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF03")].ToString()
                        .Trim()); // N103
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF04")].ToString()
                        .Trim()); // N104
                    s.Append(segterm);

                    seg++;
                    s.Append("G62"); // G6200
                    s.Append(elemsep + "10"); // G6201
                    s.Append(elemsep +
                             DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // G6202
                    s.Append(segterm);

                    seg++;
                    s.Append("W27"); // W2700
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2701")].ToString()
                        .Trim()); // W2701
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2702")].ToString()
                        .Trim()); // W2702
                    s.Append(elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2703")].ToString()
                        .Trim()); // W2703
                    s.Append(elemsep + "CC"); // W2704
                    s.Append(elemsep); // W2705
                    s.Append(elemsep); // W2706
                    s.Append(elemsep); // W2707
                    s.Append(elemsep +
                             "CC"); // W2708 -- Partial shipments are not currently implemented
                    s.Append(elemsep); // W2709
                    s.Append(segterm);

                    //Shipping Cost

                    if (decimal.TryParse(dataTable.Rows[i][dataTable.Columns.IndexOf("GS7208")].ToString(),
                            out decimal shippingCost)) //was there a billable cost?
                    {
                        seg++;
                        s.Append("G72"); //GS7200
                        s.Append(elemsep + "504"); //GS7201
                        s.Append(elemsep + "06"); //GS7202
                        s.Append(elemsep); //GS7203
                        s.Append(elemsep); //GS7204
                        s.Append(elemsep); //GS7205
                        s.Append(elemsep); //GS7206
                        s.Append(elemsep); //GS7207                            
                        s.Append(elemsep +
                                 ((int)(shippingCost * 100.0M)).ToString(CultureInfo
                                     .InvariantCulture)); //GS7208  //Remove decimal for EDI file
                        s.Append(segterm);
                    }

                    //NEW LX / MAN / N9 / W12 / N9 LOOP
                    //Start LX Loop
                    foreach (var carton in order.Cartons)
                    {
                        seg++;
                        s.Append("LX"); // LX00
                        s.Append(elemsep + ++ln); // LX01
                        s.Append(segterm);

                        seg++;
                        s.Append("MAN"); // MAN00
                        s.Append(elemsep + "GM"); // MAN01
                        s.Append(elemsep + carton.BoxID); // MAN02
                        s.Append(elemsep); // MAN03
                        string cartonIdentifier = "";
                        if (!string.IsNullOrEmpty(carton.TrackingNumber)) cartonIdentifier = "CP";
                        s.Append(elemsep + cartonIdentifier); // MAN04
                        s.Append(elemsep + carton.TrackingNumber); // MAN05
                        s.Append(segterm);

                        seg++;
                        s.Append("N9"); // N900
                        s.Append(elemsep + "CTC"); // N901
                        s.Append(elemsep + "A"); // N902
                        s.Append(segterm);

                        foreach (var item in carton.LineItems)
                        {
                            seg++;
                            s.Append("W12"); // W1200
                            s.Append(elemsep + "SH"); // W1201
                            s.Append(elemsep + item.W1202); // W1202
                            s.Append(elemsep + item.QtyShipped); // W1203
                            //qty += Convert.ToInt32(dataTable.Rows[i][dataTable.Columns.IndexOf("W1203")], CultureInfo.InvariantCulture); // running qty total
                            s.Append(elemsep); // W1204
                            s.Append(elemsep + item.W1205); // W1205
                            s.Append(elemsep); // W1206
                            s.Append(elemsep + item.W1207); // W1207
                            s.Append(elemsep + item.W1208); // W1208
                            s.Append(elemsep); // W1209
                            s.Append(elemsep + "2"); // W1210   - weight
                            s.Append(elemsep + "G"); // W1211
                            s.Append(elemsep + "L"); // W1212
                            s.Append(elemsep); // W1213
                            s.Append(elemsep); // W1214
                            s.Append(elemsep); // W1215
                            s.Append(elemsep); // W1216
                            s.Append(elemsep); // W1217
                            s.Append(elemsep); // W1218
                            s.Append(elemsep); // W1219
                            s.Append(elemsep); // W1220
                            s.Append(elemsep + item.W1221); // W1221
                            s.Append(elemsep + item.W1222); // W1222
                            s.Append(segterm);

                            seg++;
                            s.Append("N9"); // N900
                            s.Append(elemsep + "PV"); // N901
                            s.Append(elemsep + item.N902.ToString(CultureInfo.InvariantCulture)); // N902
                            s.Append(segterm);
                        }
                    }


                    seg++;
                    s.Append("W03"); // W300
                    s.Append(elemsep + order.TotalItems.ToString(CultureInfo.InvariantCulture)); // W301
                    s.Append(elemsep + order.TotalWeight.ToString(CultureInfo.InvariantCulture)); // W302
                    s.Append(elemsep + "LB"); // W303
                    s.Append(elemsep); // W304
                    s.Append(elemsep); // W305
                    s.Append(elemsep + order.TotalCartons.ToString(CultureInfo.InvariantCulture)); // W306
                    s.Append(elemsep + "CT"); // W307
                    s.Append(segterm);

                    seg++;
                    s.Append("SE"); // SE00
                    s.Append(elemsep + seg); // SE01
                    s.Append(elemsep +
                             transactionControlNumber.ToString("0000",
                                 CultureInfo.InvariantCulture)); // SE02 same as ST02
                    s.Append(segterm);

                    transactionControlNumber++;
                    completed945s.Add(new Completed945
                    {
                        TransactionSetId = int.Parse(dataTable.Rows[i][dataTable.Columns.IndexOf("TransactionSetId")]
                            .ToString().Trim(), CultureInfo.InvariantCulture),
                        OrderNumber = order.OrderNumber,
                        Filename = filename
                    });
                }

                //pending945s--;
                //END OF 945 Transaction


                s.Append("GE"); // GE00
                s.Append(elemsep + groupCount.ToString(CultureInfo.InvariantCulture)); // GE01
                s.Append(elemsep + groupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GE02 same as GS06
                s.Append(segterm);

                s.Append("IEA"); // IEA00
                s.Append(elemsep + "1"); // IEA01
                s.Append(elemsep +
                         interchangeControlNumber.ToString("000000000",
                             CultureInfo.InvariantCulture)); // IEA02 same as ISA13
                s.Append(segterm);


                Console.WriteLine("Writing: " + pathForExport + filename);
                try
                {
                    File.WriteAllText(pathForExport + filename, s.ToString());
                    #if !DEBUG
                        Completed945.Save(completed945s);
                    #endif
                    
                }
                catch (IOException ex)
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"IO Error reading {filename}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Unauthorized access to {filename}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (SecurityException ex)
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Security error on {filename}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Error saving {filename}");
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
                #if !DEBUG
                    SendEmailNotification(completed945s);
                #endif
                
            }
            if (dataSet.Tables[0].Rows.Count < 500)
                break;
        }
        
        return true;
    }

    private static void SendEmailNotification(List<Completed945> completed945S)
    {
        var smtpClient = new SmtpClient(ConfigurationManager.AppSettings["SMTPServer"],
            int.Parse(ConfigurationManager.AppSettings["SMTPPort"], CultureInfo.InvariantCulture));
       Email
            .From(ConfigurationManager.AppSettings["945SenderFrom"])
            .To(ConfigurationManager.AppSettings["945RecipientTo"])
            .CC(ConfigurationManager.AppSettings["945RecipientCC"])
            .Subject($"945 Notification - {DateTime.Now}")
            .Body($"Sent {completed945S.Count}  945s  at  {DateTime.Now}")
            .UsingClient(smtpClient)
            .Send();
       smtpClient.Dispose();
    }
}