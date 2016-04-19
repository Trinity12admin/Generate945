using log4net;
using log4net.Config;
using FUI.Logic;

namespace FUI
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            //Configure Log4Net
            XmlConfigurator.Configure();
            var inventoryLogic = new InventoryLogic();
            inventoryLogic.StartInventoryProcess();
        }
    }
}
