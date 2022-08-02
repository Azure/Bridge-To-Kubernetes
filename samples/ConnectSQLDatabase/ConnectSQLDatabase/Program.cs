// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Data.SqlClient;
using System.Threading;
using static ConnectSQLDatabase.SqlUtilities;

namespace ConnectSQLDatabase
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            MakeDbChanges();
            Console.WriteLine("Going to sleep...");
            using (var cancel = new CancellationTokenSource())
            {
                while (!cancel.Token.IsCancellationRequested)
                {
                    Thread.Sleep(10000);
                }
            }
        }

        private static void MakeDbChanges()
        {
            try
            {
                var db = Environment.GetEnvironmentVariable("DB_HOST"); // e.g. "server-bridgetest123.database.windows.net"
                var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
                var dbName = Environment.GetEnvironmentVariable("DB_NAME");

                Console.WriteLine($"DB_HOST = '{db}'");
                Console.WriteLine($"DB_NAME = '{dbName}'");

                var cb = new SqlConnectionStringBuilder
                {
                    DataSource = db,
                    UserID = "sampleLogin",
                    Password = password,
                    InitialCatalog = dbName
                };
                Console.WriteLine($"Connection string: {cb.ConnectionString}");

                using (var connection = new SqlConnection(cb.ConnectionString))
                {
                    connection.Open();

                    Submit_Tsql_NonQuery(connection, "2 - Create-Tables", Build_2_Tsql_CreateTables());

                    Submit_Tsql_NonQuery(connection, "3 - Inserts", Build_3_Tsql_Inserts());

                    Submit_Tsql_NonQuery(connection, "4 - Update-Join", Build_4_Tsql_UpdateJoin(),
                        "@csharpParmDepartmentName", "Accounting");

                    Submit_Tsql_NonQuery(connection, "5 - Delete-Join", Build_5_Tsql_DeleteJoin(),
                        "@csharpParmDepartmentName", "Legal");

                    Submit_6_Tsql_SelectEmployees(connection);
                }
            }
            catch (SqlException e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}