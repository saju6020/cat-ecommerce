// Template for the Api / web host's Program.cs.
//
// Host composition — what each extension call registers and why — is documented in
// `.ai/host-composition.md`. Read it before editing this file for real. Do NOT hand-register
// anything that `.ai/host-composition.md` lists under "Never hand-register these" (IResolver,
// IDispatcher, ICommandSender, IEventPublisher, IQueryProcessor, IAggregateRootRepository<>,
// IReadRepository, IEventRepository, IStateRepository, IMongoDbConnectionCache,
// IServiceRegistryProvider, IBusMessageDispatcher) — every one of them comes from
// AddInMemoryBusServices() / AddReadRepository() / AddServiceBusProvider() below. The Api is
// read-side only; aggregates belong to CommandWorker (ADR-008).
//
// Command Handlers do NOT belong in this host (ADR-004) — the Api sends Commands to the bus;
// CommandWorker executes them. Only Query Handlers and edge-validation Validators are registered
// here beyond the platform extensions.

using {{RootNamespace}}.Read.Queries.[ContextName];
using {{RootNamespace}}.Read.QueryHandlers.[ContextName];
using {{RootNamespace}}.Read.ViewModels;
using FluentValidation;
using Platform.Infrastructure.Core.Extensions;
using Platform.Infrastructure.Core.Queries;
using Platform.Infrastructure.MassTransit.Bus.RabbitMQ.Extensions;
using Platform.Infrastructure.Repository.MongoDb;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Platform extensions — do not replace any of these with hand-written registrations.
builder.Services.AddInMemoryBusServices();
// Api is read-side only - aggregates belong to CommandWorker (ADR-008).
builder.Services.AddReadRepository();
builder.Services.AddServiceBusProvider(builder.Configuration);

// Your own: Query Handlers for this context, and any edge-validation Validators.
builder.Services.AddScoped<IQueryHandlerAsync<Get[ResourceName]Query, [ResourceName]ViewModel?>, Get[ResourceName]QueryHandler>();
// builder.Services.AddScoped<IValidator<[ActionName]Command>, [ActionName]CommandValidator>();

var app = builder.Build();

app.UseHttpPipeline(enableAuthorization: false);

app.Run();
