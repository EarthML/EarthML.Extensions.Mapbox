using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace EarthML.Extensions.Mapbox.UploadCLI
{
    public class App : System.CommandLine.RootCommand
    {

        public App(ILogger<App> logger, IEnumerable<Command> commands)
        {
            foreach (var command in commands)
                Add(command);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }

        public async Task Run(ParseResult parseResult, IConsole console) //(string path, string customizationprefix)
        {

        }
        
    }


    internal class Program
    {
        static ServiceCollection ConfigureServices(ServiceCollection serviceCollection)
        {

          IConfiguration config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            serviceCollection
               .AddLogging(configure =>
               {
                   configure.SetMinimumLevel(LogLevel.Debug);
                   configure.AddDebug();
                   configure.AddConsole();
               })
               .AddSingleton(config)
               .AddSingleton<App>();

           
            serviceCollection.AddSingleton<Command, UploadCommand>();
 
            serviceCollection.AddHttpClient();
            return serviceCollection;

        }
        public static async Task<int> Main(string[] args)
        {

            using var services = ConfigureServices(new ServiceCollection()).BuildServiceProvider();

            var result = await services.GetRequiredService<App>().InvokeAsync(args);

            return result;
        }
    }
}