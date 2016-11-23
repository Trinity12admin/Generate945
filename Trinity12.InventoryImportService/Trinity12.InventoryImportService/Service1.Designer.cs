using System.IO;
using System.Threading;
using NLog;

namespace Trinity12.InventoryImportService
{
    partial class Service1
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.FSWatcher = new System.IO.FileSystemWatcher();
            ((System.ComponentModel.ISupportInitialize)(this.FSWatcher)).BeginInit();
            // 
            // FSWatcher
            // 
            this.FSWatcher.EnableRaisingEvents = true;
            this.FSWatcher.Filter = "*.csv";
            this.FSWatcher.NotifyFilter = ((System.IO.NotifyFilters)((((((((System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.DirectoryName) 
            | System.IO.NotifyFilters.Attributes) 
            | System.IO.NotifyFilters.Size) 
            | System.IO.NotifyFilters.LastWrite) 
            | System.IO.NotifyFilters.LastAccess) 
            | System.IO.NotifyFilters.CreationTime) 
            | System.IO.NotifyFilters.Security)));
            // Add event handlers.
            this.FSWatcher.Changed += new System.IO.FileSystemEventHandler(FSWatcher_Changed);
            this.FSWatcher.Created += new System.IO.FileSystemEventHandler(FSWatcher_Created);
            this.FSWatcher.Deleted += new System.IO.FileSystemEventHandler(FSWatcher_Deleted);
            this.FSWatcher.Renamed += new System.IO.RenamedEventHandler(FSWatcher_Renamed);

            // 
            // Service1
            // 
            this.ServiceName = "Service1";
            ((System.ComponentModel.ISupportInitialize)(this.FSWatcher)).EndInit();

        }

        #endregion

        private System.IO.FileSystemWatcher FSWatcher;

        /* DEFINE WATCHER EVENTS... */
        /// <summary>
        /// Event occurs when the contents of a File or Directory are changed
        /// </summary>
        private void FSWatcher_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            //code here for newly changed file or directory
            _logger.Trace($"{e.FullPath} changed");
        }

        /// <summary>
        /// Event occurs when the a File or Directory is created
        /// </summary>
        private void FSWatcher_Created(object sender, System.IO.FileSystemEventArgs e)
        {
            //code here for newly created file or directory
            FileInfo file = new FileInfo(e.FullPath);
            FileStream stream = null;
            bool FileReady = false;

            _logger.Info($"{file.Name} created");

            while (!FileReady)
            {
                try
                {
                    using (stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        FileReady = true;
                    }

                    // file not locked
                    _logger.Trace($"{file.Name} processing");
                    ProcessFile(file);
                }
                catch (IOException)
                {
                    //File isn't ready yet, so we need to keep on waiting until it is.
                }
                //We'll want to wait a bit between polls, if the file isn't ready.
                if (!FileReady) Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Event occurs when the a File or Directory is deleted
        /// </summary>
        private void FSWatcher_Deleted(object sender, System.IO.FileSystemEventArgs e)
        {
            //code here for newly deleted file or directory
            _logger.Trace($"{e.FullPath} deleted");
        }

        /// <summary>
        /// Event occurs when the a File or Directory is renamed
        /// </summary>
        private void FSWatcher_Renamed(object sender, System.IO.RenamedEventArgs e)
        {
            //code here for newly renamed file or directory
            _logger.Trace($"{e.FullPath} renamed");
        }
    }
}
