# SensitiveFlow.Audit.EFCore.Outbox

Outbox pattern implementation for reliable audit record publishing to external systems.

## Main Components

### Outbox Table
- **`AuditOutbox`** — Table for pending audit records
  - Transactional consistency (same commit as entity changes)
  - Background processor publishes to event bus

### Outbox Processor
- **`OutboxProcessor`** — Background job that publishes records
  - Retries on failure
  - Idempotent processing
  - Supports Kafka, RabbitMQ, Azure Service Bus

## Pattern

```
Entity Change
    ↓
Create AuditRecord → Write to Outbox table
    ↓
Commit transaction (both entity + outbox)
    ↓
OutboxProcessor (background)
    ├─ Reads from Outbox
    ├─ Publishes to event bus
    └─ Marks as processed
```

## Benefits

- Guaranteed delivery (at-least-once)
- No audit loss due to network failures
- Decoupled from external systems

## Possible Improvements

1. **Event filtering** — Route different record types to different topics
2. **Dead letter queue** — Handle permanently failed records
3. **Backpressure handling** — Limit publishing rate
