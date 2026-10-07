# CatEcommerce.IntegrationTests

Real HTTP through `WebApplicationFactory<Program>`, against the shared local Postgres database —
the same instance the Development profile uses (HO-CAT-001 §5.1). No Testcontainers, no in-memory
EF provider.

## Adding a new area

Every area's tests are built from two small classes, both deriving from a shared base extracted
after wave A (HO-CAT-001, HO-ACC-001):

- `CatEcommerceApiFactory` (`CatEcommerceApiFactory.cs`) — the `WebApplicationFactory<Program>`
  base. Handles the Testing environment and the connection string for everyone.
- `CatEcommerceIntegrationTestBase<TFactory>` (`CatEcommerceIntegrationTestBase.cs`) — the test-base
  base. Handles the shared `JsonSerializerOptions` and exposes `Factory`/`CreateClient()`.

To add area `Foo`:

1. **Factory** — `FooApiFactory : CatEcommerceApiFactory`. Add only what `Foo` itself needs:
   ```csharp
   public sealed class FooApiFactory : CatEcommerceApiFactory
   {
       protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
       {
           settings["SomeFooOnlyKey"] = "value";
       }

       protected override void ConfigureAdditionalServices(IServiceCollection services) =>
           services.AddScoped<ISomeFooDependency, FakeFooDependency>();
   }
   ```
   Do **not** override `ConfigureWebHost` directly — it is sealed on the base on purpose, so the
   Testing environment and the connection string can never be silently skipped by a new area.
   Add your own `With...()` factory-variant methods (tightened rate limits, a fake `TimeProvider`,
   a spied dependency, ...) the same way `AccountsApiFactory` does — call `WithWebHostBuilder(...)`,
   which re-invokes `ConfigureWebHost` (and so your own overrides above) before layering on your
   delta, so you never need to repeat the connection string or your other base settings there.

2. **Test base** — `FooIntegrationTestBase : CatEcommerceIntegrationTestBase<FooApiFactory>`. Add
   only `Foo`'s own HTTP helpers (registering a test entity, extracting a `Foo`-specific header,
   ...). Override `CreateClient()` only if `Foo` needs non-default
   `WebApplicationFactoryClientOptions` (`AccountsIntegrationTestBase` does, for cookie handling).

3. **Test classes** — `public sealed class FooEndpointTests(FooApiFactory factory) : FooIntegrationTestBase(factory)`,
   one `IClassFixture<FooApiFactory>` per test class as usual; xUnit shares one instance (and so
   one Testing-environment host) across all tests in that class.

Already general enough to reuse without any changes: `WithCapturedLogs()` (captures every log line
across a whole factory instance, for a secrets-in-logs sweep) and `WithFixedRemoteIpAddress(ip,
trustAsProxy)` (fixes `Connection.RemoteIpAddress` so a test can observe a non-null caller IP) — both
live on `CatEcommerceApiFactory` itself.

## §16.1 — writing for a shared, non-empty database

The suite shares one Postgres database with local development and with its own previous runs —
there is no reset between runs and no Testcontainers-style clean slate. Four rules; break any of
them and the suite passes today and fails intermittently next month, which is worse than no suite
at all:

1. **Every test mints its own ids and its own owner.** Generate a fresh id (and a fresh owner,
   customer, mobile number, ...) per test. Never reuse a fixed literal.
2. **Never assert on a global count or on emptiness.** A list endpoint will return other tests'
   data and yesterday's manual poking. Assert *"this row is absent from **this test's own**
   scope"*, never *"the list is empty"*.
3. **Every natural key is unique per run.** Suffix any code/name that must be unique with a
   `Guid.N` (or similar) fragment; a fixed literal passes once on a clean database and fails for
   ever afterwards.
4. **No test depends on another's data or on execution order.** Each test arranges everything it
   needs itself. Do not disable xUnit parallelism to paper over a shared-state bug — fix the test.
5. **Never `TRUNCATE`, and never write cleanup that deletes rows outside the ids you just
   created.** Other suites — including other areas' — are relying on their own data still being
   there.

## Pointing a branch at its own database

`CatEcommerceApiFactory` reads its connection string from the `CATECOMMERCE_TEST_DB` environment
variable first, falling back to the shared `catecommerce_dev` database everyone has used to date:

```
Host=localhost;Port=5432;Database=catecommerce_dev;Username=postgres;Password=postgres
```

A wave-B (or later) area branch that wants its own database — to develop against without §16.1's
shared-state rules while iterating, for example — sets `CATECOMMERCE_TEST_DB` to its own Postgres
connection string before running `dotnet test`. No code change is needed, and nothing here changes
what the committed default points at for everyone else. Remember the migrations still have to be
applied to whichever database you point at (`dotnet dotnet-ef database update`, from
`CatEcommerce.Infrastructure` with `CatEcommerce.Api` as the startup project) before the suite can
pass against it.
