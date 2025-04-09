using MediAdIdentityPoC;
using MediAdIdentityPoC.Transport;

const string serviceName = "MediAdIdentityPoC";

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddApplicationInsights();

builder.Services.AddWindowsService(opt => opt.ServiceName = serviceName);
builder.Logging.AddEventLog(opt => opt.SourceName = serviceName);

if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<ITransport, DevelopmentBusService>();
else
    builder.Services.AddSingleton<ITransport, ServiceBusService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();