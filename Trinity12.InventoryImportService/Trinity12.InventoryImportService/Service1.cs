using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using NLog;

namespace Trinity12.InventoryImportService
{
    public partial class Service1 : ServiceBase
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: add code to start service
            _logger.Info("service started");
            FSWatcher.Path = ConfigurationManager.AppSettings["WatchPath"];
        }

        protected override void OnStop()
        {
            _logger.Info("service stopped");
        }

        private void ProcessFile(FileInfo file)
        {
            if (file.Name.Equals("casepacks_all.csv"))
            {
                // run casepack process
                _logger.Trace($"{file.Name} determined to be casepack file");
                ImportCasepacks(file);
            }
            else if (Regex.IsMatch(file.Name, @"^Inventory \d{4}-\d{1,2}-\d{1,2}\.csv"))
            {
                // run inventory import
                _logger.Trace($"{file.Name} determined to be inventory file");
                ImportInventory(file, "FUI");
            }
            else if (Regex.IsMatch(file.Name, @"^OtherInventory \d{4}-\d{1,2}-\d{1,2}\.csv"))
            {
                // run other inventory import   
                _logger.Trace($"{file.Name} determined to be other inventory file");
                ImportInventory(file, "FUIA");
            }
            else
            {
                _logger.Info($"{file.Name} does not match any existing filters");
            }
        }

        private void ImportInventory(FileInfo file, string warehouse)
        {
            FileInfo transitionFile = new FileInfo(Path.Combine(ConfigurationManager.AppSettings["TransitionPath"], file.Name));
            FileInfo archiveFile = new FileInfo(Path.Combine(ConfigurationManager.AppSettings["ArchivePath"], file.Name));

            // move to staging folder
            _logger.Info($"moving {file.Name} to transition folder");
            File.Move(file.FullName, transitionFile.FullName);

            // run stored proc on TrinityCatalog
            try
            {
                _logger.Trace($"attempting to pass {file.Name} to po.usp_ImportInventory");
                var connString = System.Configuration.ConfigurationManager.ConnectionStrings["TrinityCatalog"].ConnectionString;

                using (var conn = new SqlConnection(connString))
                using (var command = new SqlCommand("po.usp_ImportInventory", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@csv", SqlDbType.NVarChar)).Value = transitionFile.FullName;
                    command.Parameters.Add(new SqlParameter("@warehouse", SqlDbType.NVarChar)).Value = warehouse;

                    conn.Open();
                    command.ExecuteNonQuery();
                }

                _logger.Info($"{file.Name} successfully imported");
            }
            catch (Exception e)
            {
                _logger.Error($"error executing po.usp_ImportInventory: {e.Message}");
            }

            // move to archive
            _logger.Info($"moving {file.Name} to archive folder");
            File.Move(transitionFile.FullName, archiveFile.FullName);
        }

        private void ImportCasepacks(FileInfo file)
        {
            FileInfo transitionFile = new FileInfo(Path.Combine(ConfigurationManager.AppSettings["TransitionPath"], file.Name));
            FileInfo archiveFile = new FileInfo(Path.Combine(ConfigurationManager.AppSettings["ArchivePath"], file.Name));

            // move to staging folder
            _logger.Info($"moving {file.Name} to transition folder");
            File.Move(file.FullName, transitionFile.FullName);

            // run stored proc on TrinityCatalog
            try
            {
                _logger.Trace($"attempting to pass {file.Name} to po.usp_ImportCasepacks");
                var connString = System.Configuration.ConfigurationManager.ConnectionStrings["TrinityCatalog"].ConnectionString;

                using (var conn = new SqlConnection(connString))
                using (var command = new SqlCommand("po.usp_ImportCasepacks", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@csv", SqlDbType.NVarChar)).Value = transitionFile.FullName;

                    conn.Open();
                    command.ExecuteNonQuery();
                }

                _logger.Info($"{file.Name} successfully imported");
            }
            catch (Exception e)
            {
                _logger.Error($"error executing po.usp_ImportCasepacks: {e.Message}");
            }

            // move to archive
            _logger.Info($"moving {file.Name} to archive folder");
            File.Move(transitionFile.FullName, archiveFile.FullName);
        }
    }
}
