using System.Threading;
using FUI.Logic;
using NLog;

namespace FUI
{
    class Program
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            _logger.Info("Starting FUI Inventory Update");


            var inventoryLogic = new InventoryLogic();
            inventoryLogic.StartInventoryProcess();

            // 5 seconds to let Papertrail requests catch up
            Thread.Sleep(5000);
        }
    }
}
