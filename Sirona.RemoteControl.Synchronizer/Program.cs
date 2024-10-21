using System.Net;

using Microsoft.AspNetCore.Server.Kestrel.Core;

using Sirona.RemoteControl.Synchronizer.Extensions;
using Sirona.RemoteControl.Synchronizer.Services;
using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;
using Sirona.RemoteControl.Synchronizer.Services.Launchers;

using RemoteControlService = Sirona.RemoteControl.Synchronizer.Services.RemoteControlService;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    var grpcPort = builder.Configuration.GetRequiredValue<int>("grpcPort");
    options.Listen(IPAddress.Any, grpcPort, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
});
// Add services to the container.
builder.AddBroadcasters();
builder.AddAppLaunchers();

builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddGrpc();

WebApplication app = builder.Build();

app.MapGrpcService<SynchronizerService>();
app.MapGrpcService<RemoteControlService>();

app.Run();