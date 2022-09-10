using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using Dapper;

namespace DropShipShipmentConfirmations;

public class Completed945
{
    public int TransactionSetId { get; set; }
    public string OrderNumber { get; set; }
    public string Filename { get; set; }
    public DateTime ProcessedDate { get; set; } = DateTime.Now;


    public static bool Save(List<Completed945> completed945s)
    {
        var connectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];
        using var connection = new SqlConnection(connectionString);

        try
        {
            connection.Execute("insert into edi.dbo.EDI945Complete values (@TransactionSetId, @OrderNumber, @FileName, @ProcessedDate)", completed945s );
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return true;
    }

    public static bool Clear(string orderNumber)
    {
        var connectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];
        using var connection = new SqlConnection(connectionString);

        try
        {
            connection.Execute($"delete from EDI945Complete where ordernumber = '{orderNumber}'");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return true;
    }
}