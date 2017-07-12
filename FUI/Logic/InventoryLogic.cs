using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Dapper;
using DECK.Core.Common.Contracts.API;
using Newtonsoft.Json;
using NLog;


namespace FUI.Logic
{
    public class InventoryLogic
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Start the inventory process. This call will do all of the inventory processing.
        /// </summary>
        public void StartInventoryProcess()
        {
            _logger.Info("Starting Inventory Process.");

            var gpConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["GpConnection"].ConnectionString);

            _logger.Debug("Gathering Inventory.");
            List<GpInventory> inventory = GetInventoryToImport(gpConnection);

            _logger.Debug("Zipping Inventory File");
            string inventoryZip = InventoryToZipFile(inventory);

            string apiUrl = ConfigurationManager.AppSettings["WebApiURL"];
            string apiKey = ConfigurationManager.AppSettings["ApiKey"];
            string siteCode = ConfigurationManager.AppSettings["SiteCode"];
            string inventoryExternalName = ConfigurationManager.AppSettings["InventoryExternalName"];

            _logger.Debug("Clearing PIM Inventory.");
            ClearInventoryApi(apiUrl, apiKey, siteCode);
            _logger.Debug("Loading file: " + inventoryZip);
            LoadInventoryApi(apiUrl, apiKey, inventoryZip, siteCode);
            ProcessInventoryApi(apiUrl, apiKey, siteCode, inventoryExternalName);

            Cleanup();

            _logger.Info("Finished Inventory Process.");

            //Time to read output before closing.
            Console.WriteLine("Sleeping for 10 seconds before closing.");
            Thread.Sleep(10000);
        }

        /// <summary>
        ///     get the inventory to import from the source system.
        /// </summary>
        /// <param name="gpConnection"></param>
        /// <returns></returns>
        private List<GpInventory> GetInventoryToImport(SqlConnection gpConnection)
        {
            List<GpInventory> inventory = null;

            try
            {
                using (gpConnection)
                {
                    gpConnection.Open();
                    inventory =
                        gpConnection.Query<GpInventory>("SELECT * FROM dbo.vGetInventoryDetails",
                            commandType: CommandType.Text).ToList();
                }
            }
            catch (Exception e)
            {
                _logger.Error("FUI.Logic.InventoryLogic GetInventoryToImport", e);
                throw;
            }

            return inventory;
        }

        /// <summary>
        /// Convert the inventory into a zipped CSV file for import.
        /// </summary>
        /// <param name="inventory">Inventory records to import.</param>
        /// <returns>File location of the zip file to import.</returns>
        internal string InventoryToZipFile(List<GpInventory> inventory)
        {
            string filePathCsv = "";
            string filePathZip = "";
            string csvFileName = "";

            //string basePath = Directory.GetCurrentDirectory().TrimEnd('\\') + @"\InventoryFiles\";
            string basePath = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + @"\InventoryFiles\";

            try
            {
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);
            }
            catch (Exception exc)
            {
                _logger.Error("Can't create the directory: " + basePath, exc);
            }

            DateTime now = DateTime.Now;

            filePathCsv = basePath + now.Ticks + ".csv";
            filePathZip = basePath + now.Ticks + ".zip";
            csvFileName = now.Ticks + ".csv";

            try
            {
                using (var sw = new StreamWriter(filePathCsv, false))
                {
                    foreach (GpInventory inv in inventory)
                    {
                        sw.WriteLine(inv.GTIN.Trim() + "," + inv.Style.Trim() + "," + inv.Size.Trim() + "," + inv.Width.Trim() + "," + inv.Sku.Trim() + "," + inv.Qty_available + "," + inv.Description);
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.Error("Issue writing inventory to csv file: " + filePathCsv, exc);
            }

            try
            {
                using (var fs = new FileStream(filePathZip, FileMode.CreateNew))
                {
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, true))
                    {
                        zip.CreateEntryFromFile(filePathCsv, csvFileName, CompressionLevel.Optimal);
                        Console.WriteLine("Zip file created.");
                    }
                }
            }
            catch (Exception exc)
            {
                _logger.Error("Issue writing zip file:" + filePathZip, exc);
            }

            try
            {
                File.Delete(filePathCsv);
                Console.WriteLine("CSV file deleted.");
            }
            catch (Exception exc)
            {
                _logger.Error("Issue deleting csv file:" + filePathCsv, exc);
            }

            return filePathZip;
        }

        /// <summary>
        ///     Call the clear inventory API so we can start fresh.
        /// </summary>
        private void ClearInventoryApi(string apiUrl, string apiKey, string siteCode)
        {
            string currentUtc = DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToShortTimeString();
            string newVerificationKey = GetSha1Hash("api/PimInventoryClearForImport".ToLower() + currentUtc + apiKey);

            var clearRequest = new
            {
                SiteCode = siteCode,
                TimestampUTC = currentUtc,
                VerificationKey = newVerificationKey
            };

            string json = JsonConvert.SerializeObject(clearRequest);
            string responseString = WebRequestHelper(apiUrl + "api/PimInventoryClearForImport", "POST",
                "application/json", "application/json", json);

            try
            {
                var results = JsonConvert.DeserializeObject<GenericResponse>(json);

                Console.WriteLine("ClearInventoryResult: " + results.ResponseCode);
                Console.WriteLine("                      " + results.Message);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Can't Clear Inventory" + exc.Message);
                _logger.Error("Can't Clear Inventory", exc);
                throw;
            }

            Console.WriteLine("Finished Clear: " + responseString);
        }

        /// <summary>
        ///     Send the Inventory file to the service.
        /// </summary>
        /// <param name="apiUrl">API URL</param>
        /// <param name="apiKey">API key</param>
        /// <param name="fileName">Full name/path of the .zip file to send.</param>
        /// <param name="siteCode">site code</param>
        private void LoadInventoryApi(string apiUrl, string apiKey, string fileName, string siteCode)
        {
            string currentUtc = DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToShortTimeString();
            string newVerificationKey = GetSha1Hash("api/PimInventoryLoadFile".ToLower() + currentUtc + apiKey);

            // Read file data
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var rawFileName = fs.Name;
            var data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Close();

            // Generate post objects
            var postParameters = new Dictionary<string, object>();
            postParameters.Add("TimestampUTC", currentUtc);
            postParameters.Add("SiteCode", siteCode);
            postParameters.Add("VerificationKey", newVerificationKey);
            postParameters.Add("ImportFile ", new FormUpload.FileParameter(data, rawFileName, "application/zip"));

            // Create request and receive response
            string postURL = apiUrl + "api/PimInventoryLoadFile";
            string userAgent = "";
            HttpWebResponse webResponse = FormUpload.MultipartFormDataPost(postURL, userAgent, postParameters);


            string fullResponse = "";
            try
            {
                Console.WriteLine("postURL: " + postURL);
                // Process response
                var responseReader = new StreamReader(webResponse.GetResponseStream());
                fullResponse = responseReader.ReadToEnd();
                webResponse.Close();

                var results = JsonConvert.DeserializeObject<GenericResponse>(fullResponse);

                Console.WriteLine("LoadInventoryApiResult: " + results.ResponseCode);
                Console.WriteLine("                      " + results.Message);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Error in LoadInventoryApi" + exc.Message);
                _logger.Error("Error in LoadInventoryApi", exc);
                throw;
            }

            Console.WriteLine("Finished LoadInventoryApi: " + fullResponse);
        }

        /// <summary>
        ///     Tell the system to do the import of the data.
        /// </summary>
        /// <param name="apiUrl">API URL</param>
        /// <param name="apiKey">Api Key</param>
        /// <param name="siteCode">site code</param>
        /// <param name="externalName">inventory name</param>
        private void ProcessInventoryApi(string apiUrl, string apiKey, string siteCode, string externalName)
        {
            string currentUtc = DateTime.UtcNow.ToShortDateString() + " " + DateTime.UtcNow.ToShortTimeString();
            string newVerificationKey =
                GetSha1Hash("api/PimInventoryTransformMergeImport".ToLower() + currentUtc + apiKey);

            var clearRequest = new
            {
                SiteCode = siteCode,
                TimestampUTC = currentUtc,
                VerificationKey = newVerificationKey,
                ExternalName = externalName
            };

            Console.WriteLine("Calling web api: " + apiUrl);
            string json = JsonConvert.SerializeObject(clearRequest);
            string responseString = WebRequestHelper(apiUrl + "api/PimInventoryTransformMergeImport", "POST",
                "application/json", "application/json", json);
            Console.WriteLine("Web api response: " + responseString);

            try
            {
                var results = JsonConvert.DeserializeObject<GenericResponse>(responseString);

                Console.WriteLine("PimInventoryTransformMergeImportResult: " + results.ResponseCode);
                Console.WriteLine("                                         " + results.Message);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Error in PimInventoryTransformMergeImport" + exc.Message);
                _logger.Error("Error in PimInventoryTransformMergeImport", exc);
                throw;
            }

            Console.WriteLine("Finished ProcessInventoryApi(): " + responseString);
        }

        /// <summary>
        ///     Clean up files that are not used.
        /// </summary>
        internal void Cleanup()
        {
            try
            {
                foreach (
                    string file in
                        Directory.GetFiles(System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + @"\InventoryFiles\"))
                        //Directory.GetFiles(Directory.GetCurrentDirectory().TrimEnd('\\') + @"\InventoryFiles\"))
                {
                    var fi = new FileInfo(file);

                    if (fi.CreationTime < DateTime.Now.AddDays(-15) &&
                        (fi.Extension.ToLower() == ".zip" || fi.Extension.ToLower() == ".csv"))
                    {
                        fi.Delete();
                    }
                }
                _logger.Info("Inventory processing cleanup complete.");
            }
            catch (Exception exc)
            {
                _logger.Error("Problem deleting a file: " + exc.Message);
            }
            Console.WriteLine("Finished file cleanup.");
        }

        /// <summary>
        ///     Shared method to send the request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="verb"></param>
        /// <param name="content"></param>
        /// <param name="accept"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private string WebRequestHelper(string url, string verb, string content, string accept, string data)
        {
            try
            {
                var myReq = (HttpWebRequest)WebRequest.Create(url);
                myReq.ContentType = content;
                myReq.Timeout = 1000 * 60 * 30; //30 minutes

                if (accept != "")
                {
                    myReq.Accept = accept;
                }

                myReq.Method = verb;

                if (data != "")
                {
                    using (var sw = new StreamWriter(myReq.GetRequestStream()))
                    {
                        sw.Write(data);
                    }
                }

                WebResponse myResp = myReq.GetResponse();
                string responseString = string.Empty;
                if (myResp.GetResponseStream() != null)
                {
                    var htmlStream = new StreamReader(myResp.GetResponseStream());
                    responseString = htmlStream.ReadToEnd();
                    htmlStream.Close();
                    htmlStream = null;
                }
                return responseString;
            }
            catch (WebException webEx)
            {
                _logger.Error("Inventory Import Plugin WebRequestHelper webEx: " + url, webEx);

                //try to get error details
                using (StreamReader sr = new StreamReader(webEx.Response.GetResponseStream()))
                {
                    string errorData = sr.ReadToEnd();
                    _logger.Debug(errorData);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error("Inventory Import Plugin WebRequestHelper: " + url, ex);
                return null;
            }
        }

        /// <summary>
        ///     Calculate a hash.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string GetSha1Hash(string input)
        {
            SHA256 sha1 = new SHA256Managed();
            string encodedValue = string.Empty;

            try
            {
                Byte[] originalBytes = Encoding.Default.GetBytes(input);
                Byte[] encodedBytes = sha1.ComputeHash(originalBytes);
                encodedValue = BitConverter.ToString(encodedBytes).Replace("-", "");
            }
            catch
            {
            }
            finally
            {
                sha1.Dispose();
            }

            return encodedValue;
        }
    }
}