using TradeLedger;
using TradeLedger.Core;
using TradeLedger.Core.Shared;
using TradeLedger.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<IUserContext>(_ => new FixedUserContext(null));

builder.Services.AddTradeLedgerCore(builder.Configuration);

builder.Services.AddHostedService<SyncWorker>();
builder.Services.AddHostedService<SnapshotWorker>();

var host = builder.Build();
host.Run();
