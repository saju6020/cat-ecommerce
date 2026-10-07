// Template for EventWorker's Program.cs.
//
// Host composition — what each extension call registers and why — is documented in
// `.ai/host-composition.md`. Read it before editing this file for real. Do NOT hand-register
// anything that `.ai/host-composition.md` lists under "Never hand-register these" — everything
// AddInMemoryBusServices() and AddReadRepository()/AddAggregateRootRepository() provide is already
// covered below.
//
// This is the only host that runs read-side Event Handlers (projections). There is no assembly
// scanning (`.ai/di-registration-guide.md` §4) — every Event Handler must be registered explicitly
// here, and every event's consumer adapter must be registered on the bus with an explicit
// queueName, or messages are published and silently dropped
// (`.ai/host-composition.md`, "The CustomQueueConfig trap").
//
// A worker that only projects (never loads/saves an aggregate) needs AddReadRepository() alone —
// it does not need the full AddAggregateRootRepository(). Use the narrower call unless this worker
// genuinely needs to write aggregates.
//
// WorkerHostBuilder / HostBuilderOptions come from Platform.Infrastructure.MassTransit.Bus.RabbitMQ
// — not from Platform.Infrastructure.Host.Worker, which is unrelated middleware and must not be
// referenced by a bus worker. (The platform's own ProductExample.EventWorkerHost uses a plain
// Host.CreateApplicationBuilder instead of WorkerHostBuilder; either works, but WorkerHostBuilder is
// preferred here for correlation-id logging.)

using {{RootNamespace}}.Domain.Events.[ContextName];
using {{RootNamespace}}.Read.EventHandlers.[ContextName];
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Platform.Infrastructure.Core.Events;
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
        services.AddReadRepository(); // Use AddAggregateRootRepository() instead if this worker also writes aggregates.

        // Your own: Event Handlers for this context.
        services.AddScoped<IEventHandlerAsync<[EventName]Event>, [EventName]EventHandler>();

        // Bus wiring — queueName is mandatory; without it the endpoint never binds and messages
        // are dropped with no error anywhere (see the CustomQueueConfig trap above).
        services.AddServiceBusProvider(configuration, customQueueConfigurations:
        [
            new CustomQueueConfig(
                queueName: "{{RootNamespace}}.EventWorkerHost",
                receiveEndpointConfigurator: e =>
                {
                    e.Consumer(() => new EventConsumerAdapter<[EventName]Event>());
                },
                prefetchCount: 16,
                retryCount: 3,
                retryIntervalSeconds: 5)
        ]);
    }
};

WorkerHostBuilder.BuildWorkerHost(options).Build().Run();
