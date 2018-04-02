using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DropShipShipmentConfirmations.Properties;
using DropShipToolsData;

namespace DropShipShipmentconfirmations
{
    static class Program
    {
        private static bool GenerateFlatFileExport(string dbQuery, string dbConnectionString, string PathForExport)
        {
            Console.WriteLine(dbQuery);

        //    var dbQuery = ConfigurationManager.AppSettings["DBShipmentExportQuery"];
        //    var dbConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];

            using (var dataAdapter = new SqlDataAdapter(dbQuery, dbConnectionString))
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
                            string filename="";

                            // result set
                            foreach (DataRow dataRow in dataTable.Rows)
                            {
                                /* if filename is blank, this is the first file in this table */
                                if (String.IsNullOrEmpty(filename))
                                {
                                    filename = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim();
                                }

                                /* if filename is non-blank and does not match the filename 
                                 * column of the current row, it means we're moving on to the
                                 * next file and need to write out the previous one. */
                                if ((filename != dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim()) &&
                                    (filename.Length > 0))
                                {
                                    Console.WriteLine("Writing: " + PathForExport + filename);
                                    File.WriteAllText(PathForExport + filename, s.ToString());

                                    filename = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim();

                                    s.Clear();
                                }

                                /* We add a newline to the previous line if this isn't the first line.
                                 * This is to prevent ending the file with a newline which could cause
                                 * the last line to be interpreted as a blank row */
                                if (s.Length>0)
                                {
                                    s.Append(Environment.NewLine); 
                                }

                                /* add the current row to the current file string variable s */
                                s.Append(dataRow[dataTable.Columns.IndexOf("export")].ToString().Replace(Environment.NewLine, " ").Trim());
                                

                                
                            }

                            /* write out the last file from the current table */
                            if ((s.Length > 0) && (filename.Length > 0))
                            {
                                Console.WriteLine("Writing: " + PathForExport + filename);
                                File.WriteAllText(PathForExport + filename, s.ToString());
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Console.WriteLine(System.String)")]
        private static bool GeneratePO850Export(string dbQuery, string dbConnectionString, string PathForExport)
        {
            Console.WriteLine(dbQuery);

            //    var dbQuery = ConfigurationManager.AppSettings["DBShipmentExportQuery"];
            //    var dbConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];

            using (var dataAdapter = new SqlDataAdapter(dbQuery, dbConnectionString))
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
                            int ln = 0; // PO item line number
                            string filename = "";
                            const string segterm = "\n"; // Segment terminator 0x0A
                            const string elemsep = "|"; // Element seperator

                            // result set
                            foreach (DataRow dataRow in dataTable.Rows)
                            {
                                /* if filename does not match the filename 
                                 * column of the current row, it means we're 
                                 * moving on to the next file and need to 
                                 * write out the previous one. */
                                if ((filename != dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim()))
                                {
                                    if (!String.IsNullOrEmpty(filename))
                                    {
                                        Console.WriteLine("Writing: " + PathForExport + filename);
                                        File.WriteAllText(PathForExport + filename, s.ToString());
                                    }

                                    filename = dataRow[dataTable.Columns.IndexOf("filename")].ToString().Trim();

                                    s.Clear();
                                    ln = 0;

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
                                    s.Append(elemsep + Settings.Default.NextInterchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture)); // ISA13
                                    s.Append(elemsep + "0"); // ISA14
                                    s.Append(elemsep + "P"); // ISA15  T=Test P=Production
                                    s.Append(elemsep + ">"); // ISA16
                                    s.Append(segterm);

                                    s.Append("GS"); // GS00
                                    s.Append(elemsep + "PO"); // GS01
                                    s.Append(elemsep + "6366802750"); // GS02
                                    s.Append(elemsep + "6363439914"); // GS03
                                    s.Append(elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // GS04
                                    s.Append(elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // GS05
                                    s.Append(elemsep + Settings.Default.NextGroupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GS06
                                    s.Append(elemsep + "X"); // GS07
                                    s.Append(elemsep + "004010"); // GS08
                                    s.Append(segterm);

                                    s.Append("ST"); // ST00
                                    s.Append(elemsep + "850"); // ST01
                                    s.Append(elemsep + Settings.Default.NextTransactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // ST02
                                    s.Append(segterm);

                                    s.Append("BEG"); // BEG00
                                    s.Append(elemsep + "00"); // BEG01
                                    s.Append(elemsep + "SA"); // BEG02
                                    s.Append(elemsep + "FU-DS-" + DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture)); // BEG03
                                    s.Append(elemsep); // BEG04
                                    s.Append(elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // BEG05
                                    s.Append(segterm);

                                    s.Append("DTM"); // DTM00
                                    s.Append(elemsep + "010"); // DTM01
                                    s.Append(elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // DTM02
                                    s.Append(segterm);

                                    s.Append("N1"); // N100 bill-to segment
                                    s.Append(elemsep + "BT"); // N101
                                    s.Append(elemsep + "TRINITY"); // N102
                                    s.Append(elemsep + "91"); // N103
                                    s.Append(elemsep + "314665"); // N104
                                    s.Append(segterm);

                                    s.Append("N1"); // N100 ship-to segment
                                    s.Append(elemsep + "ST"); // N101
                                    s.Append(elemsep + "TRINITY"); // N102
                                    s.Append(elemsep + "91"); // N103
                                    s.Append(elemsep + "314665"); // N104
                                    s.Append(segterm);
                                }


                                /* add the current row to the current file string variable s */
                                s.Append("PO1"); // PO00
                                s.Append(elemsep + (++ln)); // PO101
                                s.Append(elemsep +  dataRow[dataTable.Columns.IndexOf("qty")].ToString().Trim()); // PO102
                                s.Append(elemsep + "EA"); // PO103
                                s.Append(elemsep + float.Parse(dataRow[dataTable.Columns.IndexOf("cost")].ToString().Trim(), CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.InvariantCulture)); // PO104
                                s.Append(elemsep); // PO105
                                s.Append(elemsep + "UP"); // PO106
                                s.Append(elemsep + dataRow[dataTable.Columns.IndexOf("upc")].ToString().Trim()); // PO107
                                s.Append(segterm);
                            }

                            /* write out the last file from the current table */
                            if ((s.Length > 0) && (filename.Length > 0))
                            {
                                s.Append("CTT"); // CTT00
                                s.Append(elemsep + ln); // CTT01
                                s.Append(segterm);

                                s.Append("SE"); // SE00
                                s.Append(elemsep + (ln + 7)); // SE01
                                s.Append(elemsep + Settings.Default.NextTransactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // SE02 same as ST02
                                s.Append(segterm);

                                s.Append("GE"); // GE00
                                s.Append(elemsep + "1"); // GE01
                                s.Append(elemsep + Settings.Default.NextGroupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GE02 same as GS06
                                s.Append(segterm);

                                s.Append("IEA"); // IEA00
                                s.Append(elemsep + "1"); // IEA01
                                s.Append(elemsep + Settings.Default.NextInterchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture)); // IEA02 same as ISA13
                                s.Append(segterm);

                                Console.WriteLine("Writing: " + PathForExport + filename);
                                File.WriteAllText(PathForExport + filename, s.ToString());

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
                }
            }

            return true;
        }

        private static int Main()
        {
            int success = 0;

            // TODO: any kind of error handling at all
            // TODO: papertrail logging

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
                if (!hasrun && ID == 0 && dt.TimeOfDay>new TimeSpan(hours: 19, minutes: 30, seconds: 0))
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

