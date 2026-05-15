# Redis Microservice Sample

Demonstrates how to use **SensitiveFlow Redis Token Store** in an **ASP.NET Core microservice** with multiple instances sharing a centralized Redis.

## Scenario

```
┌─────────────────┐
│  Service A      │
│  Port 5001      │  ┌──────────────┐
├─────────────────┤  │              │
│  Anonymize      │──┤   Redis      │
│  Token Store    │  │  localhost   │
└─────────────────┘  │  :6379       │
                     │              │
┌─────────────────┐  │              │
│  Service B      │  │              │
│  Port 5002      │──┤              │
├─────────────────┤  │              │
│  Anonymize      │  │              │
│  Token Store    │  │              │
└─────────────────┘  └──────────────┘

✅ Token created in A → Can be resolved in B
✅ Sensitive data never appears in logs
✅ Horizontally scalable
```

## Requirements

- .NET 10.0+
- Redis 7.0+ (local or container)

## Running locally

### 1. Start Redis

**With Docker:**
```bash
docker run -d -p 6379:6379 redis:7.0
```

**Or install locally** (Linux/Mac/Windows WSL):
```bash
brew install redis  # macOS
sudo apt install redis-server  # Ubuntu
```

### 2. Run the microservice

```bash
cd samples/Redis.Microservice.Sample
dotnet run
```

The API will be available at `https://localhost:5001` with Swagger at `/swagger`.

### 3. Test with curl

**Anonymize (create token):**
```bash
curl -X POST https://localhost:5001/api/pseudonymization/anonymize \
  -H "Content-Type: application/json" \
  -d '{"value": "alice@example.com"}' \
  -k
```

Response:
```json
{
  "token": "tok_KjvqHvB4LAVxub923Ba-gA"
}
```

**Deanonymize (resolve token):**
```bash
curl -X POST https://localhost:5001/api/pseudonymization/deanonymize \
  -H "Content-Type: application/json" \
  -d '{"token": "tok_KjvqHvB4LAVxub923Ba-gA"}' \
  -k
```

Response:
```json
{
  "value": "alice@example.com"
}
```

**Health check:**
```bash
curl https://localhost:5001/api/pseudonymization/health -k
```

## Testing with multiple instances

### Terminal 1: Start the first instance
```bash
dotnet run --launch-profile https
```

### Terminal 2: Start the second instance on another port
```bash
dotnet run --launch-profile https -- --urls "https://localhost:5002"
```

### Terminal 3: Test

**On Service A (5001):**
```bash
TOKEN=$(curl -s -X POST https://localhost:5001/api/pseudonymization/anonymize \
  -H "Content-Type: application/json" \
  -d '{"value": "shared-secret@test.com"}' \
  -k | jq -r '.token')

echo "Token created: $TOKEN"
```

**On Service B (5002) — resolve the token created in A:**
```bash
curl -X POST https://localhost:5002/api/pseudonymization/deanonymize \
  -H "Content-Type: application/json" \
  -d "{\"token\": \"$TOKEN\"}" \
  -k | jq
```

✅ The token created in A is resolved in B automatically!

## Configuration

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

For production with Sentinel (HA):
```json
{
  "ConnectionStrings": {
    "Redis": "sentinel1:26379,sentinel2:26379,sentinel3:26379,serviceName=mymaster"
  }
}
```

## Endpoints

| Method | Path | Description |
|--------|------|-----------|
| `POST` | `/api/pseudonymization/anonymize` | Create token for a value |
| `POST` | `/api/pseudonymization/deanonymize` | Resolve token to value |
| `GET` | `/api/pseudonymization/health` | Check service health |

## Example: Request/Response

### Anonymize
**Request:**
```json
{
  "value": "192.168.1.100"
}
```

**Response (200 OK):**
```json
{
  "token": "tok_X7pQ9mK2vZ5rL3jW1nB8dF"
}
```

### Deanonymize
**Request:**
```json
{
  "token": "tok_X7pQ9mK2vZ5rL3jW1nB8dF"
}
```

**Response (200 OK):**
```json
{
  "value": "192.168.1.100"
}
```

**Response (404 Not Found):**
```json
"Token not found or has expired"
```

## Why it matters for microservices

### Problem WITHOUT Redis Token Store
- Service A: `value` → `token_A`
- Service B: `value` → `token_B` ← **Different tokens!**
- ❌ Impossible to correlate data across services

### Solution WITH Redis Token Store
- Service A: `value` → Redis → `token_X`
- Service B: `value` → Redis → `token_X` ← **Same token!**
- ✅ Data correlatable across services

## Scalability

```
┌─ App Instance 1 (port 5001) ─────┐
│  GET /anonymize?value=email     │──┐
└─────────────────────────────────┘  │
                                     ├─→ Redis Cluster
┌─ App Instance 2 (port 5002) ─────┐ │
│  POST /deanonymize?token=xyz     │──┤
└─────────────────────────────────┘  │
                                     ├─→ (Shared state)
┌─ App Instance N (port 500N) ─────┐ │
│  DELETE /tokens/xyz              │──┘
└─────────────────────────────────┘

N instances = 1 Redis = Synchronized
```

## Next steps

1. **Container Tests**: `tests/SensitiveFlow.TokenStore.Redis.ContainerTests/`
2. **Kubernetes**: Use Redis Helm chart for deployments
3. **Monitoring**: Configure Prometheus + Grafana for Redis metrics
4. **Resilience**: Implement circuit breaker with Polly

## References

- [StackExchange.Redis Docs](https://stackexchange.github.io/StackExchange.Redis/)
- [SensitiveFlow Documentation](../../docs/)
- [Redis Best Practices](https://redis.io/docs/management/persistence/)
