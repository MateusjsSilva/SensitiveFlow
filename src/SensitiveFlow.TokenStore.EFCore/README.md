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

## Possible Improvements

1. **Token expiration** — Auto-clean mappings after N days
2. **Salting strategies** — Per-field or per-user salts
3. **Encryption key rotation** — Seamless key migration
4. **Audit of pseudonymization** — Track who recovered originals
