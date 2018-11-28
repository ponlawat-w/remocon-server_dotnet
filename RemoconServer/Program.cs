using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RemoconServer
{
    public class Program
    {
        public static int SenderPort = 9010;
        public static int ReceiverPort = 9009;
        public static int BufferSize = 1024;

        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://localhost:{SenderPort}", $"http://localhost:{ReceiverPort}")
                .UseStartup<Startup>();
    }
}
