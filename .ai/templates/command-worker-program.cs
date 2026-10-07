// Template for CommandWorker's Program.cs.
//
// Host composition — what each extension call registers and why — is documented in
// `.ai/host-composition.md`. Read it before editing this file for real. Do NOT hand-register
// anything that `.ai/host-composition.md` lists under "Never hand-register these" — everything
// AddInMemoryBusServices() and AddAggregateRootRepository() provide is already covered below.
//
// This is the only host that runs Command Handlers (ADR-004). There is no assembly scanning
// (`.ai/di-registration-guide.md` §4) — every Command Handler, Command Validator and CommandService
// must be registered explicitly here, and every Command's consumer adapter must be registered on
// the bus with an explicit queueName, or messages are accepted and silently dropped
// (`.ai/host-composition.md`, "The CustomQueueConfig trap").
//
// WorkerHostBuilder / HostBuilderOptions come from Platform.Infrastructure.MassTransit.Bus.RabbitMQ
// — not from Platform.Infrastructure.Host.Worker, which is unrelated middleware and must not be
// referenced by a bus worker.

using {{RootNamespace}}.Application.CommandHandlers.[ContextName];
using {{RootNamespace}}.Application.Commands.[ContextName];
using {{RootNamespace}}.Application.CommandServices.[ContextName];
using {{RootNamespace}}.Application.CommandServices.Abstractions;
using FluentValidation;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Platform.Infrastructure.Core.Bus;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Extensions;
using Platform.Infrastructure.MassTransit.Bus.RabbitMQ;
using Platform.Infrastructure.MassTransit.Bus.RabbitMQ.Extensions;
using Platform.Infrastructure.Repository.MongoDb;

BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));

var options = new HostBuilderOptions
{
    CommandLineArguments = args,
    UseConsoleLoggingWithCorrelationId = true,
    AddApplicationServices = (configuration, services) =>
    {
        // Platform extensions — do not replace any of these with hand-written registrations.
        services.AddInMemoryBusServices();
        services.AddAggregateRootRepository();
        services.AddSingleton<IBusMessageDispatcher, BusMessageDispatcher>();

        // Your own: Command Handlers, Validators and CommandServices for this context.
        services.AddScoped<ICommandHandlerAsync<[ActionName]Command>, [ActionName]CommandHandler>();
        services.AddScoped<IValidator<[ActionName]Command>, [ActionName]CommandValidator>();
        services.AddScoped<I[ActionName]CommandService, [ActionName]CommandService>();

        // Bus wiring — queueName is mandatory; without it the endpoint never binds and messages
        // are dropped with no error anywhere (see the CustomQueueConfig trap above).
        services.AddServiceBusProvider(configuration, customQueueConfigurations:
        [
            new CustomQueueConfig(
                queueName: "{{RootNamespace}}.CommandWorkerHost",
                receiveEndpointConfigurator: e =>
                {
                    e.Consumer(() => new CommandConsumerAdapter<[ActionName]Command>());
                },
                prefetchCount: 16,
                retryCount: 3,
                retryIntervalSeconds: 5)
        ]);
    }
};

WorkerHostBuilder.BuildWorkerHost(options).Build().Run();
