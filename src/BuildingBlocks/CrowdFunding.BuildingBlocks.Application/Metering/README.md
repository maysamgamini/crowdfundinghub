# Application Metering Abstractions

## Purpose
Defines technology-agnostic contracts and models for recording revenue, usage, and billing events formatted according to the CloudEvents v1.0 standard.

## Files
- `CloudEvent.cs`: Standardized envelope representing usage and monetization events conforming to the CloudEvents v1.0 specification (JSON format).
- `IUsageMeteringClient.cs`: Ingestion abstraction for emitting usage events to backends like OpenMeter. By design, ingestion failures are treated as non-fatal to core transaction pipelines.
- `OpenMeterFeeOptions.cs`: Configuration options specifying platform cut, payment processing percentage fees, and flat rate deductions for fee calculations.
