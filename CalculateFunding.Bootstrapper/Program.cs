﻿using System;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Repositories.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CalculateFunding.Bootstrapper
{
    class Program
    {
        public static IConfigurationRoot Configuration { get; set; }
        static int Main(string[] args)
        {
            Console.WriteLine($"Args:{string.Join(", ", args ?? new string[0])}");
            var builder = new ConfigurationBuilder();

            builder       
                .AddJsonFile("appsettings.json")
                .AddCommandLine(args);

            Configuration = builder.Build();


            var cosmosDbConnectionString = Configuration["cosmosDbConnectionString"];
            var searchServiceName = Configuration["searchServiceName"];
            var searchPrimaryKey = Configuration["searchPrimaryKey"];
            var providersConnectionString = Configuration["providersConnectionString"];

            bool isValid = true;
            //if (string.IsNullOrWhiteSpace(cosmosDbConnectionString))
            //{
            //    Console.WriteLine("cosmosDbConnectionString must be provided");
            //    isValid = false;
            //}
            //if (string.IsNullOrWhiteSpace(searchServiceName))
            //{
            //    Console.WriteLine("searchServiceName must be specified");
            //    isValid = false;
            //}
            //if (string.IsNullOrWhiteSpace(searchPrimaryKey))
            //{
            //    Console.WriteLine("searchPrimaryKey must be specified");
            //    isValid = false;
            //}
            if (string.IsNullOrWhiteSpace(providersConnectionString))
            {
                Console.WriteLine("providersConnectionString must be specified");
                isValid = false;
            }

            if (isValid)
            {
                //Console.WriteLine($"Started with {args}");
                try
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            string externalip = new WebClient().DownloadString("http://icanhazip.com");
                            Console.WriteLine(externalip);

                            var serviceProvider = new ServiceCollection()
                                .AddSingleton(new LoggerFactory()
                                    .AddConsole())
                                .AddLogging()
                                .AddDbContext<ProvidersDbContext>(options =>
                                    options.UseSqlServer(providersConnectionString, b => b.MigrationsAssembly("CalculateFunding.Repositories.Providers.Migrations"))).BuildServiceProvider();
                            var providerDbContext = serviceProvider.GetService<ProvidersDbContext>();

                            Console.WriteLine($"Applying migrations for {providersConnectionString}");
                            await providerDbContext.Database.MigrateAsync();
                            Console.WriteLine($"Applied migrations to Providers");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        //try
                        //{
                        //    var searchInitializer = new SearchInitializer(searchServiceName,
                        //        searchPrimaryKey, cosmosDbConnectionString);

                        //    await searchInitializer.Initialise(typeof(ProductTestScenarioResultIndex));
                        //    await searchInitializer.Initialise(typeof(ProviderResultIndex));
                        //}
                        //catch (Exception e)
                        //{

                        //    Console.WriteLine(e);
                        //    throw;
                        //}

                        //Console.WriteLine("Seed budget");
                        //try
                        //{
                        //    var settings = new RepositorySettings
                        //    {
                        //        ConnectionString = cosmosDbConnectionString,
                        //        DatabaseName = "calculate-funding",
                        //        CollectionName = "specs"
                        //    };

                        //    var logger = new LoggerFactory()
                        //        .AddConsole()
                        //        .CreateLogger("Bootstrapper");
                        //    var repo = new Repository<Budget>(settings, logger);
                            
                        //    await repo.CreateAsync(SeedData.CreateGeneralAnnualGrant());
                            
                        //}
                        //catch (Exception e)
                        //{
                        //    Console.Write(e.ToString());
                        //}

                    }).Wait();
                    Console.WriteLine("Completed successfully");
                    return 0;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            return -1;
            /*
             */

        }


    }
}
