using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Npgsql;

class RunSqlScript
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run <sql-file-path>");
            return;
        }

        string sqlFilePath = args[0];
        if (!File.Exists(sqlFilePath))
        {
            Console.WriteLine($"File not found: {sqlFilePath}");
            return;
        }

        // Load connection string from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        string connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("Connection string 'DefaultConnection' not found in appsettings.json");
            return;
        }

        try
        {
            string sql = File.ReadAllText(sqlFilePath);
            
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            Console.WriteLine($"Successfully executed SQL script: {sqlFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing SQL script: {ex.Message}");
        }
    }
} 