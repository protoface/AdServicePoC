using MediAdIdentityPoC;
using MediAdIdentityPoC.Transport;

// Has to be the same as the name the service is registered with
const string serviceName = "MediAdIdentityPoC";

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddApplicationInsights();

builder.Services.AddWindowsService(opt => opt.ServiceName = serviceName);
builder.Logging.AddEventLog(opt => opt.SourceName = serviceName);

// Don't use the prod message bus in development  
if (builder.Environment.IsDevelopment())
    // TODO/NOTE: Not implemented. Will fail to start 
    builder.Services.AddSingleton<ITransport, DevelopmentBusService>();
else
    builder.Services.AddSingleton<ITransport, ServiceBusService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();