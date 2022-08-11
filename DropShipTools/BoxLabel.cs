using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DropShipShipmentConfirmations
{
    public static class BoxLabel
    {
        public static int GetLabelID()
        {
            int _nextBoxID = 0;
            try
            {
                       
                string SqlConnectionString = "Server=10.3.0.12; User ID=sa; Password=9ure3atEbexe; Database=WMS2";
                using (SqlConnection connection = new SqlConnection(SqlConnectionString)) {
                    connection.Open();
            
                    //GET ORDER HEADER
                    using (SqlCommand command = new SqlCommand("usp_GetNextPackageID", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@QtyToPrint", 1);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _nextBoxID = !reader.IsDBNull(0) ? (int)reader[0] : 2345678;
                            }
                        }
                    }
                }
            }

            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
            }
        
            return _nextBoxID;
        }
    }
}
