using DropShipShipmentConfirmations.Properties;
using DropShipToolsData;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DropShipShipmentConfirmations
{
    internal static class Program
    {
        private static bool GenerateFlatFileExport(string query, string connectionString, string pathForExport)
        {
            Console.WriteLine(query);

            // var dbQuery = ConfigurationManager.AppSettings["DBShipmentExportQuery"];
            // var dbConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];

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
                                        s.Append(Elemsep + "001"); // DTM01
                                        s.Append(Elemsep + podate_dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // DTM02
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
                                        Quantity = int.Parse(dataRow[dataTable.Columns.IndexOf("qty")].ToString().Trim(), CultureInfo.InvariantCulture),
                                        UnitCost = decimal.Parse(dataRow[dataTable.Columns.IndexOf("cost")].ToString().Trim(), CultureInfo.InvariantCulture),
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
                                    // TODO: Change ln to be calculated properly instead if using a magic number.
                                    s.Append(Elemsep + (ln + 8)); // SE01
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

        private static bool GenerateShipment945Export(string query, string connectionString, string pathForExport)
        {
            Console.WriteLine(query);

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
                            int ln = 0; // PO item line number
                            int qty = 0; // Total qty shipped per order
                            int seg = 0; // segment line number, from and including ST to SE
                            int bolNumber = 0; // carton ID / BOL number
                            string filename = string.Empty; // current filename
                            const string Segterm = "~"; // Segment terminator 0x0A
                            const string Elemsep = "*"; // Element seperator

                            // result set
                            //foreach (DataRow dataRow in dataTable.Rows)
                            for (int i = 0; i < dataTable.Rows.Count; i++)
                            {
                                /* if filename does not match the filename
                                 * column of the current row, it means we're
                                 * moving on to the next file and need to
                                 * write out the previous one. */
                                if (filename != dataTable.Rows[i][dataTable.Columns.IndexOf("filename")].ToString().Trim())
                                {
                                    if (!string.IsNullOrEmpty(filename))
                                    {
                                        Console.WriteLine("Writing: " + pathForExport + filename);
                                        File.WriteAllText(pathForExport + filename, s.ToString());
                                    }

                                    filename = dataTable.Rows[i][dataTable.Columns.IndexOf("filename")].ToString().Trim();

                                    s.Clear();
                                    ln = 0; // line number counter
                                    qty = 0; // running quantity total
                                    seg = 0; // segment line number
                                    bolNumber = Settings.Default.NextBOLNumber++; // we only want one BOL per shipment

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
                                    s.Append(Elemsep + "SW"); // GS01
                                    s.Append(Elemsep + "6366802750     "); // GS02
                                    s.Append(Elemsep + "6363439914     "); // GS03
                                    s.Append(Elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // GS04
                                    s.Append(Elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // GS05
                                    s.Append(Elemsep + Settings.Default.NextGroupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GS06
                                    s.Append(Elemsep + "X"); // GS07
                                    s.Append(Elemsep + "004010"); // GS08
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("ST"); // ST00
                                    s.Append(Elemsep + "945"); // ST01
                                    s.Append(Elemsep + Settings.Default.NextTransactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // ST02
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("W06"); // W0600
                                    s.Append(Elemsep + "N"); // W0601
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0602")].ToString().Trim()); // W0602
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0603")].ToString().Trim()); // W0603
                                    s.Append(Elemsep + ("6366802750" + Settings.Default.NextBOLNumber.ToString("0000000", CultureInfo.InvariantCulture)).AppendCheckDigit()); // W0604
                                    s.Append(Elemsep); // W0605
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0606")].ToString().Trim()); // W0606
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0607")].ToString().Trim()); // W0607
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0608")].ToString().Trim()); // W0608
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("N1"); // N100 ship-to segment
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST01")].ToString().Trim()); // N101
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST02")].ToString().Trim()); // N102
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST03")].ToString().Trim()); // N103
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST04")].ToString().Trim()); // N104
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("N1"); // N100 ship-from segment
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF01")].ToString().Trim()); // N101
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF02")].ToString().Trim()); // N102
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF03")].ToString().Trim()); // N103
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF04")].ToString().Trim()); // N104
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("G62"); // G6200
                                    s.Append(Elemsep + "10"); // G6201
                                    s.Append(Elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // G6202
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("W27"); // W2700
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2701")].ToString().Trim()); // W2701
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2702")].ToString().Trim()); // W2702
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2703")].ToString().Trim()); // W2703
                                    s.Append(Elemsep + "CC"); // W2704
                                    s.Append(Elemsep); // W2705
                                    s.Append(Elemsep); // W2706
                                    s.Append(Elemsep); // W2707
                                    s.Append(Elemsep + "CC"); // W2708 -- Partial shipments are not currently implemented
                                    s.Append(Elemsep); // W2709
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("LX"); // LX00
                                    s.Append(Elemsep + ++ln); // LX01
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("MAN"); // MAN00
                                    s.Append(Elemsep + "GM"); // MAN01
                                    s.Append(Elemsep + "00006802750" + (bolNumber).ToString("00000000", CultureInfo.InvariantCulture).AppendCheckDigit()); // MAN02
                                    s.Append(Elemsep); // MAN03
                                    s.Append(Elemsep + "CP"); // MAN04
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("MAN05")].ToString().Trim()); // MAN05
                                    s.Append(Segterm);
                                }

                                /* add the current item row data to the current file string variable s */
                                seg++;
                                s.Append("W12"); // W1200
                                s.Append(Elemsep + "SH"); // W1201
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1202")].ToString().Trim()); // W1202
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1203")].ToString().Trim()); // W1203
                                qty += Convert.ToInt32(dataTable.Rows[i][dataTable.Columns.IndexOf("W1203")], CultureInfo.InvariantCulture); // running qty total
                                s.Append(Elemsep); // W1204
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1205")].ToString().Trim()); // W1205
                                s.Append(Elemsep); // W1206
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1207")].ToString().Trim()); // W1207
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1208")].ToString().Trim()); // W1208
                                s.Append(Elemsep); // W1209
                                s.Append(Elemsep + "2"); // W1210
                                s.Append(Elemsep + "G"); // W1211
                                s.Append(Elemsep + "L"); // W1212
                                s.Append(Elemsep); // W1213
                                s.Append(Elemsep); // W1214
                                s.Append(Elemsep); // W1215
                                s.Append(Elemsep); // W1216
                                s.Append(Elemsep); // W1217
                                s.Append(Elemsep); // W1218
                                s.Append(Elemsep); // W1219
                                s.Append(Elemsep); // W1220
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1221")].ToString().Trim()); // W1221
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1222")].ToString().Trim()); // W1222
                                s.Append(Segterm);

                                bool isEof = false;
                                isEof = i == dataTable.Rows.Count - 1;
                                if (!isEof)
                                {
                                    isEof = filename != dataTable.Rows[i + 1][dataTable.Columns.IndexOf("filename")].ToString().Trim();
                                }

                                if (isEof)
                                {
                                    seg++;
                                    s.Append("W03"); // W300
                                    s.Append(Elemsep + qty); // W301
                                    s.Append(Elemsep + (qty * 2)); // W302
                                    s.Append(Elemsep + "LB"); // W303
                                    s.Append(Elemsep); // W304
                                    s.Append(Elemsep); // W305
                                    s.Append(Elemsep + ln); // W306
                                    s.Append(Elemsep + "CT"); // W307
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("SE"); // SE00
                                    s.Append(Elemsep + seg); // SE01
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
                                    Settings.Default.NextBOLNumber++;
                                    Settings.Default.Save();
                                }
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

        private static bool GenerateShipment945ExportWeb(string query, string connectionString, string pathForExport)
        {
            Console.WriteLine(query);

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
                            int ln = 0; // PO item line number
                            int qty = 0; // Total qty shipped per order
                            int seg = 0; // segment line number, from and including ST to SE
                            int bolNumber = 0; // carton ID / BOL number
                            string filename = string.Empty; // current filename
                            string tracknum = string.Empty; // current tracking number/carton number
                            const string Segterm = "\n"; // Segment terminator 0x0A
                            const string Elemsep = "*"; // Element seperator

                            // result set
                            //foreach (DataRow dataRow in dataTable.Rows)
                            for (int i = 0; i < dataTable.Rows.Count; i++)
                            {
                                /* if filename does not match the filename
                                 * column of the current row, it means we're
                                 * moving on to the next file and need to
                                 * write out the previous one. */
                                if (filename != dataTable.Rows[i][dataTable.Columns.IndexOf("filename")].ToString().Trim())
                                {
                                    if (!string.IsNullOrEmpty(filename))
                                    {
                                        Console.WriteLine("Writing: " + pathForExport + filename);
                                        File.WriteAllText(pathForExport + filename, s.ToString());
                                    }

                                    filename = dataTable.Rows[i][dataTable.Columns.IndexOf("filename")].ToString().Trim();

                                    s.Clear();
                                    ln = 0; // line number counter
                                    qty = 0; // running quantity total
                                    seg = 0; // segment line number
                                    bolNumber = Settings.Default.NextBOLNumber++; // we only want one BOL per shipment

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
                                    s.Append(Elemsep + "SW"); // GS01
                                    s.Append(Elemsep + "6366802750     "); // GS02
                                    s.Append(Elemsep + "6363439914     "); // GS03
                                    s.Append(Elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // GS04
                                    s.Append(Elemsep + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture)); // GS05
                                    s.Append(Elemsep + Settings.Default.NextGroupControlNumber.ToString("0", CultureInfo.InvariantCulture)); // GS06
                                    s.Append(Elemsep + "X"); // GS07
                                    s.Append(Elemsep + "004010"); // GS08
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("ST"); // ST00
                                    s.Append(Elemsep + "945"); // ST01
                                    s.Append(Elemsep + Settings.Default.NextTransactionControlNumber.ToString("0000", CultureInfo.InvariantCulture)); // ST02
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("W06"); // W0600
                                    s.Append(Elemsep + "N"); // W0601
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0602")].ToString().Trim()); // W0602
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0603")].ToString().Trim()); // W0603
                                    s.Append(Elemsep + ("6366802750" + Settings.Default.NextBOLNumber.ToString("0000000", CultureInfo.InvariantCulture)).AppendCheckDigit()); // W0604
                                    s.Append(Elemsep); // W0605
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0606")].ToString().Trim()); // W0606
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0607")].ToString().Trim()); // W0607
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W0608")].ToString().Trim()); // W0608
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("N1"); // N100 ship-to segment
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST01")].ToString().Trim()); // N101
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST02")].ToString().Trim()); // N102
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST03")].ToString().Trim()); // N103
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1ST04")].ToString().Trim()); // N104
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("N1"); // N100 ship-from segment
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF01")].ToString().Trim()); // N101
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF02")].ToString().Trim()); // N102
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF03")].ToString().Trim()); // N103
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("N1SF04")].ToString().Trim()); // N104
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("G62"); // G6200
                                    s.Append(Elemsep + "10"); // G6201
                                    s.Append(Elemsep + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // G6202
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("W27"); // W2700
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2701")].ToString().Trim()); // W2701
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2702")].ToString().Trim()); // W2702
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W2703")].ToString().Trim()); // W2703
                                    s.Append(Elemsep + "CC"); // W2704
                                    s.Append(Elemsep); // W2705
                                    s.Append(Elemsep); // W2706
                                    s.Append(Elemsep); // W2707
                                    s.Append(Elemsep + "CC"); // W2708 -- Partial shipments are not currently implemented
                                    s.Append(Elemsep); // W2709
                                    s.Append(Segterm);
                                }

                                /* if we've got a new tracking number, write new LX/MAN segments */
                                /* this ASSUMES the data from SQL is grouped by carton and sorted by tracking number */
                                if (tracknum != dataTable.Rows[i][dataTable.Columns.IndexOf("MAN05")].ToString().Trim())
                                {
                                    bolNumber = Settings.Default.NextBOLNumber++; // new BOL per carton

                                    seg++;
                                    s.Append("LX"); // LX00
                                    s.Append(Elemsep + ++ln); // LX01
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("MAN"); // MAN00
                                    s.Append(Elemsep + "GM"); // MAN01
                                    s.Append(Elemsep + "00006802750" + (bolNumber).ToString("00000000", CultureInfo.InvariantCulture).AppendCheckDigit()); // MAN02
                                    s.Append(Elemsep); // MAN03
                                    s.Append(Elemsep + "CP"); // MAN04
                                    s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("MAN05")].ToString().Trim()); // MAN05
                                    s.Append(Segterm);
                                }

                                tracknum = dataTable.Rows[i][dataTable.Columns.IndexOf("MAN05")].ToString().Trim();

                                /* add the current item row data to the current file string variable s */
                                seg++;
                                s.Append("W12"); // W1200
                                s.Append(Elemsep + "SH"); // W1201
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1202")].ToString().Trim()); // W1202
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1203")].ToString().Trim()); // W1203
                                qty += Convert.ToInt32(dataTable.Rows[i][dataTable.Columns.IndexOf("W1203")], CultureInfo.InvariantCulture); // running qty total
                                s.Append(Elemsep); // W1204
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1205")].ToString().Trim()); // W1205
                                s.Append(Elemsep); // W1206
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1207")].ToString().Trim()); // W1207
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1208")].ToString().Trim()); // W1208
                                s.Append(Elemsep); // W1209
                                s.Append(Elemsep + "2"); // W1210
                                s.Append(Elemsep + "G"); // W1211
                                s.Append(Elemsep + "L"); // W1212
                                s.Append(Elemsep); // W1213
                                s.Append(Elemsep); // W1214
                                s.Append(Elemsep); // W1215
                                s.Append(Elemsep); // W1216
                                s.Append(Elemsep); // W1217
                                s.Append(Elemsep); // W1218
                                s.Append(Elemsep); // W1219
                                s.Append(Elemsep); // W1220
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1221")].ToString().Trim()); // W1221
                                s.Append(Elemsep + dataTable.Rows[i][dataTable.Columns.IndexOf("W1222")].ToString().Trim()); // W1222
                                s.Append(Segterm);

                                bool isEof = false;
                                isEof = i == dataTable.Rows.Count - 1;
                                if (!isEof)
                                {
                                    isEof = filename != dataTable.Rows[i + 1][dataTable.Columns.IndexOf("filename")].ToString().Trim();
                                }

                                if (isEof)
                                {
                                    seg++;
                                    s.Append("W03"); // W300
                                    s.Append(Elemsep + qty); // W301
                                    s.Append(Elemsep + (qty * 2)); // W302
                                    s.Append(Elemsep + "LB"); // W303
                                    s.Append(Elemsep); // W304
                                    s.Append(Elemsep); // W305
                                    s.Append(Elemsep + ln); // W306
                                    s.Append(Elemsep + "CT"); // W307
                                    s.Append(Segterm);

                                    seg++;
                                    s.Append("SE"); // SE00
                                    s.Append(Elemsep + seg); // SE01
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
                                    Settings.Default.NextBOLNumber++;
                                    Settings.Default.Save();
                                }
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
                Console.WriteLine("Running shipments update. This might take a few minutes...");
                db.usp_EOD_Shipments_Update();

                // shipments (flat file)
                //success += GenerateFlatFileExport(
                //    ConfigurationManager.AppSettings["DBShipmentExportQuery"],
                //    ConfigurationManager.AppSettings["DBConnectionStringT12"],
                //    ConfigurationManager.AppSettings["PathForShipmentExport"]) ? 0 : 1;

                // inventory (flat file)
                success += GenerateFlatFileExport(
                    ConfigurationManager.AppSettings["DBInventoryExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForInventoryExport"]) ? 0 : 1;

                // receipts (flat file)
                success += GenerateFlatFileExport(
                    ConfigurationManager.AppSettings["DBReceiptsExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForReceiptsExport"]) ? 0 : 1;

                // Drop ship PO (850) for FUI
                success += GeneratePO850Export(
                    ConfigurationManager.AppSettings["DBPOExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                    ConfigurationManager.AppSettings["PathForPOExport"]) ? 0 : 1;

                
                //// Drop ship shipments (flat file)
                //success += GenerateFlatFileExport(
                //    ConfigurationManager.AppSettings["DBPOShipmentExportQuery"],
                //    ConfigurationManager.AppSettings["DBConnectionStringRBI"],
                //    ConfigurationManager.AppSettings["PathForShipmentExport"]) ? 0 : 1;
                    

                // Drop ship shipments (945)
                success += GenerateShipment945Export(
                    ConfigurationManager.AppSettings["DB945ExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringT12"],
                    ConfigurationManager.AppSettings["PathForShipmentExport"]) ? 0 : 1;

                // Update the completed_dt
                de.completed_dt = DateTime.Now;
                db.SaveChanges();
            }
            
            //// Drop ship shipments (945)
            //success += GenerateShipment945Export(
            //    ConfigurationManager.AppSettings["DB945ExportQuery"],
            //    ConfigurationManager.AppSettings["DBConnectionStringT12"],
            //    ConfigurationManager.AppSettings["PathForShipmentExport"]) ? 0 : 1;

            //// Web cartons(945)
            //success += GenerateShipment945ExportWeb(
            //    ConfigurationManager.AppSettings["DB945ExportQueryWeb"],
            //    ConfigurationManager.AppSettings["DBConnectionStringT12"],
            //    ConfigurationManager.AppSettings["PathForWebExport"]) ? 0 : 1;

            //// Zulily cartons(945)
            //success += GenerateShipment945ExportWeb(
            //    ConfigurationManager.AppSettings["DB945ExportQueryZulily"],
            //    ConfigurationManager.AppSettings["DBConnectionStringT12"],
            //    ConfigurationManager.AppSettings["PathForZulilyExport"]) ? 0 : 1;

            return success; // 0 = good, non-zero = bad
        }
    }
}