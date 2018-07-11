using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DropShipShipmentConfirmations.Properties;
using DropShipToolsData;

namespace DropShipShipmentConfirmations
{
    internal static class Program
    {
        private static bool GenerateFlatFileExport(string query, string connectionString, string pathForExport)
        {
            Console.WriteLine(query);

        //    var dbQuery = ConfigurationManager.AppSettings["DBShipmentExportQuery"];
        //    var dbConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];

            using (var dataAdapter = new SqlDataAdapter(query, connectionString))
            {
                dataAdapter.SelectCommand.CommandTimeout = 0; // disable SQL timeout

                using (var dataSet = new DataSet())
                {
                    dataSet.Locale = System.Globalization.CultureInfo.InvariantCulture;

                    dataAdapter.Fill(dataSet);

                    for (int tbl = 0; tbl < dataSet.Tables.Count; tbl++)
                    {
                        using (var dataTable = dataSet.Tables[tbl])
                        {
                            var s = new StringBuilder();
                            string filename = string.Empty;

                            // result set
                            foreach (DataRow dataRow in dataTable.Rows)
                            {
                                /* if filename is blank, this is the first file in this table */
                                if (string.IsNullOrEmpty(filename))
                                {
                                    filename = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim();
                                }

                                /* if filename is non-blank and does not match the filename 
                                 * column of the current row, it means we're moving on to the
                                 * next file and need to write out the previous one. */
                                if ((filename != dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim()) &&
                                    (filename.Length > 0))
                                {
                                    Console.WriteLine(@"Writing: " + pathForExport + filename);
                                    File.WriteAllText(pathForExport + filename, s.ToString());

                                    filename = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim();

                                    s.Clear();
                                }

                                /* We add a newline to the previous line if this isn't the first line.
                                 * This is to prevent ending the file with a newline which could cause
                                 * the last line to be interpreted as a blank row */
                                if (s.Length > 0)
                                {
                                    s.Append(Environment.NewLine); 
                                }

                                /* add the current row to the current file string variable s */
                                s.Append(dataRow[dataTable.Columns.IndexOf("export")].ToString().Replace(Environment.NewLine, " ").Trim());
                            }

                            /* write out the last file from the current table */
                            if ((s.Length > 0) && (filename.Length > 0))
                            {
                                Console.WriteLine(@"Writing: " + pathForExport + filename);
                                File.WriteAllText(pathForExport + filename, s.ToString());
                            }

                            // UTF8 BOM prefix, convert results to byte array
                            //byte[] bom = {0xEF, 0xBB, 0xBF};
                            //byte[] buffer = bom.Concat(Encoding.UTF8.GetBytes(s.ToString())).ToArray();
                        }
                    }
                }
            }
        
            return true;
        }

        private static bool GeneratePO850Export(string query, string connectionString, string pathForExport)
        {
            Console.WriteLine(query);

            //    var dbQuery = ConfigurationManager.AppSettings["DBShipmentExportQuery"];
            //    var dbConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];

            using (var dataAdapter = new SqlDataAdapter(query, connectionString))
            {
                dataAdapter.SelectCommand.CommandTimeout = 0; // disable SQL timeout

                using (var dataSet = new DataSet())
                {
                    dataSet.Locale = System.Globalization.CultureInfo.InvariantCulture;

                    dataAdapter.Fill(dataSet);

                    using (var db = new FUIXWEBEntities())
                    {
                        for (int tbl = 0; tbl < dataSet.Tables.Count; tbl++)
                        {
                            using (var dataTable = dataSet.Tables[tbl])
                            {
                                var s = new StringBuilder();
                                int ln = 0; // PO item line number
                                string filename = string.Empty;
                                const string Segterm = "\n"; // Segment terminator 0x0A
                                const string Elemsep = "|"; // Element seperator

                                // result set
                                foreach (DataRow dataRow in dataTable.Rows)
                                {
                                    string podate_s = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim().Replace(".TXT", string.Empty).Replace("PO_", string.Empty);
                                    DateTime podate_dt = DateTime.ParseExact(podate_s, "yyyyMMdd", CultureInfo.InvariantCulture);

                                    /* if filename does not match the filename 
                                     * column of the current row, it means we're 
                                     * moving on to the next file and need to 
                                     * write out the previous one. */
                                    if (filename != dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim())
                                    {
                                        if (!string.IsNullOrEmpty(filename))
                                        {
                                            Console.WriteLine("Writing: " + pathForExport + filename);
                                            File.WriteAllText(pathForExport + filename, s.ToString());
                                        }

                                        filename = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim();

                                        s.Clear();
                                        ln = 0;

                                        s.Append("ISA"); // ISA00
                                        s.Append(Elemsep + "00"); // ISA01
                                        s.Append(Elemsep + "          "); // ISA02
                                        s.Append(Elemsep + "00"); // ISA03
                                        s.Append(Elemsep + "          "); // ISA04
                                        s.Append(Elemsep + "12"); // ISA05
                                        s.Append(Elemsep + "6366802750     "); // ISA06
                                        s.Append(Elemsep + "12"); // ISA07
                                        s.Append(Elemsep + "6363439914     "); // ISA08
                                        s.Append(Elemsep + DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture)); // ISA09
                                        s.Append(Elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // ISA10
                                        s.Append(Elemsep + "U"); // ISA11
                                        s.Append(Elemsep + "00401"); // ISA12
                                        s.Append(Elemsep + Settings.Default.NextInterchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture)); // ISA13
                                        s.Append(Elemsep + "0"); // ISA14
                                        s.Append(Elemsep + "P"); // ISA15  T=Test P=Production
                                        s.Append(Elemsep + ">"); // ISA16
                                        s.Append(Segterm);

                                        s.Append("GS"); // GS00
                                        s.Append(Elemsep + "PO"); // GS01
                                        s.Append(Elemsep + "6366802750"); // GS02
                                        s.Append(Elemsep + "6363439914"); // GS03
                                        s.Append(Elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // GS04
                                        s.Append(Elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // GS05
                                        s.Append(Elemsep + Settings.Default.NextGroupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GS06
                                        s.Append(Elemsep + "X"); // GS07
                                        s.Append(Elemsep + "004010"); // GS08
                                        s.Append(Segterm);

                                        s.Append("ST"); // ST00
                                        s.Append(Elemsep + "850"); // ST01
                                        s.Append(Elemsep + Settings.Default.NextTransactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // ST02
                                        s.Append(Segterm);

                                        s.Append("BEG"); // BEG00
                                        s.Append(Elemsep + "00"); // BEG01
                                        s.Append(Elemsep + "SA"); // BEG02
                                        s.Append(Elemsep + "FU-DS-" + podate_dt.ToString("yyMMdd", CultureInfo.InvariantCulture)); // BEG03
                                        s.Append(Elemsep); // BEG04
                                        s.Append(Elemsep + podate_dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // BEG05
                                        s.Append(Segterm);

                                        s.Append("DTM"); // DTM00
                                        s.Append(Elemsep + "010"); // DTM01
                                        s.Append(Elemsep + podate_dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // DTM02
                                        s.Append(Segterm);

                                        s.Append("N1"); // N100 bill-to segment
                                        s.Append(Elemsep + "BT"); // N101
                                        s.Append(Elemsep + "TRINITY"); // N102
                                        s.Append(Elemsep + "91"); // N103
                                        s.Append(Elemsep + "314665"); // N104
                                        s.Append(Segterm);

                                        s.Append("N1"); // N100 ship-to segment
                                        s.Append(Elemsep + "ST"); // N101
                                        s.Append(Elemsep + "TRINITY"); // N102
                                        s.Append(Elemsep + "91"); // N103
                                        s.Append(Elemsep + "314665"); // N104
                                        s.Append(Segterm);
                                    }

                                    /* add the current row to the current file string variable s */
                                    s.Append("PO1"); // PO00
                                    s.Append(Elemsep + (++ln)); // PO101
                                    s.Append(Elemsep + dataRow[dataTable.Columns.IndexOf("qty")].ToString().Trim()); // PO102
                                    s.Append(Elemsep + "EA"); // PO103
                                    s.Append(Elemsep + float.Parse(dataRow[dataTable.Columns.IndexOf("cost")].ToString().Trim(), CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.InvariantCulture)); // PO104
                                    s.Append(Elemsep); // PO105
                                    s.Append(Elemsep + "UP"); // PO106
                                    s.Append(Elemsep + dataRow[dataTable.Columns.IndexOf("upc")].ToString().Trim()); // PO107
                                    s.Append(Segterm);

                                    FUI_POImport p = new FUI_POImport
                                    {
                                        Company = "FUI",
                                        PO_ = "FU-DS-" + podate_s,
                                        VendorID = "FUI",
                                        PODate = podate_dt,
                                        ShipDate = podate_dt,
                                        CancelDate = podate_dt.AddMonths(3),
                                        SKU = dataRow[dataTable.Columns.IndexOf("upc")].ToString().Trim(),
                                        Quantity = int.Parse(dataRow[dataTable.Columns.IndexOf("qty")].ToString().Trim()),
                                        UnitCost = decimal.Parse(dataRow[dataTable.Columns.IndexOf("cost")].ToString().Trim()),
                                        Timestamp = DateTime.Now
                                    };

                                    db.FUI_POImport.Add(p);
                                }

                                /* write out the last file from the current table */
                                if ((s.Length > 0) && (filename.Length > 0))
                                {
                                    s.Append("CTT"); // CTT00
                                    s.Append(Elemsep + ln); // CTT01
                                    s.Append(Segterm);

                                    s.Append("SE"); // SE00
                                    s.Append(Elemsep + (ln + 7)); // SE01
                                    s.Append(Elemsep + Settings.Default.NextTransactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // SE02 same as ST02
                                    s.Append(Segterm);

                                    s.Append("GE"); // GE00
                                    s.Append(Elemsep + "1"); // GE01
                                    s.Append(Elemsep + Settings.Default.NextGroupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GE02 same as GS06
                                    s.Append(Segterm);

                                    s.Append("IEA"); // IEA00
                                    s.Append(Elemsep + "1"); // IEA01
                                    s.Append(Elemsep + Settings.Default.NextInterchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture)); // IEA02 same as ISA13
                                    s.Append(Segterm);

                                    Console.WriteLine("Writing: " + pathForExport + filename);
                                    File.WriteAllText(pathForExport + filename, s.ToString());

                                    Settings.Default.NextTransactionControlNumber++;
                                    Settings.Default.NextGroupControlNumber++;
                                    Settings.Default.NextInterchangeControlNumber++;
                                    Settings.Default.Save();
                                }

                                // UTF8 BOM prefix, convert results to byte array
                                //byte[] bom = {0xEF, 0xBB, 0xBF};
                                //byte[] buffer = bom.Concat(Encoding.UTF8.GetBytes(s.ToString())).ToArray();
                            }
                        }

                        db.SaveChanges();
                    }
                }
            }

            return true;
        }

        private static int Main()
        {
            int success = 0;

            // TODO: any kind of error handling at all, papertrail logging

            using (var db = new WMS2Entities())
            {
                // Disable SQL command timeouts
                db.Database.CommandTimeout = 0;

                DateTime dt = DateTime.Now;

                // Have we already run an End of Day today?
                bool hasrun = db.DayEnds
                    .Where(x => System.Data.Entity.DbFunctions.TruncateTime(x.started_dt) == dt.Date)
                    .Any();

                if (hasrun)
                {
                    Console.WriteLine("End of day has already been run today!");
                    return 1;
                }

                // If there is an EOD to eb run today that has not already started, get the ID
                int? ID = db.DayEnds
                    .Where(x => System.Data.Entity.DbFunctions.TruncateTime(x.started_dt) == null)
                    .Where(x => System.Data.Entity.DbFunctions.TruncateTime(x.submit_dt) == dt.Date)
                    .Select(x => x.id)
                    .FirstOrDefault();

                // If the EOD has not run and there is not one to run and it is after 7:30PM, 
                // insert a record and use that as today's EOD
                if (!hasrun && ID == 0 && dt.TimeOfDay > new TimeSpan(hours: 19, minutes: 30, seconds: 0))
                {
                    Console.WriteLine("End of day has not run and it's after 7:30PM. Running automatically.");

                    DayEnd d = new DayEnd
                    {
                        submit_dt = dt,
                        submit_user = "Automatic"
                    };

                    db.DayEnds.Add(d);
                    db.SaveChanges();
                    ID = d.id;
                }

                // If ID is 0 there is no EOD to run
                if (ID == 0)
                {
                    Console.WriteLine("There is no End of Day to run right now.");
                    return 1;
                }

                // Update the started_dt
                DayEnd de = db.DayEnds
                    .Where(x => x.id == ID)
                    .Select(x => x)
                    .First();
                de.started_dt = DateTime.Now;
                db.SaveChanges();

                // Run the EOD shipments update to ensure all data is up-to-date
                Console.WriteLine("Running shipments update. This might take about ten minutes...");
                db.usp_EOD_Shipments_Update();

                // shipments
                success += GenerateFlatFileExport(
                    ConfigurationManager.AppSettings["DBShipmentExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringT12"],
                    ConfigurationManager.AppSettings["PathForShipmentExport"]) ? 0 : 1;

                // inventory
                success += GenerateFlatFileExport(
                    ConfigurationManager.AppSettings["DBInventoryExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForInventoryExport"]) ? 0 : 1;

                // receipts
                success += GenerateFlatFileExport(
                    ConfigurationManager.AppSettings["DBReceiptsExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForReceiptsExport"]) ? 0 : 1;

                // Drop ship PO for FUI
                success += GeneratePO850Export(
                    ConfigurationManager.AppSettings["DBPOExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForPOExport"]) ? 0 : 1;

                // Drop ship shipments
                success += GenerateFlatFileExport(
                    ConfigurationManager.AppSettings["DBPOShipmentExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForShipmentExport"]) ? 0 : 1;
               
                // Update the completed_dt
                de.completed_dt = DateTime.Now;
                db.SaveChanges();
            }            

            return success; // 0 = good, non-zero = bad
        }
    }
}
