using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace DropShipShipmentConfirmations;

public class EDIControlNumbers
{
    public int InterchangeControlNumber { get; }
    public int GroupControlNumber { get; }
    public int TransactionControlNumber { get; }

    public int BOLNumber { get; }

    public static int LabelID()
    {
        int nextBoxId = 0;
        string sqlConnectionString = ConfigurationManager.AppSettings["DBConnectionStringRBI"];
        using var connection = new SqlConnection(sqlConnectionString);

        try
        {
            nextBoxId = connection.QueryFirst<int>($"usp_GetNextPackageID 1");
        }
        catch (SqlException e)
        {
            Console.WriteLine(e.ToString());
        }

        return nextBoxId;
    }

    public static EDIControlNumbers NextControlNumbers(int transactionCount)
    {
        var controlNumbers = new EDIControlNumbers();
        string sqlConnectionString = ConfigurationManager.AppSettings["DBConnectionStringT12"];
        using var connection = new SqlConnection(sqlConnectionString);
        try
        {
            controlNumbers =
                connection.QueryFirst<EDIControlNumbers>($"exec dbo.NextEDIControlSet {transactionCount}");
        }
        catch
        {
        }

        return controlNumbers;
    }
}