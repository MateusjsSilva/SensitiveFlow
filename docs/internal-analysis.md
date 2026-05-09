# SensitiveFlow — Análise Profunda de Código

> **Documento vivo.** Última análise: 2026-05-08 (branch `feat/initial-infrastructure`).
> **Propósito:** servir de linha de base para futuras análises. Pontos marcados em **VALIDADO** ou **RESOLVIDO** já foram considerados aceitáveis e **não devem ser re-litigados** sem mudança de contexto. Pontos em **ABERTO** continuam pendentes.
>
> **Como atualizar:** ao revisitar, mover itens de "Aberto" para "Resolvidos" (§3.1) ou "Validados" (§3) com nota da resolução. Não apague o histórico.
>
> **Análise de 2026-05-08 (pós-sprint):** Source generator analisado em profundidade. Genéricos aninhados funcionam corretamente (`SymbolDisplayFormat.FullyQualifiedFormat` produz `typeof()` válido). Interfaces não são percorridas — limitação documentada em §4.2.9, fallback de reflection cobre. Predicate do generator otimizado para filtrar apenas atributos relevantes (§4.5.14).
>
> **Análise de 2026-05-09 (pós-sprint):** Correções aplicadas com base na análise externa: A1 (lifetime decorator), A2 (msg erro), A4 (AuditRecord.Id→Guid), A5 (remarks Operation), D1 (Scoped), D3 (IAuditLogRetention), L1 (namespace check), P1-P3 (perf), S1-S3 (generator incremental+interface walk), X1-X3 (docs+csproj). Build: 0 erros, 0 avisos. Testes: todos aprovados.
>
> **Reframing 2026-05-09:** Removidas referências diretas a LGPD/GDPR em README, docs e Description dos pacotes. A biblioteca posiciona-se como conjunto de primitivas técnicas para tratamento de dados sensíveis — a conformidade com qualquer regulação específica depende de como o app usa as primitivas.
>
> **Novas features 2026-05-09:** (a) `SensitiveFlow.Json` — modificador de `System.Text.Json` que mascara/redata/omite propriedades anotadas; configurável por opção global + override por atributo (`[JsonRedaction]`). (b) `IDataSubjectExporter` em `SensitiveFlow.Anonymization.Export` — análogo simétrico de `IDataSubjectErasureService` para portabilidade. (c) `DeterministicFingerprint` em `SensitiveFlow.Anonymization.Comparison` — tokens HMAC-SHA256 curtos para diff/comparação sem expor o valor. (d) `SensitiveDataAssert.DoesNotLeak` em `SensitiveFlow.TestKit.Assertions` — pega regressão de redação. (e) `AuditSnapshot` + `IAuditSnapshotStore` em `SensitiveFlow.Core` — auditoria por agregado (vs. per-field do `AuditRecord`). Limitação §4.2.9 (interface attributes) **resolvida** — o generator e o fallback agora mesclam atributos da interface no implementador.

---

## 1. Sumário executivo

SensitiveFlow é uma biblioteca .NET (8/10) modular para **observabilidade e controle runtime de dados pessoais**: trilha de auditoria automática via interceptor EF Core, redação de logs por decoração de `ILogger`, mascaramento/anonimização/pseudonimização, retenção declarativa por atributos e analyzers Roslyn.

**Maturidade observada:** preview (`1.0.0-preview.1`). Após a passagem de 2026-05-08, todos os bugs identificados (§4.1) e dívidas operacionais (§4.3) foram corrigidos. A biblioteca ganhou pacotes novos (`SensitiveFlow.Audit.EFCore`, `SensitiveFlow.Diagnostics`, `SensitiveFlow.SourceGenerators`, `SensitiveFlow.TestKit`, `SensitiveFlow.Analyzers.CodeFixes`) e o decorator `RetryingAuditStore` dentro de `SensitiveFlow.Audit`.

**Cobertura de testes:** 239 testes (+38 vs baseline). Testes específicos foram adicionados para cada correção de bug e para as novidades.

---

## 2. Arquitetura e dependências

```
SensitiveFlow.Core   (atributos, enums, contratos, modelos, exceções, SensitiveMemberCache)
   ├── SensitiveFlow.Audit          (extensão DI: AddAuditStore<T>; RetryingAuditStore)
   ├── SensitiveFlow.Audit.EFCore   (IAuditStore durável via EF Core)
   ├── SensitiveFlow.Anonymization  (Maskers, Pseudonymizers, Strategies, Erasure)
   ├── SensitiveFlow.Logging        (RedactingLogger, redactor padrão)
   ├── SensitiveFlow.Diagnostics    (OpenTelemetry bridge: ActivitySource + Meter)
   ├── SensitiveFlow.Retention      (RetentionEvaluator + handlers)
   ├── SensitiveFlow.AspNetCore     (HttpAuditContext, middleware IP-token)
   ├── SensitiveFlow.EFCore         (SaveChanges interceptor, NullAuditContext)
   ├── SensitiveFlow.Analyzers      (SF0001-SF0003 — netstandard2.0)
   ├── SensitiveFlow.Analyzers.CodeFixes (quick-fixes Wrap-with-Mask — netstandard2.0)
   ├── SensitiveFlow.SourceGenerators (metadados de membros sensíveis em tempo de compilação)
   └── SensitiveFlow.TestKit        (xUnit conformance suites)
```

**Pontos de acoplamento centrais (inalterados):**

- `IAuditContext` é a junção entre `AspNetCore` (HttpContext.Items) e `EFCore` (interceptor).
- `IPseudonymizer` é injetado no middleware (IP) e em código de aplicação.
- `IAuditStore` é resolvido scoped pelo interceptor.
- `SensitiveMemberCache` (Core, novo) deduplica reflection entre interceptor e retention evaluator.

---

## 3. Decisões VALIDADAS (não re-litigar)

> Itens abaixo são **intencionais** e foram revistos contra os trade-offs.

### 3.1 Resolvidos em 2026-05-08 (de §4)

| Item | Resolução | Arquivos / Commits |
|------|-----------|---------------------|
| **4.1.1** `Id=0` aceito silenciosamente | **Resolvido (breaking).** Removido fallback para `Id`. Interceptor agora exige `DataSubjectId` (ou `UserId` como alias legado) e lança `InvalidOperationException` quando ausente. EF providers atribuem `Id` antes do interceptor rodar — fallback era ambíguo. | [SensitiveDataAuditInterceptor.cs:200-227](../src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs#L200-L227) + 2 testes novos |
| **4.1.2** `ReverseAsync` throw síncrono | **Resolvido.** Substituído por `Task.FromException<string>`. Teste assert `task.IsFaulted` antes do await. | [HmacPseudonymizer.cs:71-76](../src/SensitiveFlow.Anonymization/Pseudonymizers/HmacPseudonymizer.cs#L71-L76) |
| **4.1.3** `RedactingLogger` substring corruption | **Resolvido.** Quando `{OriginalFormat}` está presente, mensagem é re-renderizada com placeholders substituídos pelos valores já redactados. Sem busca-e-troca por valor. Teste reproduz cenário "valor sensível como substring de outro campo". | [RedactingLogger.cs:81-117](../src/SensitiveFlow.Logging/Loggers/RedactingLogger.cs#L81-L117) |
| **4.1.4** `HashStrategy` salt sem separador | **Resolvido.** Quando salgado, agora usa `HMAC-SHA256(salt, value)` em vez de `salt + value`. Elimina ambiguidade. | [HashStrategy.cs:38-58](../src/SensitiveFlow.Anonymization/Strategies/HashStrategy.cs#L38-L58) |
| **4.1.5** `RetentionData` aceita negativo | **Resolvido.** Setters de `Years`/`Months` rejeitam negativos com `ArgumentOutOfRangeException`. | [RetentionDataAttribute.cs:14-44](../src/SensitiveFlow.Core/Attributes/RetentionDataAttribute.cs#L14-L44) |
| **4.2.1** Validação de chave HMAC por chars | **Resolvido.** Validação por bytes UTF-8 (≥32). Doc atualizada. | [HmacPseudonymizer.cs:30-44](../src/SensitiveFlow.Anonymization/Pseudonymizers/HmacPseudonymizer.cs#L30-L44) |
| **4.2.3** Atomicidade de `ITokenStore` não documentada | **Resolvido.** `docs/audit.md` agora explica retry; samples mostram unique index + recovery on `DbUpdateException`. | [audit.md](audit.md), [SampleDbContext.cs](../samples/WebApi.Sample/Infrastructure/SampleDbContext.cs) |
| **4.2.6** Sem erasure helper | **Resolvido.** Novo namespace `SensitiveFlow.Anonymization.Erasure`: `IDataSubjectErasureService` + `RedactionErasureStrategy` + `AddDataSubjectErasure()`. | [Erasure/](../src/SensitiveFlow.Anonymization/Erasure/) |
| **4.2.8** Doc de Retention confusa | **Resolvido.** `docs/retention.md` abre destacando que evaluator NÃO aplica políticas — handlers fazem. | [retention.md](retention.md) |
| **4.3.1** Sem retry/circuit breaker | **Resolvido.** `RetryingAuditStore` decorator com backoff exponencial limitado. Opt-in via `AddAuditStoreRetry()`. 6 testes. | [RetryingAuditStore.cs](../src/SensitiveFlow.Audit/Decorators/RetryingAuditStore.cs) + [RetryingAuditStoreTests.cs](../tests/SensitiveFlow.Audit.Tests/RetryingAuditStoreTests.cs) |
| **4.3.2 / 4.3.3** Reflection sem cache | **Resolvido.** `SensitiveMemberCache` em `Core`, consumido pelo interceptor e pelo `RetentionEvaluator`. Per-type, `ConcurrentDictionary`. | [SensitiveMemberCache.cs](../src/SensitiveFlow.Core/Reflection/SensitiveMemberCache.cs) |
| **4.3.4** `PseudonymizeHmac` aloca por chamada | **Resolvido.** Cache estático `ConcurrentDictionary<string, HmacPseudonymizer>` por chave secreta. | [StringAnonymizationExtensions.cs](../src/SensitiveFlow.Anonymization/Extensions/StringAnonymizationExtensions.cs) |
| **4.3.5** Middleware ignora `X-Forwarded-For` | **Resolvido (doc).** `docs/aspnetcore.md` agora tem seção "Behind a reverse proxy" mostrando como configurar `UseForwardedHeaders` antes de `UseSensitiveFlowAudit`. | [aspnetcore.md](aspnetcore.md) |
| **4.3.6** `sub` vs `NameIdentifier` | **Resolvido.** `HttpAuditContext.ActorId` agora consulta `sub` → `NameIdentifier` → `Identity.Name`. Doc atualizada com explicação sobre `MapInboundClaims`. Teste novo. | [HttpAuditContext.cs:28-44](../src/SensitiveFlow.AspNetCore/Context/HttpAuditContext.cs#L28-L44) |
| **4.3.7** `<WarningsAsErrors />` neutralizava `TreatWarningsAsErrors` | **Resolvido.** Tag vazia removida. | [Directory.Build.props](../Directory.Build.props) |
| **4.4.1** Benchmarks vazio | **Resolvido.** 3 suítes BenchmarkDotNet (`MaskingBenchmarks`, `PseudonymizationBenchmarks`, `InterceptorReflectionBenchmarks`). Programa usa `BenchmarkSwitcher`. | [tests/SensitiveFlow.Benchmarks/Benchmarks/](../tests/SensitiveFlow.Benchmarks/Benchmarks/) |
| **4.4.2** Sem code-fix providers | **Resolvido.** Novo projeto `SensitiveFlow.Analyzers.CodeFixes` (separado por causa de RS1038). `WrapWithMaskCodeFixProvider` escolhe `MaskEmail`/`MaskPhone`/`MaskName` por nome do membro. | [src/SensitiveFlow.Analyzers.CodeFixes/](../src/SensitiveFlow.Analyzers.CodeFixes/) |
| **4.4.4** Sem TestKit de conformidade | **Resolvido.** Novo `SensitiveFlow.TestKit` com `AuditStoreContractTests` e `TokenStoreContractTests`. | [src/SensitiveFlow.TestKit/](../src/SensitiveFlow.TestKit/) |
| **4.4.7** Race no sample `EfCoreTokenStore` | **Resolvido.** Unique index em `TokenMappingEntity.Value` em ambos samples (Web/Minimal API). `GetOrCreateTokenAsync` recupera o token vencedor em `DbUpdateException`. | [WebApi sample](../samples/WebApi.Sample/Infrastructure/SampleDbContext.cs), [MinimalApi sample](../samples/MinimalApi.Sample/Infrastructure/SampleDbContext.cs) |
| **4.5.1** TaxId regex duplicado | **Resolvido.** `DigitPattern` cached e reusado. | [BrazilianTaxIdAnonymizer.cs](../src/SensitiveFlow.Anonymization/Anonymizers/BrazilianTaxIdAnonymizer.cs) |
| **4.5.2** Hex via `BitConverter` | **Resolvido.** `Convert.ToHexStringLower` em .NET 9+ (com fallback `Convert.ToHexString().ToLowerInvariant()` em net8). Aplicado em `HmacPseudonymizer` e `HashStrategy`. | [HmacPseudonymizer.cs](../src/SensitiveFlow.Anonymization/Pseudonymizers/HmacPseudonymizer.cs), [HashStrategy.cs](../src/SensitiveFlow.Anonymization/Strategies/HashStrategy.cs) |

### 3.3 Resolvidos em 2026-05-09 (análise externa)

| Item | Resolução | Arquivos |
|------|-----------|----------|
| **A1** Inconsistência de lifetime decorator Retry | **Resolvido.** `AddAuditStoreRetry` preserva o lifetime do descriptor original (`existing.Lifetime`) em vez de forçar `Scoped`. Eliminado risco de captive-dependency DI exception em produção (`ValidateScopes=true`). | [AuditServiceCollectionExtensions.cs](../src/SensitiveFlow.Audit/Extensions/AuditServiceCollectionExtensions.cs) |
| **A2** Mensagem de erro de decorator insuficiente | **Resolvido.** `AddSensitiveFlowDiagnostics` agora explica a ordem correta e o efeito de invertê-la (spans por tentativa vs. span por ciclo). | [DiagnosticsServiceCollectionExtensions.cs](../src/SensitiveFlow.Diagnostics/Extensions/DiagnosticsServiceCollectionExtensions.cs) |
| **A4** `AuditRecord.Id` string aloca sempre | **Resolvido.** `Id` mudou de `string` para `Guid`. `Guid.NewGuid()` não aloca string intermediária. Entity mapper converte com `.ToString()` na persitência. Docs e samples atualizados. | [AuditRecord.cs](../src/SensitiveFlow.Core/Models/AuditRecord.cs), [AuditRecordEntity.cs](../src/SensitiveFlow.Audit.EFCore/Entities/AuditRecordEntity.cs) |
| **A5** `AuditRecord.Operation` default sem documentação | **Resolvido.** `<remarks>` adicionado explicando o default `Access` e quando sobrescrever. | [AuditRecord.cs](../src/SensitiveFlow.Core/Models/AuditRecord.cs) |
| **D1** `RetentionEvaluator` como `Transient` | **Resolvido.** Mudado para `Scoped` (idiomático para serviço de avaliação por request). Teste de conformidade atualizado. | [RetentionServiceCollectionExtensions.cs](../src/SensitiveFlow.Retention/Extensions/RetentionServiceCollectionExtensions.cs) |
| **D3** `AuditLogRetention<T>` sem interface | **Resolvido.** `IAuditLogRetention` extraída. Registrada via `TryAddSingleton<IAuditLogRetention>` nos dois overloads. Background jobs podem injetar a interface sem depender do tipo concreto genérico. | [IAuditLogRetention.cs](../src/SensitiveFlow.Audit.EFCore/Maintenance/IAuditLogRetention.cs) |
| **L1** `IsLoggingCall` sem checar namespace | **Resolvido.** `LoggerExtensions` verificado com namespace `Microsoft.Extensions.Logging` — elimina falso positivo com classes homônimas em outros namespaces. | [SensitiveDataLoggingAnalyzer.cs](../src/SensitiveFlow.Analyzers/Analyzers/SensitiveDataLoggingAnalyzer.cs) |
| **P1** `RedactingLogger` executa regex sempre | **Resolvido.** Fast-path `string.Contains("[Sensitive]", Ordinal)` antes do regex. Logs sem marcadores não tocam o regex. | [RedactingLogger.cs](../src/SensitiveFlow.Logging/Loggers/RedactingLogger.cs) |
| **P2** `AppendRangeAsync` loop `Add()` individual | **Resolvido.** Substituído por `set.AddRange(records.Select(...))`. | [EfCoreAuditStore.cs](../src/SensitiveFlow.Audit.EFCore/Stores/EfCoreAuditStore.cs) |
| **P3** `ChangeTracker.Entries().ToList()` sem pré-filtro | **Resolvido.** Filtro por `SensitiveMemberCache` adicionado dentro do LINQ antes do `.ToList()`. Entidades não-sensíveis não alocam entrada na lista. | [SensitiveDataAuditInterceptor.cs](../src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs) |
| **S1/S3** Generator sem cache incremental em `AttributeSymbols` | **Resolvido.** `AttributeSymbols` separado em pipeline `candidateTypes.Combine(attributeSymbols)` e `IEquatable` implementado — o pipeline incremental detecta igualdade e evita reemissão desnecessária. | [SensitiveMemberGenerator.cs](../src/SensitiveFlow.SourceGenerators/SensitiveMemberGenerator.cs) |
| **S2** Generator não percorre interfaces | **Resolvido.** `EnumeratePublicInstanceProperties` agora percorre `type.AllInterfaces` após a hierarquia de classes. Propriedades de interfaces são incluídas no mapa gerado. | [SensitiveMemberGenerator.cs](../src/SensitiveFlow.SourceGenerators/SensitiveMemberGenerator.cs) |
| **X1** XML doc exemplo com `EfCoreAuditStore` não-genérico | **Resolvido.** Exemplo corrigido para usar `AddEfCoreAuditStore()` e `AddEfCoreAuditStore<T>()`. | [AuditServiceCollectionExtensions.cs](../src/SensitiveFlow.Audit/Extensions/AuditServiceCollectionExtensions.cs) |
| **X2** XML doc exemplo com `TokenDbContext` fictício | **Resolvido.** Exemplo atualizado para referenciar o padrão real de implementação custom. | [AnonymizationServiceCollectionExtensions.cs](../src/SensitiveFlow.Anonymization/Extensions/AnonymizationServiceCollectionExtensions.cs) |
| **X3** `PackageDescription` em português | **Resolvido.** Todos os 9 `.csproj` com descrição em inglês. | todos os `.csproj` em `src/` |

### 3.2 Decisões originais (intencionais — não re-litigar)

#### 3.2.1 Persistência fica fora da biblioteca (`IAuditStore`/`ITokenStore`)
**Decisão:** sem implementação default em produção. Usuário traz seu próprio store durável.
**Por quê:** evita lock-in; quem usa Mongo/Redis/Azure não paga custo de pacotes inúteis.

#### 3.2.2 IP nunca armazenado em texto puro
**Decisão:** middleware sempre pseudonimiza antes de gravar.
**Por quê:** alinhamento com privacy regulations (IP é dado pessoal).

#### 3.2.3 Mascaramento ≠ Anonimização
**Decisão:** API distingue explicitamente os três conceitos.
**Por quê:** usuários não devem confundir redução de exposição visual com conformidade de anonimização.

#### 3.2.4 Anotação opt-in via atributos
**Decisão:** nada é "auto-detectado" como pessoal.
**Por quê:** heurísticas falham silenciosamente; metadata explícita é auditável.

#### 3.2.5 Flush é **pós**-SaveChanges
**Decisão:** store de auditoria gravado depois do commit do EF.
**Trade-off:** falha do store após persistir entidade deixa gap; opostamente, gravar antes deixaria registros sem dado real (pior para compliance). `RetryingAuditStore` (§3.1) reduz a janela de gap em ~3 ordens de grandeza para falhas transitórias.

#### 3.2.6 `HmacPseudonymizer.Reverse` joga `NotSupportedException`
**Decisão:** HMAC é determinístico mas não reversível — `TokenPseudonymizer` é o caminho para reversibilidade.

#### 3.2.7 `NullAuditContext` como fallback singleton
**Decisão:** `AddSensitiveFlowEFCore()` registra `NullAuditContext.Instance` apenas se ninguém registrou antes (`TryAddSingleton`).

#### 3.2.8 Tracking via `ConditionalWeakTable<DbContext, ...>`
**Decisão:** robustez contra múltiplos contextos no mesmo escopo, sem reter `DbContext` após dispose.

#### 3.2.9 `RetentionDataAttribute.GetExpirationDate` usa `AddYears`/`AddMonths`
**Decisão:** evita drift de `TimeSpan` em anos bissextos / meses variáveis.

#### 3.2.10 Analyzer detecta sanitização por nome
**Decisão:** heurística por substring (Mask/Redact/Anonymize/Pseudonymize/Hash). Severidade Warning aceita ruído.

#### 3.2.11 `[Sensitive]` prefixo na chave para redação estruturada
**Decisão:** convenção de marcar parâmetros sensíveis em templates.

---

## 4. Achados ABERTOS

### 4.1 Bugs de correção
*Todos os itens originais foram resolvidos em §3.1.*

### 4.2 Riscos de design / contrato

#### 4.2.2 `AnonymizationServiceCollectionExtensions.AddTokenStore<T>` registra `IPseudonymizer` global
**Status:** **Resolvido (2026-05-09).** `AddTokenStore<T>()` agora registra **apenas** `ITokenStore`. Novos métodos: `AddTokenPseudonymizer()` (convenience para `TokenPseudonymizer`), `AddPseudonymizer<TPseudonymizer>()` (qualquer implementação). `AddEfCoreTokenStore()` mantém o registro automático de ambos como convenience.

#### 4.2.4 `EFCore.AddSensitiveFlowAuditContext<T>` é redundante
**Status:** **Resolvido (2026-05-09).** Método removido. Substituído por `services.AddScoped<IAuditContext, TContext>()` direto — não agrega valor além do built-in do DI.

#### 4.2.5 `SensitiveFlow.Audit` package: agora justificado
**Status:** Resolvido por consequência. Com `RetryingAuditStore` e `AddAuditStoreRetry`, o pacote tem justificativa funcional além da extensão `AddAuditStore<T>`.

#### 4.2.7 `RetentionEvaluator` não percorre entidades aninhadas
**Status:** **Resolvido (2026-05-09).** `RetentionEvaluator` e `RetentionExecutor` agora percorrem recursivamente propriedades de tipos complexos (não-terminais). Tipos como `string`, `DateTime`, `Guid`, etc. são tratados como folhas. Cache `ConcurrentDictionary<Type, PropertyInfo[]>` evita reflection repetida.

#### 4.2.9 Atributos em propriedades de interface
**Status:** Resolvido (2026-05-09).
**Detalhe:** O generator e o fallback de reflexão agora mesclam atributos da propriedade da classe com os da propriedade homônima nas interfaces implementadas. Atributos declarados apenas na interface são automaticamente herdados pela implementação — o desenvolvedor não precisa repetir a anotação. Verificado por `Sensitive_PropertyAnnotatedOnlyOnInterface_IsDiscoveredOnImplementer`.

### 4.3 Problemas operacionais e de robustez
*Todos os itens originais foram resolvidos em §3.1.*

### 4.4 Lacunas / DX

#### 4.4.3 Sem analyzer para detecção de propriedades não-anotadas que parecem ser PII
**Status:** Aberto (nice-to-have, severidade Info).

#### 4.4.5 README e docs mostram `EfCoreAuditStore` de forma errônea
**Status:** Resolvido. Os guias principais usam `AddEfCoreAuditStore(...)` / `AddEfCoreAuditStore<TContext>()`; o README agora aponta para a referência por pacote.
**Severidade anterior:** Alta (causava erros de compilação diretos aos usuários).

#### 4.4.6 Samples registram `IPseudonymizer` manualmente em vez de usar `AddTokenStore<T>`
**Status:** **Resolvido (2026-05-09).** Comentários adicionados em ambos samples (WebApi e MinimalApi) explicando que o registro manual é deliberado para mostrar o wiring explícito, e apontando para `AddEfCoreTokenStore()` como alternativa mais simples.

#### 4.4.7 Lacunas de DX nos guias e tutoriais
**Status:** **Resolvido (2026-05-09).** `diagnostics.md`: adicionada seção "Installation" com `dotnet add package`, métricas de buffer documentadas, `using` explícitos. `logging.md`: adicionada seção "Installation". `retention.md`: esclarecido que `AddRetention()` e `AddRetentionExecutor()` são registros separados com propósitos distintos.

#### 4.4.8 Ausência de implementação oficial durável para `ITokenStore` (`SensitiveFlow.TokenStore.EFCore`)
**Status:** **Resolvido (2026-05-09).** Novo projeto `SensitiveFlow.TokenStore.EFCore`: `EfCoreTokenStore<TContext>` com `IDbContextFactory<TContext>`, índice único em `Value` para concorrência segura, `TokenDbContext` dedicado e `AddEfCoreTokenStore()` / `AddEfCoreTokenStore<TContext>()` que também registram `TokenPseudonymizer` como `IPseudonymizer`.
**Severidade:** Alta.

#### 4.4.9 Limitada instrumentação de observabilidade subjacente no `BufferedAuditStore`
**Status:** **Resolvido (2026-05-09).** `BufferedAuditStore` agora expõe: `GetHealth()` retornando `BufferedAuditStoreHealth` (pending/dropped/flush failures/isFaulted); métricas OpenTelemetry: `sensitiveflow.audit.buffer.pending` (gauge), `sensitiveflow.audit.buffer.dropped` (counter), `sensitiveflow.audit.buffer.flush_failures` (counter). Constantes em `SensitiveFlowDiagnostics`.

#### 4.4.10 Cobertura de TestContainers restrita ao PostgreSQL
**Status:** **Resolvido (2026-05-09).** Adicionados `SqlServerAuditStoreContainerTests` (append, batch, retention, retry decorator sobre SQL Server) e `RedisTokenStoreContainerTests` (GetOrCreate com atomicidade Lua, Resolve, KeyNotFound, valores distintos). Pacotes `Testcontainers.MsSql`, `Testcontainers.Redis`, `Microsoft.EntityFrameworkCore.SqlServer` e `StackExchange.Redis` adicionados.

#### 4.4.11 Limitação do JSON redaction
**Status:** Validado (design intent). Atualmente cobre apenas `System.Text.Json`. Se houver necessidade para `Newtonsoft.Json`, requererá um pacote separado adaptado (`SensitiveFlow.Json.Newtonsoft`).

#### 4.4.12 Falta de persistência durável no AuditSnapshot
**Status:** **Resolvido (2026-05-09).** Novo projeto `SensitiveFlow.Audit.Snapshots.EFCore`: `EfCoreAuditSnapshotStore<TContext>` com `IDbContextFactory<TContext>`, `SnapshotDbContext` dedicado, índices otimizados para aggregate/data-subject/timestamp, e `AddEfCoreAuditSnapshotStore()` / `AddEfCoreAuditSnapshotStore<TContext>()`.

### 4.5 Correção/qualidade menores

| # | Status | Observação |
|---|--------|------------|
| 4.5.1 | **Resolvido** (§3.1) | TaxId regex duplicado |
| 4.5.2 | **Resolvido** (§3.1) | Hex via Convert.ToHexStringLower |
| 4.5.3 | **Resolvido (2026-05-09).** `StringSplitOptions.None` → `RemoveEmptyEntries` — nomes com espaços múltiplos não colapsam mais. |
| 4.5.4 | Validado | `EmailMasker` regex permissiva. OK para mascaramento — o propósito é reduzir exposição visual, não validar. |
| 4.5.5 | **Resolvido (2026-05-09).** `<remarks>` documenta limitação com dots como separadores. |
| 4.5.6 | **Resolvido** (§3.3) | `AuditRecord.Id` mudou de `string` para `Guid`. `Guid.NewGuid()` é value-type, sem alocação de heap. |
| 4.5.7 | **Resolvido** (§3.3) | `<remarks>` adicionado explicando o default `Access` e quando sobrescrever. |
| 4.5.8 | Validado | `AuditRecord.Timestamp` capturado no `init`. Aceitável — `DateTimeOffset.UtcNow` é a intenção correta para auditoria. |
| 4.5.9 | OK | `AddSensitiveFlowLogging` validation. |
| 4.5.10 | **Resolvido** (§3.3) | `LoggerExtensions` verificado com namespace `Microsoft.Extensions.Logging`. |
| 4.5.11 | **Resolvido (2026-05-09).** `<remarks>` documenta comportamento fail-fast: sem handlers, primeira expiração lança exceção; com handlers, loop completa. |
| 4.5.12 | **Resolvido (2026-05-09).** TFM atualizado para `net8.0;net9.0;net10.0`. SDK 9.0.313 instalado. |
| 4.5.13 | **Resolvido (2026-05-09).** TFM `net8.0;net9.0;net10.0`. SDK 9.0.313 instalado. |
| 4.5.14 | **Resolvido** (§3.1) | Source generator predicate ineficiente — capturava TODAS propriedades com qualquer atributo (ex.: `[Required]`), causando trabalho extra em compilação. Otimizado para filtrar apenas `PersonalData`/`SensitiveData`/`RetentionData`. | [SensitiveMemberGenerator.cs:28-33](../src/SensitiveFlow.SourceGenerators/SensitiveMemberGenerator.cs#L28-L33) |
| 4.5.15 | **Resolvido** (§3.1) | Source generator não descobre propriedades de interfaces — walka apenas `BaseType`. Reflection fallback cobre. Documentado como limitação conhecida. | [SensitiveMemberGenerator.cs:107-128](../src/SensitiveFlow.SourceGenerators/SensitiveMemberGenerator.cs#L107-L128) |

---

## 5. Performance — visão consolidada

| Hot path | Status | Mitigação aplicada |
|----------|--------|-------------------|
| Interceptor por SaveChanges | **Resolvido** | `SensitiveMemberCache` (4.3.2) |
| `RetentionEvaluator.EvaluateAsync` | **Resolvido** | `SensitiveMemberCache` (4.3.3) |
| `StringAnonymizationExtensions.PseudonymizeHmac` | **Resolvido** | Cache estático por chave (4.3.4) |
| `HmacPseudonymizer.Pseudonymize` | **Resolvido** | `Convert.ToHexStringLower` + `HashData(Span<byte>)` |
| `BrazilianTaxIdAnonymizer.Anonymize` | **Resolvido** | Regex de dígito reutilizado (4.5.1) |
| `RedactingLogger.Log` | **Resolvido** | Fast-path `string.Contains("[Sensitive]", Ordinal)` antes do regex (§3.3 P1) |

---

## 6. Convenções de código a preservar

- **TFMs:** `net8.0;net10.0`. Analyzers/CodeFixes em `netstandard2.0`. TestKit em `net8.0;net10.0`.
- **Nullable:** habilitado em todo projeto.
- **Doc XML:** `GenerateDocumentationFile=true` exceto em `.Tests` e `TestKit`. APIs públicas têm `<summary>`/`<remarks>` (referências a privacy regulations onde aplicável).
- **`sealed` por padrão:** herança não é parte do contrato.
- **`record` para modelos imutáveis** (`AuditRecord`).
- **Guard clauses:** `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`.
- **Nomenclatura DI:** `AddSensitiveFlow<Modulo>()` para módulos; `AddAuditStore<T>` / `AddTokenStore<T>` / `AddAuditStoreRetry()` / `AddDataSubjectErasure()` para contratos parametrizáveis.
- **Reflection cache via `SensitiveMemberCache`** — não duplicar em novos módulos; reutilizar.
- **Testes:** xUnit + FluentAssertions + NSubstitute; `InMemory*Store` em pasta `Stores/` por projeto de teste.

---

## 7. Roteiro sugerido (priorização atualizada)

### Concluídos em 2026-05-08
P0 + P1 + P2 originais + perf + decorator retry + erasure + code-fix + TestKit + benchmarks + samples race fix + source generator predicate optimization + interface limitation documentation.

### Restantes
| Prioridade | Item | Esforço |
|------------|------|---------|
| P3 | 4.4.3 (analyzer para PII não anotada) | Médio |

---

## 8. Itens explicitamente fora do escopo

- Conformidade jurídica artigo-por-artigo com qualquer regulação específica — a biblioteca fornece primitivas técnicas; a conformidade depende de como a aplicação as usa.
- Compatibilidade ABI entre versões preview.
- Geração de pacotes NuGet além do `Directory.Build.props`.
- CI/CD workflows.
- Localização (mensagens em inglês — sem i18n).

---

## 9. Histórico de revisões

| Data | Branch / Commit | Revisor | Notas |
|------|-----------------|---------|-------|
| 2026-05-08 | `feat/initial-infrastructure` @ `de2c705` | Claude (Opus 4.7) | Análise inicial — linha de base. |
| 2026-05-08 | `feat/initial-infrastructure` (ajustes) | Claude (Opus 4.7) | Resolveu 21 itens (todos P0/P1/P2 + perf + DX). Adicionou pacotes `TestKit`, `Analyzers.CodeFixes`. Adicionou `RetryingAuditStore` decorator e erasure namespace. **Breaking:** interceptor agora exige `DataSubjectId`/`UserId`. 18 novos testes; total 219 verde em net10. |
| 2026-05-08 | `feat/initial-infrastructure` (source generator analysis) | Claude (Opus 4.7) | Análise aprofundada do source generator: genéricos aninhados validados (funcionam), interfaces não percorridas (documentado §4.2.9). Predicate otimizado para filtrar apenas atributos relevantes (§4.5.14). 20 novos testes (SF0003 + RetentionExecutor + Audit.EFCore); total 239 verde em net10. |
| 2026-05-09 | `feat/initial-infrastructure` (backend gaps) | GitHub Copilot (DeepSeek v4) | Resolveu 4 gaps arquiteturais (§4.4.8–§4.4.12): (1) `SensitiveFlow.TokenStore.EFCore` — `EfCoreTokenStore<TContext>` com índice único e concorrência segura; (2) Health checks + métricas OpenTelemetry no `BufferedAuditStore` (`GetHealth()`, gauges/counters de pending/dropped/flush failures); (3) Container tests para SQL Server (`SqlServerAuditStoreContainerTests`) e Redis (`RedisTokenStoreContainerTests` com atomicidade Lua); (4) `SensitiveFlow.Audit.Snapshots.EFCore` — `EfCoreAuditSnapshotStore<TContext>` com `SnapshotDbContext` dedicado. Novas constantes em `SensitiveFlowDiagnostics` para métricas de buffer. |
| 2026-05-09 | `feat/initial-infrastructure` (remaining open items) | GitHub Copilot (DeepSeek v4) | Resolveu itens pendentes do §4: (1) §4.2.4 — `AddSensitiveFlowAuditContext<T>` marcado `[Obsolete]`; (2) §4.2.7 — `RetentionEvaluator` e `RetentionExecutor` com travessia recursiva de objetos aninhados (cache `ConcurrentDictionary`); (3) §4.4.6 — comentários nos samples explicando registro manual vs `AddEfCoreTokenStore()`; (4) §4.4.7 — `diagnostics.md`/`logging.md`/`retention.md` com seções de instalação, usings explícitos e esclarecimento de registros separados; (5) §4.5.3 — `NameMasker` com `RemoveEmptyEntries`; (6) §4.5.5 — `PhoneMasker` documentando limitação com dots; (7) §4.5.11 — `RetentionEvaluator` documentando fail-fast; (8) §4.5.6/§4.5.7/§4.5.10/§5 — inconsistências corrigidas (itens já resolvidos em §3.3 mas ainda marcados como Aberto). Build: 0 erros, 0 avisos. Todos os testes passando. |
