using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Tsintra.Persistence
{
    /// <summary>
    /// Клас для запуску міграцій бази даних з консольних програм або тестів
    /// </summary>
    public static class RunDbMigrations
    {
        /// <summary>
        /// Запускає міграції з використанням вказаного файлу конфігурації
        /// </summary>
        /// <param name="configPath">Шлях до файлу конфігурації</param>
        /// <returns>Результат виконання міграцій: true - успішно, false - помилка</returns>
        public static bool RunMigrations(string configPath = "appsettings.json")
        {
            // Перевірка наявності файлу
            if (!File.Exists(configPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Помилка: Файл конфігурації '{configPath}' не знайдено");
                Console.ResetColor();
                
                string[] possiblePaths = {
                    Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Tsintra.Api", "appsettings.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Tsintra.WebApi", "appsettings.json")
                };
                
                Console.WriteLine("Пошук доступних файлів конфігурації...");
                
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Знайдено: {path}");
                        Console.ResetColor();
                        
                        Console.WriteLine($"Спробуйте запустити з цим шляхом: RunDbMigrations.RunMigrations(\"{path}\")");
                    }
                }
                
                return false;
            }

            try
            {
                // Створення конфігурації
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory())
                    .AddJsonFile(Path.GetFileName(configPath), optional: false, reloadOnChange: true)
                    .Build();

                // Налаштування логера
                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                var logger = loggerFactory.CreateLogger("DbMigrations");

                logger.LogInformation("Запуск міграцій з використанням конфігурації: {ConfigPath}", configPath);

                // Виконання міграцій
                return DependencyInjection.RunMigrations(configuration, logger);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Помилка при запуску міграцій: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Внутрішня помилка: {ex.InnerException.Message}");
                }
                Console.ResetColor();
                return false;
            }
        }
    }
} 