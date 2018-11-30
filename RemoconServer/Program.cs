using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace RemoconServer
{
    public class Program
    {
        public static int Port = 9999;
        public static int BufferSize = 4096;

        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://0.0.0.0:{Port}")
                .UseStartup<Startup>();
    }
}
