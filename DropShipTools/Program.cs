using System;
using System.Configuration;

namespace DropShipShipmentConfirmations;

public static class Program
{
    public static int Main(string[]? args)
    {
        int success = 0;

        if (args is { Length: 0 })
        {
            try
            {
                success += Export945Shipment.Generate(
                    ConfigurationManager.AppSettings["DB945ExportQuery"],
                    ConfigurationManager.AppSettings["DBConnectionStringT12"],
                    ConfigurationManager.AppSettings["PathForShipmentExport"])
                    ? 0
                    : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                success = 1;
            }

            return success;
        }

        if (args != null)
            foreach (string arg in args)
            {
                string newArg = arg.Replace("'", "").Replace("\"", "").Replace("\r", " ")
                    .Replace("\n", ","); //Remove ticks and question marks
                string[] orders = newArg.Split(',');

                foreach (string order in orders)
                    try
                    {
#if !DEBUG
                        Completed945.Clear(order);
#endif

                        Console.WriteLine($"Processing 945 for Order: {order.Trim()}");
                        success += Export945Shipment.Generate(
                            $"EXEC [dbo].[Drop_Ship_945_Export_SingleOrder] '{order.Trim()}'",
                            ConfigurationManager.AppSettings["DBConnectionStringT12"],
                            ConfigurationManager.AppSettings["PathForShipmentExport"])
                            ? 0
                            : 1;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        success = 1;
                    }
            }


        return success; // 0 = good, non-zero = bad
    }
}