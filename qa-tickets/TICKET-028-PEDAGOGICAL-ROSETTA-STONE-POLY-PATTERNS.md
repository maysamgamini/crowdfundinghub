# QA Ticket: TICKET-028

**Title:** The Pedagogical Rosetta Stone: Implement Multi-Paradigm Architectural Contrast Slices (Minimal CRUD vs. Clean CQRS vs. Rich DDD)  
**Severity:** 🟡 P2 (Medium - Educational Benchmark Requirement)  
**QA Focus Area:** Architecture Paradigms, Educational Clarity & Poly-Pattern Demonstration  
**Found By:** `qa-architect-curriculum`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In the current codebase, every single write operation—even basic status changes and entity inserts—is forced through the identical, heavyweight Clean Architecture + DDD pipeline:
- Controller $\to$ Request DTO $\to$ Validator $\to$ Command DTO $\to$ Command Dispatcher $\to$ Command Handler $\to$ Domain Aggregate $\to$ Domain Event $\to$ Outbox Message $\to$ Repository $\to$ DbContext.

Navigating **14 distinct files across 5 projects** just to insert a single record into PostgreSQL creates significant cognitive overhead.

### The Educational Problem
When principal engineers and software architects inspect this codebase to learn architectural trade-offs, they see **only one dogmatic pattern applied uniformly across the entire system**. They cannot see:
1. What this feature would look like as a high-performance **15-line Minimal API slice**.
2. What this feature looks like in a **pragmatic Clean CQRS slice** without domain aggregate abstractions.
3. Why the **Rich DDD + Outbox slice** is justified for high-value transactional domains (like contributions and financial state machines) but excessive for standard CRUD.

---

## 2. Blast Radius & Pedagogical Impact

- **Cognitive Exhaustion:** Junior to senior engineers studying the codebase may conclude that Clean Architecture and DDD inherently require 14 files per endpoint, promoting architectural dogma over pragmatism.
- **Missing Benchmarks:** There are no concrete performance, code-length, or complexity benchmarks in the codebase comparing the paradigms side-by-side.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Expose and cure **Architectural Dogmatism**. In enterprise engineering, architects frequently mandate a single architectural pattern (e.g. "Every endpoint in this company must use CQRS, Repositories, Domain Aggregates, and MediatR") regardless of business complexity. This ticket teaches students to evaluate the **Return on Investment (ROI) of Abstraction**.

### Monolith First, Microservices Ready
The outcome of this project is a **Modular Monolith, NOT microservices**. Inside a modular monolith, different modules and features have vastly different complexity profiles:
- *Reference data, lookups, and simple CRUD* should be written with high developer velocity using Tier 1 Minimal APIs.
- *High-value financial state machines and multi-step sagas* justify Tier 3 Rich DDD and Outbox events.
By demonstrating both styles in the exact same codebase, students see that **decomposition-readiness does not require 14 files for every simple table insert**. A Minimal API slice that writes to its own isolated schema is just as extractable into a microservice as a heavyweight DDD aggregate!

### What Breaks Tomorrow If Ignored Today?
If teams apply heavy DDD uniformly across all features, development velocity grinds to a halt, engineers suffer cognitive fatigue, and simple CRUD features incur enormous maintenance overhead with zero microservice decomposition benefits.

---

## 4. Affected Files & Modules

- Creation of a new dedicated sample directory: `src/Samples/RosettaStone/`
- Documentation references in [`educational/01-architecture-paradigms/poly-pattern-architecture.md`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/educational/01-architecture-paradigms/poly-pattern-architecture.md)

---

## 4. Implementation Specification & Greenfield Solution

Create `src/Samples/RosettaStone/` containing three side-by-side implementations of the exact same business requirement: **Creating a Campaign with Title, Story, and Target Amount**.

```
src/Samples/RosettaStone/
├── README.md                                 # Architectural scorecard & complexity analysis
├── 01-MinimalApiCrud/
│   └── CreateCampaignMinimalEndpoint.cs      # 1 file, 25 lines: Minimal API + EF Core direct
├── 02-PragmaticCqrs/
│   ├── CreateCampaignEndpoint.cs             # FastEndpoints / Controller
│   ├── CreateCampaignCommand.cs              # Command record + FluentValidation
│   └── CreateCampaignCommandHandler.cs       # Handler with direct DbContext projection
└── 03-RichDomainModel/
    └── (Points to the existing 14-file pipeline in CrowdFunding.Modules.Campaigns.*)
```

### Slice 1: `CreateCampaignMinimalEndpoint.cs` (Minimal CRUD)
```csharp
namespace CrowdFunding.Samples.RosettaStone.MinimalApi;

public static class CreateCampaignMinimalEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/rosetta/v1/campaigns", async (
            CreateCampaignRequest req,
            CampaignsDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title) || req.TargetAmount <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Title"] = ["Title is required and target must be positive."]
                });
            }

            var campaign = new CampaignRecord
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Story = req.Story,
                TargetAmount = req.TargetAmount,
                Currency = req.Currency,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.CampaignRecords.Add(campaign);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/rosetta/v1/campaigns/{campaign.Id}", new { campaign.Id });
        })
        .WithTags("RosettaStone")
        .WithSummary("Tier 1: Minimal API Single-File Slice");
    }
}
```

### Slice 2: `02-PragmaticCqrs` (Clean CQRS without DDD Aggregates)
- Separates request validation from execution.
- Employs a direct MediatR / Dispatcher command.
- Persists directly without an Aggregate Root or Domain Event pipeline.
- Ideal for 80% of enterprise CRUD applications.

### Slice 3: The Educational Contrast Document (`README.md`)
Include an explicit comparative scorecard:

| Dimension | Tier 1: Minimal API | Tier 2: Pragmatic CQRS | Tier 3: Rich DDD + Outbox |
| :--- | :---: | :---: | :---: |
| **Files Required** | 1 | 3 | 14 |
| **Lines of Code** | ~30 | ~90 | ~350 |
| **Assemblies Involved** | 1 | 2 | 5 |
| **Decomposition Effort** | Low (Self-contained) | Medium (DTO boundary) | Zero (Contracts already extracted) |
| **Audit & Outbox Guarantees** | None (Direct write) | Custom manual outbox | Automated via aggregate event collection |
| **When to Choose** | Config, Lookups, Prototypes | Standard Business CRUD | Financial Ledgers, Lifecycles, Multi-Module Sagas |

---

## 5. Verification & Acceptance Criteria

1. **Compilation & Routing:** All three Rosetta Stone endpoints compile and run under Swagger UI at `/swagger` under the `RosettaStone` tag.
2. **Behavioral Parity:** Sending valid payload `{ "title": "Test", "story": "A valid story...", "targetAmount": 1000, "currency": "USD" }` to all three endpoints succeeds and persists a valid record.
3. **Automated Unit Tests:** Add unit/integration tests asserting identical RFC 9457 Problem Details error responses across all three implementations for invalid inputs.
4. **Pedagogical Value:** Document contains clear guidelines on how to choose between the three styles without dogmatic bias.
