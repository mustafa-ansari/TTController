﻿using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using NLog;
using TTController.Service.Config.Data;
using TTController.Service.Manager;
using TTController.Service.Utils;

namespace TTController.Service
{
    static class Program
    {
        private static ServiceController Service => ServiceController.GetServices()
            .FirstOrDefault(s => s.ServiceName.Equals(TTInstaller.ServiceName));

        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                var menu = new MenuPage("Main Menu");
                menu.Add("Manage Service", ManageService, () => true);
                menu.Add("Run in console", () => {
                    Console.Clear();
                    var service = new TTService();
                    service.Initialize();
                    Console.ReadKey(true);
                    service.Dispose(ComputerStateType.Shutdown);
                    Console.WriteLine("Press any key to return to the menu...");
                    Console.ReadKey(true);
                    return false;
                }, () => Service != null && Service.Status != ServiceControllerStatus.Running);
                menu.Add("Show hardware info", () => {
                    ShowInfo();
                    return false;
                }, () => Service != null && Service.Status != ServiceControllerStatus.Running);
                menu.Add("Exit", () => true, () => true, '0');

                while (true)
                {
                    Console.Clear();
                    var selected = menu.Show();
                    if (selected.Callback())
                        return;
                }
            }
            else
            {
                ServiceBase.Run(new TTService());
            }
        }

        private static bool ManageService()
        {
            void StopService()
            {
                Console.WriteLine("Stopping the service...");
                Service?.Stop();
                Service?.WaitForStatus(ServiceControllerStatus.Stopped);
            }

            void StartService()
            {
                Console.WriteLine("Starting the service...");
                Service?.Start();
                Service?.WaitForStatus(ServiceControllerStatus.Running);
            }

            var menu = new MenuPage("Main Menu > Manage Service");
            menu.Add("Start", () =>
            {
                StartService();
                return false;
            }, () => Service != null && Service.Status != ServiceControllerStatus.Running);
            menu.Add("Stop", () =>
            {
                StopService();
                return false;
            }, () => Service != null && Service.Status == ServiceControllerStatus.Running);
            menu.Add("Restart", () => {
                StopService();
                StartService();
                return false;
            }, () => Service != null && Service.Status == ServiceControllerStatus.Running);
            menu.Add("Uninstall", () => {
                if(Service?.Status != ServiceControllerStatus.Stopped)
                    StopService();
                ManagedInstallerClass.InstallHelper(new[]
                    {"/u", "/LogFile=", "/LogToConsole=true", Assembly.GetExecutingAssembly().Location});
                Console.WriteLine("Press any key to return to the menu...");
                Console.ReadKey(true);
                return false;
            }, () => Service != null);
            menu.Add("Install", () => {
                ManagedInstallerClass.InstallHelper(new[]
                    {"/LogFile=", "/LogToConsole=true", Assembly.GetExecutingAssembly().Location});
                Console.WriteLine("Press any key to return to the menu...");
                Console.ReadKey(true);
                return false;
            }, () => Service == null);
            menu.Add("Back", () => true, () => true, '0');
            
            while (true)
            {
                Console.Clear();
                var selected = menu.Show();
                if (selected.Callback())
                    return false;
            }
        }

        private static void ShowInfo()
        {
            LogManager.Configuration = null;
            Console.Clear();
            Console.WriteLine("Controllers");
            Console.WriteLine("-------------------------------");
            using (var deviceManager = new DeviceManager())
            {
                foreach (var controller in deviceManager.Controllers)
                {
                    Console.WriteLine($"Name: {controller.Name}" +
                                      $"\nVendorId: {controller.VendorId}" +
                                      $"\nProductId: {controller.ProductId}");
                    Console.WriteLine($"Ports:");
                    foreach (var port in controller.Ports)
                    {
                        var data = controller.GetPortData(port.Id);
                        Console.WriteLine($"\tId: {port.Id}" +
                                          $"\n\tData: {data}" +
                                          $"\n\tIdentifier: {port}" +
                                          $"\n");
                    }

                    Console.WriteLine($"Available effect types:");
                    Console.WriteLine($"{string.Join(", ", controller.EffectTypes)}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine("Sensors");
            Console.WriteLine("-------------------------------");
            using (var sensorManager = new SensorManager())
            {
                foreach (var hardware in sensorManager.TemperatureSensors.Select(s => s.Hardware).Distinct())
                    hardware.Update();
                
                foreach (var sensor in sensorManager.TemperatureSensors)
                {
                    Console.WriteLine($"{sensor.Hardware.Name}:");
                    Console.WriteLine($"\t{sensor.Identifier}:" +
                                      $"\n\t\tName: {sensor.Name}" + 
                                      $"\n\t\tValue: {sensor.Value ?? float.NaN}" +
                                      $"\t");
                }
            }
            Console.WriteLine("-------------------------------");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        #region Menu
        private class MenuOption
        {
            public MenuOption(string description, Func<bool> callback, Func<bool> enabled, char? keyOverride = null)
            {
                Description = description;
                Callback = callback;
                Enabled = enabled;
                KeyOverride = keyOverride;
            }

            public string Description { get; }
            public Func<bool> Callback { get; }
            public Func<bool> Enabled { get; }
            public char? KeyOverride { get; }
        }

        private class MenuPage
        {
            private readonly List<MenuOption> _options;
            private readonly string _header;

            public void Add(string description, Func<bool> callback, Func<bool> enabled, char? keyOverride = null) =>
                _options.Add(new MenuOption(description, callback, enabled, keyOverride));

            public MenuPage(string header = null)
            {
                _header = header;
                _options = new List<MenuOption>();
            }

            public MenuOption Show()
            {
                if (_header != null)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(_header);
                    Console.WriteLine("================================");
                }

                var index = 0;
                var optionMap = new Dictionary<char, MenuOption>();
                foreach (var option in _options)
                {
                    var key = option.KeyOverride ?? (char) (index++ + (index > 9 ? 'a' : '1'));
                    optionMap.Add(key, option);

                    Console.ForegroundColor = option.Enabled() ? ConsoleColor.Gray : ConsoleColor.DarkGray;
                    Console.WriteLine("[{0}] {1}", key, option.Description);
                }

                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{string.Join(", ", optionMap.Where(kv => kv.Value.Enabled()).Select(kv => kv.Key))}]: ");
                
                while (true)
                {
                    var c = Console.ReadKey(true).KeyChar;
                    if (optionMap.ContainsKey(c) && optionMap[c].Enabled())
                    {
                        Console.ResetColor();
                        Console.WriteLine(c);
                        return optionMap[c];
                    }
                }
            }
        }
        #endregion
    }
}
