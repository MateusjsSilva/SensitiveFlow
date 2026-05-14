# Redis Token Store — Purpose & Use Cases

## O Que É?

O `RedisTokenStore` é uma implementação de `ITokenStore` que usa **Redis como backend distribuído** para armazenar mapeamentos de pseudonymização (tokens).

---

## Problema Que Resolve

### Cenário Sem Redis (SQL/EFCore)

```csharp
// Cada aplicação tem seu próprio banco de dados
var tokenStore = new EfCoreTokenStore(dbContext);  // SQLite, SQL Server, PostgreSQL, etc

// Instância A da app:
var token1 = await tokenStore.GetOrCreateTokenAsync("alice@corp.com");  // "tok_abc123"

// Instância B da app (outro servidor):
var token2 = await tokenStore.GetOrCreateTokenAsync("alice@corp.com");  // "tok_xyz789" ← DIFERENTE!

// ❌ Problema: Mesmo email gera tokens diferentes em servidores diferentes!
// ❌ Impossível correlacionar eventos entre instâncias
// ❌ Cada servidor precisa de seu próprio banco (replicated, janky, complexo)
```

### Solução Com Redis

```csharp
// Todos os servidores apontam para o MESMO Redis
var redis = ConnectionMultiplexer.Connect("redis-cluster.internal:6379");
var tokenStore = new RedisTokenStore(redis);

// Instância A:
var token1 = await tokenStore.GetOrCreateTokenAsync("alice@corp.com");  // "tok_abc123"

// Instância B:
var token2 = await tokenStore.GetOrCreateTokenAsync("alice@corp.com");  // "tok_abc123" ← MESMO!

// ✅ Consistente entre instâncias
// ✅ Eventos podem ser correlacionados
// ✅ Um único ponto de verdade (Redis)
```

---

## Casos De Uso

### 1. **Microsserviços com Escalamento Horizontal**

```
┌─────────────────────────────────────────┐
│  Load Balancer                          │
└──┬──────────────┬──────────────┬────────┘
   │              │              │
┌──▼──┐      ┌────▼──┐      ┌───▼───┐
│ App │      │ App   │      │ App   │
│ :1  │      │ :2    │      │ :3    │
└──┬──┘      └────┬──┘      └───┬───┘
   │              │              │
   └──────────────┼──────────────┘
                  │
            ┌─────▼──────┐
            │   Redis    │
            │ Cluster    │
            └────────────┘

Todos os servidores compartilham o mesmo Redis:
- Token para "alice@corp.com" é sempre "tok_abc123" em qualquer servidor
- Escalabilidade sem replicação de dados
- Sem race conditions
```

### 2. **Multi-Tenant com Dados Compartilhados**

```csharp
// Tenant A (Empresa XYZ):
var tokenStoreA = new RedisTokenStore(redis, keyPrefix: "tenant-a:tokens:");
var tokenA = await tokenStoreA.GetOrCreateTokenAsync("john@xyz.com");  // "tok_xyz_123"

// Tenant B (Empresa ABC):
var tokenStoreB = new RedisTokenStore(redis, keyPrefix: "tenant-b:tokens:");
var tokenB = await tokenStoreB.GetOrCreateTokenAsync("john@abc.com");  // "tok_abc_456"

// ✅ Isolamento via prefixo de chave
// ✅ Um Redis para múltiplos tenants
// ✅ Economia de infraestrutura
```

### 3. **High-Throughput com Cache Distribuído**

```csharp
// Auditoria de e-commerce com milhões de transações/hora
var pseudonymizer = new TokenPseudonymizer(
    new RedisTokenStore(redis, defaultExpiry: TimeSpan.FromDays(90))
);

// 10.000 requests/segundo:
// - Redis é mais rápido que banco relacional para reads
// - Sem I/O de rede desnecessário
// - Expiração automática (TTL) libera memória
```

### 4. **Correlação Entre Sistemas**

```
Sistema A (Ecommerce):
- Pseudonymiza cliente: "alice@corp.com" → "tok_abc123"
- Armazena em Redis

Sistema B (CRM):
- Precisa pseudonymizar o mesmo cliente
- Busca em Redis: "alice@corp.com" → "tok_abc123"
- ✅ Mesmo token! Pode correlacionar atividades entre sistemas
```

---

## Comparação: Diferentes ITokenStore

| Implementação | Tipo | Cenário | Pros | Contras |
|---|---|---|---|---|
| **InMemoryTokenStore** | Memória | Testes, single-instance | Rápido, simples | Perde dados no restart |
| **EfCoreTokenStore** | SQL | Produção single-instance | Durável, ACID | Sem scaling horizontal |
| **RedisTokenStore** | Cache | Produção escalável | Distribuído, rápido, cache | Precisa Redis, não é durável por padrão |
| **Híbrido (Redis + SQL)** | Cache + Durável | Alta confiabilidade | O melhor dos dois | Mais complexo, mais componentes |

---

## Arquitetura Redis

### Fluxo de Dados

```
1. GetOrCreateTokenAsync("alice@corp.com")
   │
   ├─ Busca Redis: GET "tokens:rev:alice@corp.com"
   │  (reverse index = valor → token)
   │
   ├─ Se encontrou:
   │  └─ Retorna token
   │
   └─ Se não encontrou:
      ├─ Gera token único: "tok_<guid>"
      ├─ Executa transação Lua (atomic):
      │  ├─ SET "tokens:tok_<guid>" "alice@corp.com" EX 90days
      │  └─ SET "tokens:rev:alice@corp.com" "tok_<guid>" EX 90days
      └─ Retorna token novo

2. ResolveTokenAsync("tok_abc123")
   │
   └─ Busca Redis: GET "tokens:tok_abc123"
      └─ Retorna "alice@corp.com"
```

### Vantagens

✅ **Transações Atômicas** — Scripts Lua garantem consistência  
✅ **TTL Automático** — Expiração automática de chaves (conformidade com retenção)  
✅ **Bidirecional** — Índices reversos para procuras rápidas  
✅ **Replicação** — Redis Cluster oferece alta disponibilidade  
✅ **Persistência Opcional** — RDB snapshots ou AOF para durabilidade  

---

## Quando NÃO Usar Redis

❌ **Single-Instance Server** — EfCoreTokenStore é suficiente e mais durável  
❌ **Retenção Rigorosa** — Precisa de backup garantido; use SQL + Redis juntos  
❌ **SLA 99.99%+ com Garantia Duração** — Redis pode perder dados em falha; use SQL  
❌ **Sem Infraestrutura Redis** — Custo/complexidade não compensa  

---

## Quando USAR Redis

✅ **Múltiplas Instâncias de App** — Scaling horizontal, load balancing  
✅ **Microsserviços** — Cada serviço precisa dos mesmos tokens  
✅ **Multi-Tenant** — Isolamento via prefixo de chave  
✅ **High-Throughput** — Bilhões de tokens/dia  
✅ **Cache Distribuído** — Reduz carga de BD relacional  

---

## Exemplo Prático: E-commerce

### Setup
```csharp
// Startup
var redis = ConnectionMultiplexer.Connect("redis.internal:6379");
var tokenStore = new RedisTokenStore(
    redis, 
    keyPrefix: "ecommerce:tokens:",
    defaultExpiry: TimeSpan.FromDays(365)  // Reter 1 ano para conformidade
);
var pseudonymizer = new TokenPseudonymizer(tokenStore);

builder.Services.AddSingleton(pseudonymizer);
```

### Auditoria
```csharp
// 3 servidores, cada um processando milhões de requests/dia
public class OrderController
{
    [HttpPost("/api/orders")]
    public async Task<OrderResponse> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Pseudonymizar email do cliente
        var emailToken = await _pseudonymizer.PseudonymizeAsync(request.Email);
        
        // Armazenar no audit log (Redis):
        // "token:emailToken" → "alice@corp.com" (só para forensics)
        // Todos os 3 servidores compartilham o mesmo token para "alice@corp.com"
        
        var auditRecord = new AuditRecord
        {
            DataSubjectId = emailToken,  // ← Pseudonymizado
            Entity = "Order",
            Operation = AuditOperation.Create,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        await _auditStore.AppendAsync(auditRecord);
        
        return new OrderResponse { OrderId = order.Id };
    }
}

// Correlação:
// - Servidor 1 processa request de alice@corp.com → token "tok_123"
// - Servidor 2 processa request de alice@corp.com → token "tok_123" (mesmo!)
// - Servidor 3 processa request de alice@corp.com → token "tok_123" (mesmo!)
// ✅ Todos os eventos de Alice podem ser correlacionados em dashboards de auditoria
```

---

## Implementação Status

| Item | Status | Arquivo |
|------|--------|---------|
| Código-fonte | ✅ Existe | `src/SensitiveFlow.TokenStore.Redis/` |
| Testes Unitários | ❌ Desabilitados | `tests/.../RedisTokenStoreTests.cs` (comentado) |
| Projeto .csproj | ❌ NÃO EXISTE | Precisa ser criado |
| NuGet Package | ❌ Não publicado | Depende do .csproj |
| Documentação | ⚠️ Parcial | XMLDoc presente, docs/ incompleto |
| Sample | ✅ Existe | `samples/Redis.Sample/` |

---

## Para Criar o Projeto Redis

1. **Criar `SensitiveFlow.TokenStore.Redis.csproj`** (15 min)
   - Template no `docs/REDIS_PROJECT_TODO.md`
   - Referências: StackExchange.Redis, SensitiveFlow.Core

2. **Habilitar Testes** (15 min)
   - Descoment `RedisTokenStoreTests.cs`
   - Adicionar projeto reference no .csproj

3. **Testar** (30 min)
   - `dotnet test` — 10 testes devem passar
   - `dotnet build` — deve compilar

4. **Publicar NuGet** (5 min)
   - Automático via CI/CD

---

## Conclusão

**Redis TokenStore** é para quando você tem:
- ✅ Múltiplos servidores de aplicação
- ✅ Scaling horizontal com load balancing
- ✅ Necessidade de tokens consistentes entre instâncias
- ✅ Infraestrutura Redis disponível

Sem essas condições, **EfCoreTokenStore** é mais simples e tão eficiente.

