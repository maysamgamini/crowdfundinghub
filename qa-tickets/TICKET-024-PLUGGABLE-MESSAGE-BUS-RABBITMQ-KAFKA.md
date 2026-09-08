# QA Ticket: TICKET-024

**Title:** Pluggable Message Bus Abstraction (`IMessageBus`) Supporting In-Process vs. Distributed Broker (RabbitMQ / Kafka)  
**Severity:** 🔴 P1 (Critical - Microservice Decomposition Blocker)  
**QA Focus Area:** Event-Driven Architecture & Message Broker Decoupling  
**Found By:** `qa-architect-curriculum`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In the current codebase, the transactional outbox worker dispatches application events in-process directly via Microsoft Dependency Injection:

[`OutboxProcessorBackgroundService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Background/OutboxProcessorBackgroundService.cs#L82):
```csharp
var publisher = services.GetRequiredService<IEventPublisher>();
await publisher.PublishAsync(domainEvent, cancellationToken);
```
Where `IEventPublisher` is implemented exclusively by [`ServiceProviderEventPublisher.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Events/ServiceProviderEventPublisher.cs#L17):
```csharp
var handlers = _serviceProvider.GetServices(handlerType);
foreach (var handler in handlers)
{
    await (Task)method.Invoke(handler, [domainEvent, cancellationToken])!;
}
```

### Why This Is a Critical Decomposition Trap
1. **In-Process Lock-In:** Application events cannot leave the operating system process boundary. An external microservice (e.g. `CrowdFunding.Moderation.Service` or a separate `NotificationService`) has no mechanism to subscribe to `CampaignCreatedApplicationEvent` or `PaymentConfirmedApplicationEvent`.
2. **Zero Broker Integration:** The codebase lacks integration with industry-standard brokers like RabbitMQ, Azure Service Bus, or Apache Kafka.
3. **Missing Architectural Teaching Moment:** Students cannot observe the seamless transition from Monolith in-process dispatching to distributed pub/sub without rewriting application handlers.

---

## 2. Blast Radius & Decomposition Impact

- **Physical Isolation Barrier:** No module can be extracted into an independent deployment unit or container without maintaining the outbox worker inside the monolithic API host.
- **Message Serialization Rigidity:** Outbox payloads are stored as raw JSON without standardized CloudEvents metadata envelope headers (e.g., `id`, `source`, `type`, `time`, `datacontenttype`, `traceparent`).

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Demonstrate the **Dependency Inversion Principle at the Infrastructure Boundary**. Application event handlers should be completely agnostic of whether events are dispatched in-memory within a single .NET process or broadcast across a distributed cluster via RabbitMQ or Apache Kafka.

### Monolith First, Microservices Ready
The outcome of this project is a **Modular Monolith**, where the default deployment setting is `Messaging:Provider = "InProcess"`. Everything runs efficiently inside a single host process with zero extra infrastructure overhead (no Docker containers or cloud brokers required for local run). However, because the system binds to `IMessageBus` and wraps payloads in CNCF CloudEvents v1.0 envelopes, **turning any module into a microservice tomorrow requires toggling a single configuration key (`Messaging:Provider = "RabbitMQ"`)**, without touching a single application command or domain event handler.

### What Breaks Tomorrow If Ignored Today?
If a monolith hardcodes in-memory event dispatching (`IServiceProvider.GetServices`), when a team is tasked with extracting a high-traffic service, they must rewrite all event publishing and subscription logic, migrate message schemas, and refactor error-handling pipelines under immense delivery pressure.

---

## 4. Affected Files & Modules

- [`src/BuildingBlocks/CrowdFunding.BuildingBlocks.Application/Abstractions/Events/IEventPublisher.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Application/Abstractions/Events/IEventPublisher.cs)
- [`src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Events/ServiceProviderEventPublisher.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/BuildingBlocks/CrowdFunding.BuildingBlocks.Infrastructure/Events/ServiceProviderEventPublisher.cs)
- [`src/API/CrowdFunding.API/Background/OutboxProcessorBackgroundService.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Background/OutboxProcessorBackgroundService.cs)

---

## 4. Implementation Specification & Greenfield Solution

Implement a pluggable `IMessageBus` abstraction backed by configuration, supporting both **InProcess** (local MediatR/DI style) and **Distributed Broker** (MassTransit with RabbitMQ / Kafka) modes.

```mermaid
graph TD
    Outbox[Transactional Outbox Table] --> Poller[Outbox Processor Worker]
    Poller --> Bus[IMessageBus Interface]
    Bus -->|appsettings: "InProcess"| InProc[InProcessMessageBus<br/>Dispatches via IServiceProvider]
    Bus -->|appsettings: "RabbitMQ"| Rabbit[RabbitMqMessageBus / MassTransit<br/>Publishes to RabbitMQ Topic Exchange]
    Bus -->|appsettings: "Kafka"| Kafka[KafkaMessageBus<br/>Publishes CloudEvents to Kafka Topic]
    
    Rabbit --> Micro1[Extracted Moderation Microservice]
    Rabbit --> Micro2[Extracted Notification Microservice]
```

### Step 1: Define `IMessageBus` in `BuildingBlocks.Application`
```csharp
namespace CrowdFunding.BuildingBlocks.Application.Abstractions.Messaging;

public interface IMessageBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IApplicationEvent;

    Task PublishBatchAsync(IEnumerable<IApplicationEvent> events, CancellationToken cancellationToken = default);
}
```

### Step 2: Implement CloudEvents v1.0 Envelope Wrapper
Ensure events published to external brokers conform to CNCF CloudEvents:
```csharp
public sealed record CloudEventEnvelope<T>(
    string Id,
    string Source,
    string Type,
    DateTimeOffset Time,
    string DataContentType,
    string? TraceParent,
    T Data
);
```

### Step 3: Implement Providers in `BuildingBlocks.Infrastructure`
1. `InProcessMessageBus`: Invokes local `IApplicationEventHandler<T>` implementations registered in DI.
2. `RabbitMqMessageBus`: Uses MassTransit or raw `RabbitMQ.Client` to publish to an exchange named `crowdfunding.events`.

### Step 4: Configuration Switch in `appsettings.json`
```json
{
  "Messaging": {
    "Provider": "InProcess", // Options: "InProcess", "RabbitMQ", "Kafka"
    "RabbitMq": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "guest",
      "Password": "guest"
    }
  }
}
```

### Step 5: Dependency Injection Extension
```csharp
public static IServiceCollection AddCrowdFundingMessaging(this IServiceCollection services, IConfiguration config)
{
    var provider = config.GetValue<string>("Messaging:Provider") ?? "InProcess";
    if (provider.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase))
    {
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(config["Messaging:RabbitMq:Host"]);
                cfg.ConfigureEndpoints(context);
            });
        });
        services.AddScoped<IMessageBus, MassTransitMessageBus>();
    }
    else
    {
        services.AddScoped<IMessageBus, InProcessMessageBus>();
    }
    return services;
}
```

---

## 5. Verification & Acceptance Criteria

1. **Configuration-Driven Provider:** Changing `Messaging:Provider` from `"InProcess"` to `"RabbitMQ"` requires zero code changes in application handlers or controllers.
2. **CloudEvents Standard Compliance:** All messages delivered to RabbitMQ contain valid CloudEvents headers (`ce-id`, `ce-source`, `ce-type`, `ce-specversion: 1.0`).
3. **Integration Test Suite:** Add a Testcontainers-backed RabbitMQ integration test (`RabbitMqMessageBusTests.cs`) verifying pub/sub delivery across independent simulated services.
