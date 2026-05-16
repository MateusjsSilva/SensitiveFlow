# SensitiveFlow.TokenStore.EFCore

Encrypted token storage for pseudonymization and reversible redaction.

## Main Components

### Token Store
- **`EfCoreTokenStore<TDbContext>`** — Persists pseudonym mappings
  - Original value ↔ Token (hashed/encrypted)
  - Reversible (can recover original if needed)
  - Useful for analytics with privacy

### Token Generation
- **`TokenGenerator`** — Creates unique tokens
  - Collision detection
  - Configurable format

## Usage

```csharp
[PersonalData]
[Redaction(
    Export = OutputRedactionAction.Pseudonymize,
    Logs = OutputRedactionAction.Pseudonymize
)]
public string Email { get; set; }

// Logs show: PT:a7f3b2c9 (token, not actual email)
// Audit records same
// But mapping stored in token_store table

// Analytics can correlate by token without knowing original
```

## Architecture

```
Original Email: alice@example.com
    ↓ (Hash + salt)
Token: PT:a7f3b2c9
    ↓ (Store mapping)
TokenStore: { original_hash, token, created_at }
    ↓ (Use in logs/export)
Log message: "User PT:a7f3b2c9 logged in"
    ↓ (Analytics can aggregate by token)
"10 logins from PT:a7f3b2c9 this week"
```

## Advanced Features

### Token Expiration
Automatically manage token lifespan and cleanup:

```csharp
var options = new TokenExpirationOptions
{
    DefaultTtl = TimeSpan.FromDays(30),
    PurgeOnAccess = true
};

var expirationService = new TokenExpirationService<TokenDbContext>(factory);
var expiredCount = await expirationService.GetExpiredCountAsync();
await expirationService.PurgeExpiredAsync();
```

**Components:**
- `TokenExpirationOptions` — Configures TTL and purge behavior
- `TokenExpirationService<TContext>` — Manages expiration and cleanup
- `TokenMappingEntity.ExpiresAt` — Optional expiration timestamp

### Salting Strategies
Use contextual salts to ensure the same value produces different tokens in different contexts:

```csharp
var plainStrategy = new PlainTextSaltStrategy();
var plainSalted = plainStrategy.Apply("alice@example.com", null);  // "alice@example.com"

var prefixStrategy = new PrefixSaltStrategy();
var emailSalted = prefixStrategy.Apply("alice@example.com", "email");    // "email:alice@example.com"
var phoneSalted = prefixStrategy.Apply("alice@example.com", "phone");    // "phone:alice@example.com"

var registry = new TokenSaltStrategyRegistry();
registry.Register("custom", new PrefixSaltStrategy());
var strategy = registry.GetOrDefault("custom");
```

**Components:**
- `ITokenSaltStrategy` — Strategy interface
- `PlainTextSaltStrategy` — No-op default
- `PrefixSaltStrategy` — Contextual prefix salt
- `TokenSaltStrategyRegistry` — Named strategy registry

### Key Rotation
Seamlessly migrate tokens when the pseudonymization scheme changes:

```csharp
var rotationService = new TokenKeyRotationService<TokenDbContext>(factory);

var allTokens = await rotationService.GetAllTokensAsync();

await rotationService.ReplaceTokenAsync(mappingId, "new-token");

var updates = new[] { (id1, "token1"), (id2, "token2") };
await rotationService.BulkReplaceAsync(updates);

await rotationService.DeleteAsync(mappingId);
```

**Components:**
- `TokenKeyRotationService<TContext>` — Bulk token migration
  - `GetAllTokensAsync()` — Inspect all mappings
  - `ReplaceTokenAsync()` — Update single mapping
  - `BulkReplaceAsync()` — Batch update
  - `DeleteAsync()` — Remove mapping

### Audit of Pseudonymization
Track token operations without storing original values:

```csharp
var auditSink = new InMemoryTokenAuditSink();
var innerStore = new EfCoreTokenStore<TokenDbContext>(factory);

var auditingStore = new AuditingTokenStore(innerStore, auditSink, actorId: "user-123");

await auditingStore.GetOrCreateTokenAsync("alice@example.com");
await auditingStore.ResolveTokenAsync("token-xyz");

var records = auditSink.GetRecords();
// records[0]: { Token = "token-...", Operation = Created, OccurredAt = ..., ActorId = "user-123" }
// records[1]: { Token = "token-xyz", Operation = Resolved, OccurredAt = ..., ActorId = "user-123" }
```

**Components:**
- `TokenAuditOperation` — Enum: `Created`, `Resolved`, `Expired`
- `TokenAuditRecord` — Immutable audit event
- `ITokenAuditSink` — Audit recipient interface
- `InMemoryTokenAuditSink` — Thread-safe in-memory sink (testing/development)
- `AuditingTokenStore` — Decorator that wraps `ITokenStore` and records operations
