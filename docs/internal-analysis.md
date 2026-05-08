# SensitiveFlow — Análise Profunda de Código

> **Documento vivo.** Última análise: 2026-05-08 (branch `feat/initial-infrastructure`, commit `de2c705`).
> **Propósito:** servir de linha de base para futuras análises. Pontos marcados em **VALIDADO** já foram considerados aceitáveis e **não devem ser re-litigados** sem mudança de contexto. Pontos em **ABERTO** são candidatos a correção/discussão.
>
> **Como atualizar:** ao revisitar, mover itens de "Aberto" para "Validado" ou "Resolvido" conforme decidido — não apague o histórico, apenas mude o status.

---

## 1. Sumário executivo

SensitiveFlow é uma biblioteca .NET (8/10) modular para **observabilidade e controle runtime de dados pessoais**: trilha de auditoria automática via interceptor EF Core, redação de logs por decoração de `ILogger`, mascaramento/anonimização/pseudonimização, retenção declarativa por atributos e analyzers Roslyn.

**Maturidade observada:** preview (`1.0.0-preview.1`). Núcleo conceitual sólido — separação correta entre anonimização irreversível, pseudonimização reversível e mascaramento; contratos `IAuditStore`/`ITokenStore` deixam persistência ao usuário; `IBatchAuditStore` evita N round-trips. Existem, no entanto, **bugs reais de correção** (item 4.1, 4.2), **lacunas operacionais** (item 4.3, 4.4) e **dívidas de performance** (item 5).

**Cobertura de testes:** alta para os módulos individualmente; faltam testes de integração que cruzem todas as camadas com store durável real (SQLite via EF Core), embora os samples cubram esse cenário sem assertions automatizadas.

---

## 2. Arquitetura e dependências

```
SensitiveFlow.Core   (atributos, enums, contratos, modelos, exceções)
   ├── SensitiveFlow.Audit          (extensão DI: AddAuditStore<T>)
   ├── SensitiveFlow.Anonymization  (Maskers, Pseudonymizers, Strategies)
   ├── SensitiveFlow.Logging        (RedactingLogger, redactor padrão)
   ├── SensitiveFlow.Retention      (RetentionEvaluator + handlers)
   ├── SensitiveFlow.AspNetCore     (HttpAuditContext, middleware IP-token)
   ├── SensitiveFlow.EFCore         (SaveChanges interceptor, NullAuditContext)
   └── SensitiveFlow.Analyzers      (SF0001/SF0002 — netstandard2.0)
```

**Pontos de acoplamento centrais:**

- O `IAuditContext` é o ponto de junção entre `AspNetCore` (lê HttpContext.Items) e `EFCore` (consumido pelo interceptor). Resolução por escopo é correta.
- `IPseudonymizer` é injetado tanto no middleware (para o IP) quanto livremente em código de aplicação. Não há registro automático de `TokenPseudonymizer` — o usuário registra manualmente OU usa `AddTokenStore<T>()`.
- `IAuditStore` é resolvido scoped pelo interceptor (também scoped). Discutido no item 4.3.

---

## 3. Decisões já VALIDADAS (não re-litigar)

> Itens abaixo são **intencionais** e foram revistos contra os trade-offs. Reabrir só com novo contexto.

### 3.1 Persistência fica fora da biblioteca (`IAuditStore`/`ITokenStore`)
**Decisão:** sem implementação default em produção. Usuário traz seu próprio store durável.
**Por quê:** evita lock-in com EF Core/SQL Server; quem usa Mongo/Redis/Azure Tables não paga custo de pacotes inúteis.
**Evidência:** README §3, `AuditServiceCollectionExtensions`, `AnonymizationServiceCollectionExtensions`.

### 3.2 IP nunca armazenado em texto puro
**Decisão:** middleware sempre pseudonimiza antes de gravar em `HttpContext.Items`. `AuditRecord.IpAddressToken` documenta explicitamente que valor cru é proibido.
**Por quê:** alinhamento com privacy regulations (IP é dado pessoal).
**Evidência:** `SensitiveFlowAuditMiddleware`, XML doc em `AuditRecord.IpAddressToken`.

### 3.3 Mascaramento ≠ Anonimização
**Decisão:** API distingue explicitamente os três conceitos. `MaskEmail/MaskPhone/MaskName` documentam que **resultado permanece dado pessoal**.
**Por quê:** usuários não devem confundir redução de exposição visual com conformidade de anonimização.
**Evidência:** XML docs em `IMasker`, `EmailMasker`, `NameMasker`, `PhoneMasker`, `StringAnonymizationExtensions`.

### 3.4 Anotação opt-in via atributos
**Decisão:** nada é "auto-detectado" como pessoal. O dev marca `[PersonalData]`/`[SensitiveData]`.
**Por quê:** heurísticas falham silenciosamente; metadata explícita é auditável.
**Evidência:** README §"Design Principles".

### 3.5 `SensitiveDataAuditInterceptor` flush é **pós**-SaveChanges (`SavedChangesAsync`)
**Decisão:** o store de auditoria só é gravado depois do commit do EF.
**Trade-off conhecido:** se o store falhar após persistir a entidade, fica gap de auditoria — porém o oposto (gravar antes) deixa registros de auditoria sem dado real, o que é pior para compliance.
**Evidência:** `SensitiveDataAuditInterceptor.SavedChangesAsync` linhas 67-78; cancelamento em `SaveChangesFailed` linhas 92-113.
**Status:** aceito. Para garantir consistência forte, o usuário pode envolver SaveChanges + audit em uma transação na sua implementação de `IAuditStore` (compartilhando `DbContext`).

### 3.6 `HmacPseudonymizer.Reverse` joga `NotSupportedException`
**Decisão:** é o comportamento correto — HMAC é determinístico mas **não reversível**. `TokenPseudonymizer` é o caminho para reversibilidade.
**Por quê:** semântica clara, força usuário a escolher conscientemente.
**Evidência:** `HmacPseudonymizer.Reverse` linha 63-65; teste `Reverse_ThrowsNotSupportedException`.
**Aviso:** ver item 4.1.2 sobre forma de lançar a exceção em `ReverseAsync`.

### 3.7 `NullAuditContext` como fallback singleton
**Decisão:** `AddSensitiveFlowEFCore()` registra `NullAuditContext.Instance` apenas se ninguém registrou antes (`TryAddSingleton`). `AddSensitiveFlowAspNetCore()` substitui por scoped `HttpAuditContext`.
**Por quê:** ordem de registro fica idempotente; cenário sem ASP.NET (console/worker) não quebra.
**Evidência:** `EFCoreServiceCollectionExtensions.AddSensitiveFlowEFCore` linha 24.

### 3.8 Tracking via `ConditionalWeakTable<DbContext, PendingAuditRecords>`
**Decisão:** mesmo o interceptor sendo scoped, registros pendentes são associados ao `DbContext` específico (caso múltiplos contextos coexistam no mesmo escopo).
**Por quê:** robustez. CWT não retém o DbContext após dispose, evitando vazamento.
**Evidência:** `SensitiveDataAuditInterceptor` linha 21.

### 3.9 `RetentionDataAttribute.GetExpirationDate` usa `AddYears`/`AddMonths`
**Decisão:** evita cálculo via `TimeSpan` que é insensível a anos bissextos / meses de tamanho variável.
**Por quê:** período declarado legalmente é em anos/meses, não em dias.
**Evidência:** XML doc do método; importante diferenciador de bibliotecas concorrentes.

### 3.10 Analyzer detecta sanitização por nome (Mask/Redact/Anonymize/Pseudonymize/Hash)
**Decisão:** heurística baseada em substring no nome do método.
**Trade-off:** falsos negativos se método tiver outro nome; falsos positivos se método não-sanitizador contiver substring. Aceitável para warnings (severidade Warning, não Error).
**Evidência:** `SymbolExtensions.IsSanitizationMethod`.

### 3.11 `[Sensitive]` prefixo na chave para redação estruturada
**Decisão:** convenção de marcar parâmetros sensíveis em templates: `"User {[Sensitive]Email}"`.
**Por quê:** sinks estruturados (Serilog/Seq) recebem propriedade redactada; mensagem renderizada também tem o valor substituído.
**Evidência:** `RedactingLogger.SensitiveKeyPattern`, samples.

---

## 4. Achados ABERTOS

### 4.1 Bugs de correção

#### 4.1.1 `SensitiveDataAuditInterceptor.ResolveDataSubjectId` aceita silenciosamente `Id = 0`
**Severidade:** Alta.
**Arquivo:** [SensitiveDataAuditInterceptor.cs:199-214](../src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs#L199-L214)
**Descrição:** o fallback é `DataSubjectId → Id → UserId`. Se a entidade tem `int Id` gerado pelo EF Core, no momento do `SavingChanges` ele ainda é `0`. `0.ToString() = "0"`, não é null/vazio, logo passa direto. Audit record gravado com `DataSubjectId = "0"` — agrupando todos os primeiros inserts entre entidades distintas.
**Reprodução:** entidade sem `DataSubjectId` explícito, com `int Id` auto-gerado, em `Added`. Audit gravado com "0".
**Sugestão:** rejeitar `"0"` para Id numéricos OU exigir `DataSubjectId` explícito (lançar mais cedo se não houver).

#### 4.1.2 `HmacPseudonymizer.ReverseAsync` lança síncrono em vez de retornar Task faulted
**Severidade:** Média.
**Arquivo:** [HmacPseudonymizer.cs:68-69](../src/SensitiveFlow.Anonymization/Pseudonymizers/HmacPseudonymizer.cs#L68-L69)
**Descrição:**
```csharp
public Task<string> ReverseAsync(string token, CancellationToken ct = default)
    => Task.FromResult(Reverse(token));   // Reverse(token) THROWS antes de Task.FromResult
```
`Reverse` joga `NotSupportedException` **antes** de `Task.FromResult` executar. O contrato de `Async` em .NET é que falhas vêm via Task faulted, não como throw síncrono. Consumidores que fazem `try { await x.ReverseAsync(...) }` capturam, mas frameworks que esperam Task (ex.: middlewares, pipelines reativos) podem quebrar.
**Sugestão:** `=> Task.FromException<string>(new NotSupportedException(...))`.

#### 4.1.3 `RedactingLogger.RedactSensitiveValues` substitui valor sensível como substring global
**Severidade:** Média.
**Arquivo:** [RedactingLogger.cs:84-102](../src/SensitiveFlow.Logging/Loggers/RedactingLogger.cs#L84-L102)
**Descrição:** após renderizar a mensagem, faz `redacted.Replace(value, marker)`. Se o valor sensível for muito curto (ex.: nome `"a"`) ou ocorrer como substring em outro campo, todas as ocorrências são substituídas — corrompendo a mensagem. Também processa em ordem não-determinística do `IEnumerable`.
**Sugestão:** redação na mensagem deve usar a posição do placeholder no template, não busca por valor. Alternativa: redactar somente em propriedades estruturadas (já feito) e deixar a mensagem renderizada usar marker via formatter customizado.

#### 4.1.4 `HashStrategy` concatena salt sem separador
**Severidade:** Baixa (criptográfica).
**Arquivo:** [HashStrategy.cs:37](../src/SensitiveFlow.Anonymization/Strategies/HashStrategy.cs#L37)
**Descrição:** `_salt + value` permite colisão: `("salt", "value")` e `("saltv", "alue")` produzem o mesmo input. Pouco explorável na prática (salt fixo), mas é um anti-padrão.
**Sugestão:** usar HMAC-SHA256 com salt como chave, ou inserir delimitador improvável no input.

#### 4.1.5 `RetentionDataAttribute.Years/Months` aceita valores negativos
**Severidade:** Baixa.
**Arquivo:** [RetentionDataAttribute.cs:11-15](../src/SensitiveFlow.Core/Attributes/RetentionDataAttribute.cs#L11-L15)
**Descrição:** sem validação. `[RetentionData(Years = -1)]` faz `GetExpirationDate` retornar data no passado, evaluator marca tudo como expirado.
**Sugestão:** validar no construtor / propriedade setter (lançar `ArgumentOutOfRangeException`).

### 4.2 Riscos de design / contrato

#### 4.2.1 `HmacPseudonymizer` valida `Length < 32` por caracteres, não bytes
**Severidade:** Baixa.
**Arquivo:** [HmacPseudonymizer.cs:33-36](../src/SensitiveFlow.Anonymization/Pseudonymizers/HmacPseudonymizer.cs#L33-L36)
**Descrição:** doc afirma "32 characters to match the SHA-256 digest size" — mas o digest tem 32 **bytes**. Para chaves ASCII, char ≈ byte; para Unicode, divergem. Mensagem confunde.
**Sugestão:** validar `Encoding.UTF8.GetByteCount(secretKey) >= 32` e atualizar docs.

#### 4.2.2 `AnonymizationServiceCollectionExtensions.AddTokenStore<T>` registra `IPseudonymizer` global
**Severidade:** Média (limitação de design).
**Arquivo:** [AnonymizationServiceCollectionExtensions.cs:33-39](../src/SensitiveFlow.Anonymization/Extensions/AnonymizationServiceCollectionExtensions.cs#L33-L39)
**Descrição:** uma única instância de `IPseudonymizer` no DI. Não há suporte a "HMAC para tipo X, Token para tipo Y" via keyed services ou named registration.
**Sugestão:** considerar API nomeada (`AddNamedPseudonymizer<T>("hmac", ...)`) ou keyed services do .NET 8.

#### 4.2.3 `ITokenStore.GetOrCreateTokenAsync` não documenta atomicidade
**Severidade:** Média.
**Arquivo:** [ITokenStore.cs:9-16](../src/SensitiveFlow.Core/Interfaces/ITokenStore.cs#L9-L16)
**Descrição:** dois callers concorrentes para o mesmo valor podem inserir dois mappings (no exemplo do README §3, `EfCoreTokenStore` faz read-then-write sem upsert/transação).
**Sugestão:** documentar que implementações DEVEM ser atômicas (unique index + handle de duplicate). Atualizar `backends-example.md` com upsert seguro.

#### 4.2.4 `EFCore.AddSensitiveFlowAuditContext<T>` é redundante
**Arquivo:** [EFCoreServiceCollectionExtensions.cs:34-39](../src/SensitiveFlow.EFCore/Extensions/EFCoreServiceCollectionExtensions.cs#L34-L39)
**Descrição:** o método existe para "substituir" `IAuditContext`, mas `AddSensitiveFlowEFCore` já usa `TryAddSingleton`, então qualquer registro posterior (ou anterior) ganha. O helper só esconde uma chamada simples.
**Status:** aceitar (DX) ou remover. Decidir.

#### 4.2.5 `SensitiveFlow.Audit` package praticamente vazio
**Descrição:** o pacote contém apenas `AuditServiceCollectionExtensions.AddAuditStore<T>` (4 linhas úteis). `IAuditStore`/`AuditRecord` vivem em `Core`. Faz sentido um pacote separado?
**Sugestão:** mover essa extensão para `Core` ou enriquecer `Audit` com primitivos compartilhados (ex.: helper de query, batch builder, retry decorator opcional).

#### 4.2.6 Ausência de "right to be forgotten" / erasure helper
**Descrição:** retenção dispara handler quando expira, mas não há utilitário canônico para "anonimizar todos os campos `[PersonalData]` desta entidade agora" (data subject request). Cada usuário re-implementa.
**Sugestão:** `IDataSubjectErasureService` ou helper estático que percorre atributos e aplica políticas.

#### 4.2.7 `RetentionEvaluator` não percorre entidades aninhadas
**Arquivo:** [RetentionEvaluator.cs:39](../src/SensitiveFlow.Retention/Services/RetentionEvaluator.cs#L39)
**Descrição:** apenas propriedades públicas do tipo top-level. `Customer.Address.PostalCode` com `[RetentionData]` é ignorado.
**Sugestão:** documentar limitação ou suportar recursão controlada (com proteção contra ciclos).

#### 4.2.8 `RetentionEvaluator` não aplica a política — só notifica
**Arquivo:** [RetentionEvaluator.cs:55-65](../src/SensitiveFlow.Retention/Services/RetentionEvaluator.cs#L55-L65)
**Descrição:** o nome "AnonymizeOnExpiration" sugere ação automática, mas o evaluator apenas chama handlers (que o usuário implementa). Sem handler, lança exceção.
**Sugestão:** doc `retention.md` deve abrir com "esta biblioteca não anonimiza/deleta — você implementa o handler". Hoje o aviso está, mas não é a primeira frase.

### 4.3 Problemas operacionais e de robustez

#### 4.3.1 Sem retry/circuit breaker no flush de auditoria
**Descrição:** falha transitória do store de auditoria quebra `SaveChangesAsync`. Não há decorator opcional para retry com backoff.
**Sugestão:** decorator `RetryingAuditStore` opcional + doc.

#### 4.3.2 Reflection sem cache no interceptor
**Arquivo:** [SensitiveDataAuditInterceptor.cs:117-167](../src/SensitiveFlow.EFCore/Interceptors/SensitiveDataAuditInterceptor.cs#L117-L167)
**Descrição:** `entity.GetType().GetProperties()` + `Attribute.IsDefined` em cada SaveChanges, para cada entidade, para cada propriedade. Para entidades com 30 propriedades em batches de 1000 entidades é caro.
**Sugestão:** cache `ConcurrentDictionary<Type, PropertyInfo[]>` com propriedades sensíveis pré-filtradas.

#### 4.3.3 `RetentionEvaluator` também faz reflection sem cache
**Mesma sugestão do 4.3.2** — escala com número de avaliações.

#### 4.3.4 `StringAnonymizationExtensions.PseudonymizeHmac` aloca novo `HmacPseudonymizer` por chamada
**Arquivo:** [StringAnonymizationExtensions.cs:86-87](../src/SensitiveFlow.Anonymization/Extensions/StringAnonymizationExtensions.cs#L86-L87)
**Descrição:** validação de chave + alocação de bytes UTF-8 a cada invocação. Em hot paths é desperdício.
**Sugestão:** cache estático `ConcurrentDictionary<string, HmacPseudonymizer>` ou exigir injeção via DI.

#### 4.3.5 Middleware ignora `X-Forwarded-For`
**Arquivo:** [SensitiveFlowAuditMiddleware.cs:29](../src/SensitiveFlow.AspNetCore/Middleware/SensitiveFlowAuditMiddleware.cs#L29)
**Descrição:** lê `RemoteIpAddress` direto. Em deploy atrás de load balancer/proxy, captura o IP do LB.
**Status:** correto delegar a configuração de `UseForwardedHeaders` ao usuário, mas a doc precisa lembrar disso (`aspnetcore.md`).

#### 4.3.6 `HttpAuditContext` lê `User.FindFirst("sub")`
**Arquivo:** [HttpAuditContext.cs:23-25](../src/SensitiveFlow.AspNetCore/Context/HttpAuditContext.cs#L23-L25)
**Descrição:** depende de o handler JWT ter desativado o `MapInboundClaims` que renomeia `sub` para `ClaimTypes.NameIdentifier`. Configuração default da Microsoft renomeia → `FindFirst("sub")` retorna null.
**Sugestão:** procurar `sub` E `ClaimTypes.NameIdentifier` antes de cair no `Identity.Name`.

#### 4.3.7 `Directory.Build.props` neutraliza `TreatWarningsAsErrors`
**Arquivo:** [Directory.Build.props:7-8](../Directory.Build.props#L7-L8)
**Descrição:** `<WarningsAsErrors />` vazio efetivamente desliga a promoção a erro de qualquer warning. Pode ser intencional, mas a interação com `TreatWarningsAsErrors=true` confunde.
**Sugestão:** remover a tag vazia ou comentar a intenção.

### 4.4 Lacunas / DX

#### 4.4.1 Projeto Benchmarks vazio
**Arquivo:** [tests/SensitiveFlow.Benchmarks/Program.cs](../tests/SensitiveFlow.Benchmarks/Program.cs)
**Descrição:** apenas `Console.WriteLine("Hello, World!");`. Pacote BenchmarkDotNet referenciado, mas sem benchmarks. Engano para quem clona o repo.
**Sugestão:** implementar benchmarks reais para `Pseudonymize`, `Mask*`, `Redact` ou remover o projeto até haver suíte.

#### 4.4.2 Analyzers sem code-fix providers
**Descrição:** SF0001/SF0002 reportam mas não sugerem `.MaskEmail()`/`.Pseudonymize(...)`. Roslyn permite quick-fix com refatoração automática.
**Sugestão:** adicionar `CodeFixProvider` para os dois analyzers.

#### 4.4.3 Sem analyzer para detecção de propriedades não-anotadas que parecem ser PII
**Descrição:** complementaria os existentes — sugerir `[PersonalData]` quando nome é `Email`/`Cpf`/`Phone`.
**Status:** nice-to-have. Severidade `Info`.

#### 4.4.4 Sem suíte de conformidade para implementações de `IAuditStore`/`ITokenStore`
**Descrição:** quem implementa um store próprio não tem testes prontos para validar contratos (idempotência, ordenação, paginação).
**Sugestão:** publicar `SensitiveFlow.TestKit` (xUnit base classes parametrizadas).

#### 4.4.5 README §3 mostra `EfCoreAuditStore` injetando `AuditDbContext` — mas DbContext é scoped
**Descrição:** o exemplo recomenda registrar via `AddAuditStore<EfCoreAuditStore>()` (que registra scoped). Funciona, mas se o usuário tem `SensitiveDataAuditInterceptor` resolvido para `DbContextOptions` via `AddInterceptors(sp.GetRequiredService<...>())`, ordem de resolução pode resultar em ciclo se `EfCoreAuditStore` referencia o **mesmo** DbContext que está sendo configurado.
**Sugestão:** documentar **dois DbContexts separados** ou descrever explicitamente o pattern self-referencing seguro.

#### 4.4.6 Samples registram `IPseudonymizer` manualmente em vez de usar `AddTokenStore<T>`
**Arquivo:** [WebApi.Sample/Program.cs:56-70](../samples/WebApi.Sample/Program.cs#L56-L70)
**Descrição:** comentário menciona "AddAuditStore/AddTokenStore" mas não usa. O sample acaba sendo verbose. `AddTokenStore<EfCoreTokenStore>()` faria o mesmo.
**Sugestão:** simplificar samples ou explicar por que a forma manual é necessária.

#### 4.4.7 `EfCoreTokenStore` no sample tem race
**Arquivo:** [SampleDbContext.cs:137-151](../samples/WebApi.Sample/Infrastructure/SampleDbContext.cs#L137-L151)
**Descrição:** read-then-write sem unique index nem handling de DbUpdateException. Dois requests com mesmo IP → dois tokens.
**Sugestão:** adicionar índice único em `Value` no DbContext OnModelCreating + try/catch ou upsert.

### 4.5 Correção/qualidade menores

| # | Local | Observação |
|---|-------|------------|
| 4.5.1 | `BrazilianTaxIdAnonymizer.Anonymize` | Chama `CanAnonymize` (3 regex) e depois `Regex.Replace` (4ª regex). Pode reusar match. |
| 4.5.2 | `HmacPseudonymizer.Pseudonymize` | `BitConverter.ToString(...).Replace("-","").ToLowerInvariant()` → `Convert.ToHexStringLower(hash)` (.NET 5+) é mais rápido e sem alocações intermediárias. |
| 4.5.3 | `NameMasker.Mask` | `Split(' ', None)` colapsa espaços múltiplos no `Join`. Cosmético. |
| 4.5.4 | `EmailMasker` regex | `^[^@]+@[^@]+\.[^@]+$` aceita `"a@b.c"`. OK para mascaramento, não para validação. |
| 4.5.5 | `PhoneMasker` regex | Não suporta separador `.` (`+55.11.99999.8877`). Documentar formatos suportados. |
| 4.5.6 | `AuditRecord.Id` default | Aloca `Guid.NewGuid()` mesmo se caller for sobrescrever. Marginal. |
| 4.5.7 | `AuditRecord.Operation` default `Access` | Default silencioso. Considerar `required` ou `init = AuditOperation.Unspecified`. |
| 4.5.8 | `AuditRecord.Timestamp` default `DateTimeOffset.UtcNow` | Capturado no `init`. Se record é criado bem antes do append, drift. Aceitável. |
| 4.5.9 | `Logging.Extensions.AddSensitiveFlowLogging` | Marker validado por `ArgumentException.ThrowIfNullOrWhiteSpace` — OK. |
| 4.5.10 | `Analyzers.IsLoggingCall` | Match por `containingType.Name == "LoggerExtensions"` (não-qualificado). Falsos positivos teóricos com tipos homônimos. |
| 4.5.11 | `RetentionEvaluator.EvaluateAsync` | Throw on first expired (sem handlers) — não acumula. Documentar fail-fast. |
| 4.5.12 | `Directory.Packages.props` | Mistura M$ 9.0 com EF Core 9.0 e CodeAnalysis 4.12 — verificar preview/RC vs release. |
| 4.5.13 | TFM `net8.0;net10.0` (sem net9.0) | Aceitável se net10 já está GA; verificar com release notes. |

---

## 5. Performance — visão consolidada

| Hot path | Custo atual | Mitigação |
|----------|-------------|-----------|
| Interceptor por SaveChanges | Reflection completa por entidade | Cache por Type (4.3.2) |
| `RetentionEvaluator.EvaluateAsync` | Reflection completa por chamada | Cache por Type (4.3.3) |
| `StringAnonymizationExtensions.PseudonymizeHmac` | Aloca pseudonymizer + valida key por call | Cache estático (4.3.4) |
| `RedactingLogger.Log` | Regex em mensagem de TODO log (mesmo sem prefixo `[Sensitive]`) | Pré-checar `Contains("[Sensitive]")` antes do Replace |
| `HmacPseudonymizer.Pseudonymize` | Hex via BitConverter | `Convert.ToHexStringLower` (4.5.2) |
| `BrazilianTaxIdAnonymizer.Anonymize` | 4 regex matches | Reusar match único |

---

## 6. Convenções de código a preservar

- **TFMs:** `net8.0;net10.0`. Analyzers em `netstandard2.0`.
- **Nullable:** habilitado em todo projeto.
- **Doc XML:** `GenerateDocumentationFile=true` exceto em `.Tests` — cada API pública tem `<summary>`, frequentemente com `<remarks>` legais (referências a privacy regulations).
- **Atributos selados (`sealed`):** padrão; herança não é parte do contrato.
- **`record` para modelos imutáveis** (`AuditRecord`).
- **Guard clauses padronizadas:** `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`.
- **Nomenclatura DI:** `AddSensitiveFlow<Modulo>()` para módulos; `AddAuditStore<T>` / `AddTokenStore<T>` para contratos parametrizáveis.
- **Testes:** xUnit + FluentAssertions + NSubstitute; `InMemory*Store` em pasta `Stores/` por projeto de teste (mesmo arquivo às vezes duplicado entre projetos — aceitável para isolar dependências).

---

## 7. Roteiro sugerido de correção (priorização)

| Prioridade | Item | Esforço |
|------------|------|---------|
| P0 | 4.1.1 (`Id=0` aceito) | Baixo |
| P0 | 4.1.2 (`ReverseAsync` síncrono throw) | Trivial |
| P1 | 4.1.3 (substring corruption no logger) | Médio (refator) |
| P1 | 4.3.6 (`sub` vs `NameIdentifier`) | Trivial |
| P1 | 4.3.2 + 4.3.3 (cache de reflection) | Baixo |
| P2 | 4.1.4 (HashStrategy salt) | Baixo |
| P2 | 4.1.5 (RetentionData negativo) | Trivial |
| P2 | 4.4.7 (race no sample EfCoreTokenStore) | Baixo |
| P3 | 4.4.1 (Benchmarks vazio) | Médio |
| P3 | 4.4.2 (Code fix providers) | Médio-Alto |
| P3 | 4.4.4 (TestKit conformance) | Alto |
| P3 | 4.2.6 (erasure helper) | Médio |

---

## 8. Itens explicitamente fora do escopo desta análise

Para evitar repetição em análises futuras, registramos o que **não** foi auditado em profundidade nesta passagem:

- Conformidade jurídica detalhada com LGPD/GDPR Article-by-article — biblioteca foca em runtime, não em paperwork.
- Compatibilidade ABI entre versões preview.
- Geração de pacotes NuGet (`.csproj` packaging metadata) além do `Directory.Build.props`.
- Workflows CI/CD (`.github/workflows/`) — apenas presença mencionada.
- Localização (mensagens de exceção em inglês — sem i18n).

---

## 9. Histórico de revisões

| Data | Branch | Revisor | Notas |
|------|--------|---------|-------|
| 2026-05-08 | `feat/initial-infrastructure` @ `de2c705` | Claude (Opus 4.7) | Análise inicial — linha de base. |
