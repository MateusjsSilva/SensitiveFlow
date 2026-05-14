# Redis Token Store Project — Implementation TODO

**Status:** ❌ Not yet created as .csproj  
**Impact:** Redis-backed token store tests and NuGet package cannot be built  
**Referenced by:** 
- `src/SensitiveFlow.TokenStore.Redis/` (source files exist)
- `tests/SensitiveFlow.TokenStore.EFCore.Tests/RedisTokenStoreTests.cs` (tests disabled)
- `samples/Redis.Sample/` (sample code)

---

## Current State

✅ **Source files exist:**
- `src/SensitiveFlow.TokenStore.Redis/RedisTokenStore.cs` — 28 KB, public sealed class
- `src/SensitiveFlow.TokenStore.Redis/RedisTokenStoreExtensions.cs` — Service registration

❌ **Project file missing:**
- No `src/SensitiveFlow.TokenStore.Redis/SensitiveFlow.TokenStore.Redis.csproj`

❌ **Tests disabled:**
- `tests/SensitiveFlow.TokenStore.EFCore.Tests/RedisTokenStoreTests.cs` (wrapped in `/* */`)
- Cannot run because project reference not available

---

## Required Work

### 1. Create `SensitiveFlow.TokenStore.Redis.csproj`

**File:** `src/SensitiveFlow.TokenStore.Redis/SensitiveFlow.TokenStore.Redis.csproj`

**Template:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <RootNamespace>SensitiveFlow.TokenStore.Redis</RootNamespace>
    <AssemblyName>SensitiveFlow.TokenStore.Redis</AssemblyName>
    <Description>Redis-backed distributed token store for SensitiveFlow pseudonymization</Description>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="StackExchange.Redis" />
    <ProjectReference Include="..\SensitiveFlow.Core\SensitiveFlow.Core.csproj" />
  </ItemGroup>
</Project>
```

### 2. Update Global.json or Directory.Build.props

Ensure StackExchange.Redis version is consistent:

**In Directory.Build.props:**
```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.0" />
```

### 3. Enable Redis Tests

**File:** `tests/SensitiveFlow.TokenStore.EFCore.Tests/RedisTokenStoreTests.cs`

**Changes:**
1. Uncomment `using StackExchange.Redis;`
2. Uncomment `using SensitiveFlow.TokenStore.Redis;`
3. Remove the `/* TODO: ... */` wrapper around test classes
4. Replace `namespace SensitiveFlow.TokenStore.Tests;` with `namespace SensitiveFlow.TokenStore.EFCore.Tests;`

### 4. Update Test Project References

**File:** `tests/SensitiveFlow.TokenStore.EFCore.Tests/SensitiveFlow.TokenStore.EFCore.Tests.csproj`

**Add to ItemGroup:**
```xml
<ProjectReference Include="..\..\src\SensitiveFlow.TokenStore.Redis\SensitiveFlow.TokenStore.Redis.csproj" />
<PackageReference Include="StackExchange.Redis" />
```

### 5. Update Solution File

Add to `.sln` if not already present:
```
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "SensitiveFlow.TokenStore.Redis", "src\SensitiveFlow.TokenStore.Redis\SensitiveFlow.TokenStore.Redis.csproj", "{...GUID...}"
EndProject
```

---

## Testing After Implementation

### Unit Tests
```bash
dotnet test tests/SensitiveFlow.TokenStore.EFCore.Tests/SensitiveFlow.TokenStore.EFCore.Tests.csproj --filter "Redis"
```

Expected: All 10 RedisTokenStore tests pass (mocked)

### Container Tests  
Create `tests/SensitiveFlow.TokenStore.Redis.ContainerTests/` with real Redis via testcontainers

### Build
```bash
dotnet build src/SensitiveFlow.TokenStore.Redis/SensitiveFlow.TokenStore.Redis.csproj
```

Expected: 0 errors, 0 warnings across net8.0, net9.0, net10.0

---

## Package Metadata

When creating the .csproj, ensure these are set:

```xml
<PropertyGroup>
  <Title>SensitiveFlow Token Store — Redis</Title>
  <Description>Redis-backed distributed token store for pseudonymization across multiple app instances</Description>
  <Authors>SensitiveFlow Contributors</Authors>
  <PackageProjectUrl>https://github.com/your-org/sensitiveflow</PackageProjectUrl>
  <RepositoryUrl>https://github.com/your-org/sensitiveflow.git</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageTags>privacy;gdpr;data-protection;pseudonymization;redis;distributed</PackageTags>
</PropertyGroup>
```

---

## Documentation Updates Needed

1. **docs/backends-example.md** — Add Redis implementation example
2. **README.md** — Add Redis to supported backends list
3. **CHANGELOG.md** — Add entry when Redis package is published
4. **samples/Redis.Sample/README.md** — Ensure accurate (may need updates based on final implementation)

---

## Estimate

- Create .csproj: 15 min
- Enable tests: 15 min
- Test and verify: 30 min
- Documentation: 30 min
- **Total: ~90 min** (1.5 hours)

---

## Dependencies

- StackExchange.Redis 2.8.0+
- SensitiveFlow.Core (published package or local project reference)

---

## Notes

- Redis implementation is **not** durable by default — requires RDB/AOF config for production
- Good for distributed scenarios where multiple app instances share token store
- Excellent for high-throughput pseudonymization with horizontal scaling
- Sample implementation in `samples/Redis.Sample/` shows real-world usage

---

## Success Criteria

- [ ] `SensitiveFlow.TokenStore.Redis.csproj` created
- [ ] Tests compile without errors
- [ ] All 10 unit tests pass (mocked)
- [ ] NuGet package can be built
- [ ] Documentation updated
- [ ] CI pipeline includes Redis build
- [ ] Sample compiles and runs

