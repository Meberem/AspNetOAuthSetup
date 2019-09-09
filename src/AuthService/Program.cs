using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuthService.Seed;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthService
{
    public class Program
    {
        private const string SeedArgument = "seed";
        public static async Task Main(string[] args)
        {
            var seed = args.Any(x => x == SeedArgument);
            if (seed)
            {
                args = args.Except(new[] { SeedArgument }).ToArray();
            }

            var host = CreateWebHostBuilder(args).Build();
            if (seed)
            {
                await host.SeedData();
            }
                
            await host.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
