using MediAdIdentityPoC;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddApplicationInsights();

builder.Services.AddWindowsService(opt => opt.ServiceName = "MediAdIdentityPoC");
builder.Logging.AddEventLog(opt => opt.SourceName = "MediAdIdentityPoC");

builder.Services.AddSingleton<ServiceBusService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();