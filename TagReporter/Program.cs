using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TagReporter;


var host = Host.CreateDefaultBuilder(args).ConfigureLogging(builder => { builder.AddSimpleConsole(); })
    .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
    .Build();
await host.InitAdminRole();
await host.InitAdminUser();
await host.RunAsync();