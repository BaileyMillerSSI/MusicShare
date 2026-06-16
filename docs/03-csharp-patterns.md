# Cool C# Patterns Used in MusicShare

> **Slide talking points:** These are the patterns your Copilot-using teammates will recognize — but maybe haven't seen wired together like this before.

---

## 1. Saga Pattern (MassTransit State Machine)

**The "distributed workflow coordinator"**

### What problem does it solve?
- Resolving a song across 3 music services involves multiple async steps
- Each step can fail independently
- We need to know when ALL steps are done and handle partial failures
- Without a saga, you'd need a mess of callbacks, timers, or a polling loop

### How it works in MusicShare

```
ShareRequestSaga is a state machine:

  [Initial]
      ↓  SongShareSubmitted
  [ResolvingMetadata]
      ↓  SourceMetadataResolved
  [AwaitingServiceLinks]  ← waits for all 3 services
      ↓  all ServiceLinkResolved/Failed
  [Completed] or [Failed]
```

### Why this is cool
- Saga **state is persisted in MongoDB** — if the server crashes mid-resolution, it resumes automatically
- Handles **optimistic concurrency** — 3 service results arrive simultaneously, retries on conflict
- Each state transition is **explicit and traceable** in the Aspire dashboard
- Adding a new music service = one more event to wait for in `AwaitingServiceLinks`

### Code snapshot
```csharp
public class ShareRequestSaga : MassTransitStateMachine<ShareRequestSagaState>
{
    public State ResolvingMetadata { get; private set; }
    public State AwaitingServiceLinks { get; private set; }

    public ShareRequestSaga()
    {
        During(ResolvingMetadata,
            When(MetadataResolved)
                .Then(ctx => ctx.Saga.SongId = ctx.Message.SongId)
                .PublishAsync(ctx => ctx.Init<ResolveServiceLink>(new { /* YouTube */ }))
                .PublishAsync(ctx => ctx.Init<ResolveServiceLink>(new { /* Apple */ }))
                .TransitionTo(AwaitingServiceLinks));
    }
}
```

---

## 2. CQRS with MediatR

**"Commands change things. Queries read things. They live separately."**

### What problem does it solve?
- Keeps request handling logic out of controllers
- Commands and queries have different concerns — don't muddle them
- Each handler is independently testable
- MediatR acts as the in-process message bus

### The pattern: Static outer class with nested types

```csharp
public static class SubmitShare          // ← named for the operation
{
    public record Request(string Url)    // ← what comes in
        : IRequest<Response>;

    public class Handler(                // ← primary constructor DI
        IShareRequestService svc,
        IMusicServiceResolver resolver)
        : IRequestHandler<Request, Response>
    {
        public async Task<Response> Handle(Request req, CancellationToken ct)
        {
            var serviceType = resolver.DetectServiceType(req.Url);
            var shareId = await svc.Create(req.Url, serviceType!.Value, ct);
            return Response.AsSuccess(shareId, ShareStatus.Pending);
        }
    }

    public record Response(...)          // ← what goes out
    {
        public static Response AsSuccess(...) => new(...);
        public static Response AsFailure(...) => new(...);
    }
}
```

### Why this is cool
- **One file per operation** — no hunting across layers
- Factory methods on Response (`AsSuccess`, `AsFailure`) make intent obvious
- Controller becomes trivial: `var result = await _mediator.Send(command);`
- Copilot analogue: like a vertical slice in the same file

---

## 3. Decorator Pattern — Confidence Filtering

**"Wrap any adapter with smarter behavior, transparently."**

### What problem does it solve?
- Every music service returns search results — but how do you know if the result is actually the right song?
- You don't want this logic inside Spotify's adapter (or YouTube's, or Apple's)
- The decorator wraps any `IMusicServiceAdapter` and filters/ranks results by confidence — without the inner adapter knowing

### How confidence is scored

```
Title match    → 40% weight (Levenshtein fuzzy distance)
Artist match   → 25% weight (set intersection of artist names)
Album match    → 25% weight (fuzzy distance)
Duration       → 10% weight (tolerance bands: ±2s → 0.95, ±10s → 0.85...)

Total score ≥ threshold → keep the result
                        < threshold → discard
```

### The decorator in action

```csharp
// ConfidenceAdapter wraps ANY IMusicServiceAdapter
public class ConfidenceAdapter(
    IMusicServiceAdapter innerAdapter,
    IConfidenceScoreService scorer,
    double threshold) : IMusicServiceAdapter   // ← same interface!
{
    public IAsyncEnumerable<SongSearchResult> FindSongsAsync(SongMetadata metadata, ...)
    {
        return innerAdapter.FindSongsAsync(metadata)
            .Select(r => (result: r, score: scorer.CalculateScore(metadata, r.FoundMetadata)))
            .Where(r => scorer.MeetsThreshold(r.score, threshold))
            .OrderByDescending(r => r.score.TotalScore)
            .Select(r => r.result);
    }
}
```

### Wired up in DI — transparently

```csharp
// Register the raw adapter
builder.Services.AddTransient<SpotifyMusicAdapter>();

// Register the decorated version as the interface
builder.Services.AddTransient<IMusicServiceAdapter>(sp =>
    new ConfidenceAdapter(
        sp.GetRequiredService<SpotifyMusicAdapter>(),
        sp.GetRequiredService<IConfidenceScoreService>(),
        threshold: 0.75));
```

Consumers just ask for `IMusicServiceAdapter` — they have no idea there's a confidence filter in the middle.

---

## 4. Strategy Pattern — Music Service Adapters

**"Uniform interface, wildly different implementations."**

### The interface

```csharp
public interface IMusicServiceAdapter
{
    ServiceType ServiceType { get; }
    Task<SongMetadata?> ResolveMetadataAsync(string url, CancellationToken ct);
    IAsyncEnumerable<SongSearchResult> FindSongsAsync(SongMetadata metadata, CancellationToken ct);
    string NormalizeUrl(string url);
    string? ExtractSongId(string url);
}
```

### Three implementations
- `SpotifyMusicAdapter` → Spotify Web API (OAuth, real HTTP calls)
- `YouTubeMusicAdapter` → YouTubeMusicAPI NuGet package (no official API, creative workaround)
- `AppleMusicMockAdapter` → Placeholder (Apple has no public search API)

### `MusicServiceResolver` routes between them

```csharp
// URL detection → routes to the right adapter
var serviceType = resolver.DetectServiceType("https://open.spotify.com/track/...");
var adapter = resolver.GetAdapter(serviceType);
```

---

## 5. Repository Pattern

**"Your business logic shouldn't care that you're using MongoDB."**

```csharp
// Interface — business logic talks to this
public interface IShareRequestRepository
{
    Task<ShareRequest?> GetByShareIdAsync(string shareId, CancellationToken ct);
    Task<ShareRequest> InsertAsync(ShareRequest request, CancellationToken ct);
    Task UpdateAsync(ShareRequest request, CancellationToken ct);
}

// Implementation — MongoDB-specific, hidden from domain
public class ShareRequestRepository(IMusicShareDbContext context) : IShareRequestRepository
{
    private readonly IMongoCollection<ShareRequest> _col = context.ShareRequests;

    public async Task<ShareRequest?> GetByShareIdAsync(string shareId, CancellationToken ct)
    {
        var filter = Builders<ShareRequest>.Filter.Eq(r => r.ShareId, shareId);
        return await _col.Find(filter).FirstOrDefaultAsync(ct);
    }
}
```

### Why it matters for testing
- Mock the interface → no MongoDB needed in unit tests
- Integration tests use Aspire to spin up a real MongoDB

---

## 6. MassTransit Consumer Base Class

**"Abstract the common, specialize the different."**

- `ServiceLinkConsumerBase` handles the shared logic: idempotency check, error publishing, success publishing
- Concrete consumers (`SpotifyLinkConsumer`, `YouTubeMusicLinkConsumer`) just supply their `ServiceType` and adapter
- Auto-registered via assembly scanning — no manual registration needed

```csharp
// Concrete consumer is almost empty
public class SpotifyLinkConsumer(
    IMusicServiceResolver resolver,
    ISongServiceLinkRepository repo,
    ILogger<SpotifyLinkConsumer> logger)
    : ServiceLinkConsumerBase(repo, logger)
{
    protected override ServiceType ServiceType => ServiceType.Spotify;
    protected override IMusicServiceAdapter GetAdapter() => resolver.GetAdapter(ServiceType.Spotify)!;
}
```

---

## 7. IHostApplicationBuilder Extension Methods for DI

**"Keep Program.cs clean."**

```csharp
// Program.cs
builder.AddServiceDefaults();  // ← one line wires up everything

// Which calls:
public static TBuilder AddDomainServices<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    return builder
        .AddMusicServices()
        .AddFrontendRevalidateService();
}
```

- Fluent chaining keeps registrations grouped by domain
- Generic constraint `where TBuilder : IHostApplicationBuilder` works with both `WebApplicationBuilder` and Aspire's builder
- Easy to find where any service is registered

---

## 8. Primary Constructors for DI (C# 12)

**"Less boilerplate, same DI."**

```csharp
// Old way
public class ShareRequestService
{
    private readonly IPublishEndpoint _publishEndpoint;
    public ShareRequestService(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }
}

// New way (C# 12 primary constructors)
public class ShareRequestService(IPublishEndpoint publishEndpoint) : IShareRequestService
{
    // publishEndpoint is directly available as a parameter
}
```

Used consistently across every handler, consumer, and service in the codebase.
