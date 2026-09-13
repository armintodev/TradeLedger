# Backend changes required by the web frontend

Companion to [`web/SPEC.md`](../web/SPEC.md). That spec assumes this document has
been implemented first.

Only **§1 is a blocker**. Everything below it is a defect or a deferred feature
that `web/SPEC.md` explicitly works around, recorded here so the workaround has
somewhere to point.

Scope rule inherited from `CLAUDE.md` §1: none of this touches a Bitunix write
endpoint, and none of it changes sync watermark semantics.

**Status.** §1, §2 and §4 are implemented, and `web/` is built against them —
the frontend follow-ups those sections called for are done. §3, §5 and §6 are
still open; §3 is left alone on purpose because `TrackedFrom` is the backfill
floor and `CLAUDE.md` §8 requires asking before changing watermark semantics.

---

## 1. CORS policy — REQUIRED, blocks all frontend work

**Problem.** `src/TradeLedger.Api/Program.cs` never calls `AddCors` or `UseCors`.
The frontend is served from a different origin (`http://localhost:5173` in
development, the nginx container in compose), so every request — including the
login that would prove anything else works — is blocked by the browser before it
reaches the API. The failure is a console CORS error with no server-side log,
which is exactly the kind of thing that eats an afternoon.

**Change.**

### 1.1 Options type

New file `src/TradeLedger.Api/Shared/CorsOptions.cs`:

```csharp
namespace TradeLedger.Api.Shared;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "web";

    public string[] AllowedOrigins { get; set; } = [];
}
```

### 1.2 Registration in `Program.cs`

Add to the service registrations, near the other `Configure<>` calls:

```csharp
var cors = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
           ?? new CorsOptions();

builder.Services.AddCors(options =>
    options.AddPolicy(CorsOptions.PolicyName, policy =>
        policy
            .WithOrigins(cors.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
```

`AllowAnyMethod` matters: the journal uses `PATCH` and the credentials and proxy
endpoints use `PUT` and `DELETE`. A hand-listed method set will silently break
the review loop.

Do **not** add `AllowCredentials`. The frontend authenticates with an
`Authorization: Bearer` header, not cookies, so credentialed CORS is unnecessary
and would forbid ever using a wildcard origin.

### 1.3 Middleware placement in `Program.cs`

`app.UseCors` must sit **before** `UseAuthentication`/`UseAuthorization` and
before the endpoint mappings, so that preflight `OPTIONS` requests — which carry
no `Authorization` header and would otherwise 401 — are answered by the CORS
middleware:

```csharp
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors(CorsOptions.PolicyName);   // <-- here

// ... the Development-only OpenAPI/Scalar/Swagger block ...

app.UseAuthentication();
app.UseAuthorization();
```

Placing it above the exception handler would strip CORS headers from error
responses, which makes a 422 from the journal form look like a network failure in
the browser.

### 1.4 Configuration

`src/TradeLedger.Api/appsettings.Development.json`:

```jsonc
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173", "http://localhost:4173" ]
}
```

`5173` is the Vite dev server, `4173` is `vite preview`.

`appsettings.json` ships an empty array, so a misconfigured production deployment
refuses browser origins rather than accepting all of them:

```jsonc
"Cors": {
  "AllowedOrigins": []
}
```

The compose deployment sets its origin through the environment, using the
standard `__` separator for array indices:

```
Cors__AllowedOrigins__0=http://localhost:8080
```

### 1.5 Verification

An empty `AllowedOrigins` array makes `WithOrigins` match nothing, which is the
intended safe default — but it is also indistinguishable from a typo at runtime.
Log the resolved origins once at startup so a missing config is visible:

```csharp
app.Logger.LogInformation(
    "CORS policy '{Policy}' allows {Count} origin(s): {Origins}",
    CorsOptions.PolicyName,
    cors.AllowedOrigins.Length,
    string.Join(", ", cors.AllowedOrigins));
```

**Test** — add to `tests/TradeLedger.IntegrationTests`:

1. A preflight `OPTIONS /api/trades` carrying `Origin: http://localhost:5173` and
   `Access-Control-Request-Method: PATCH` returns 2xx with
   `Access-Control-Allow-Origin: http://localhost:5173`.
2. The same preflight from `https://evil.example` returns no
   `Access-Control-Allow-Origin` header.
3. A real `GET /api/trades` with an allowed `Origin` and a valid bearer token
   carries the allow-origin header on the 200.
4. A 401 response still carries the allow-origin header — this is what §1.3's
   ordering buys, and it is the regression most likely to reappear.

### 1.6 Documentation

Add a Cors row to `CLAUDE.md` §7a so the setting is discoverable next to the
other configuration.

---

## 2. `MarketSession` breakdown groups on the wrong field — defect

**Problem.** `src/TradeLedger.Core/Analytics/AnalyticsService.cs:171`:

```csharp
BreakdownDimension.MarketSession => trade.MarketContext?.MarketSession ?? "(none)",
```

`MarketContext.MarketSession` is the trader's **free-text checklist note** — what
they thought at the time. `Trade.MarketSession` is the `[Flags]` enum **derived**
from `OpenedAt` by `MarketSessionCalendar` and persisted precisely so it can be
grouped on. `CLAUDE.md` §3 calls out the distinction explicitly.

Consequences today: the breakdown keys are whatever free text was typed, every
trade without a filled checklist collapses into `(none)`, and every synced trade
that has never been reviewed is therefore invisible in this dimension. The
Tokyo/London/New York comparison — the reason the enum exists — cannot be read
off this endpoint.

Note that `GET /api/trades?marketSession=` filters on the **correct** field
(`TradeEndpoints.cs:50`), so the journal and the analytics disagree with each
other right now.

**Change.**

```csharp
BreakdownDimension.MarketSession => MarketSessionCalendar.Describe(trade.MarketSession),
```

`Describe` already produces `"Tokyo"`, `"Tokyo / London overlap"`,
`"London / New York overlap"` and `"Off hours"` — it is what
`TradeDetailResponse.marketSessionLabel` uses, so the breakdown keys become
consistent with the trade detail page.

**Decide before implementing:** overlaps currently produce a combined key
(`"Tokyo, London"` is its own row, distinct from `"Tokyo"`). That is probably
what you want — the overlap is the window traders care about — but the
alternative is one row per flag, with overlapping trades counted in both. The
combined key is the smaller change and keeps `tradeCount` summing to the total.

**Test** — unit test in `tests/TradeLedger.UnitTests`: three trades opened at
02:00, 08:00 and 14:00 UTC produce the keys `Tokyo`, `Tokyo / London overlap` and
`London / New York overlap`, with no reliance on `MarketContext`.

**Frontend follow-up:** ~~once merged, drop requirement 38 and Open Question 1
from `web/SPEC.md`~~ — done. Requirement 38 now describes the derived keys and
the analytics page renders no caveat.

---

## 3. Write endpoints for profile and account settings — partly done

**`timeZoneId` is done.** `PUT /api/auth/me/timezone` takes `{ "timeZoneId": "Asia/Tehran" }`
and returns the stored value. It sits in the `Auth` slice beside `GET /api/auth/me`
rather than under a new `/api/me` group, so the profile's read and its one write
stay on the same path.

**It is not validated with `TimeZoneInfo`,** which is what this section used to
call for. The solution builds with `InvariantGlobalization=true`
(`Directory.Build.props`), and under it `TimeZoneInfo` has no ICU data to work
from. Measured on the Windows dev host:

| Call | Result under invariant globalization |
|---|---|
| `TryFindSystemTimeZoneById("Asia/Tehran")` | **false** |
| `TryFindSystemTimeZoneById("Iran Standard Time")` | true |
| `TryConvertWindowsIdToIanaId("Iran Standard Time")` | **false**, no output |

On Linux it inverts — IANA ids resolve from `/usr/share/zoneinfo`, Windows ids do
not — so the same request would be accepted on one host and refused on another.
The server never resolves the zone anyway; the browser does, through
`Intl.DateTimeFormat`. So `AppUser.SetTimeZone` validates the *shape* of an IANA
id (`Area/Location`, allowing `America/Argentina/Buenos_Aires`, `Etc/GMT+5`,
`America/Port-au-Prince`) and accepts `UTC`, which is platform-independent and
matches what the client can actually consume. Windows ids are refused with a
message saying why.

If strict validation against the real tz database is ever wanted, it needs ICU:
set `InvariantGlobalization=false` for the API **and** pin
`CultureInfo.DefaultThreadCurrentCulture` to invariant at startup, or the app
starts honouring the host's locale for number formatting — the exact hazard
`CLAUDE.md` §6 warns about.

**Problem, for the rest.** `GET /api/auth/me` also returns `StartingBalance`,
`JournalStartedAt` and `DefaultRiskPerTrade`, and `GET /api/accounts` returns
`Name`, `IsActive` and `TrackedFrom` — none of which any endpoint can change. The
frontend renders those read-only with an apologetic note.

**Change, when picked up.**

| Endpoint | Body | Notes |
|---|---|---|
| `PATCH /api/auth/me` | `displayName`, `startingBalance`, `journalStartedAt`, `defaultRiskPerTrade` | `timeZoneId` already has its own endpoint; fold it in here only if the whole profile becomes one form. |
| `PATCH /api/accounts/{id}` | `name`, `isActive`, `trackedFrom` | Never `kind`, `venue` or `quoteAsset` — changing those retroactively invalidates every synced row. |

Both go through intention-revealing methods on the aggregates
(`user.UpdateProfile(...)`, `account.Rename(...)`, `account.Deactivate()`), never
setters — `CLAUDE.md` §6 "The domain model is rich, not a property bag". Check
whether `AppUser` and `Account` already expose suitable methods before adding new
ones.

Moving `TrackedFrom` backwards has sync consequences — it is the floor for
backfill. `CLAUDE.md` §8 requires asking before changing watermark semantics, so
either forbid moving it earlier or raise it explicitly.

---

## 4. Login's 401 is not a `ProblemDetails` from `ApiProblem` — nit

**Problem.** `src/TradeLedger.Api/Features/Auth/AuthEndpoints.cs:25` builds its
401 with `Results.Problem(...)` directly, so — alone among the API's failures —
it carries no `code` and no `traceId`:

```csharp
return Results.Problem(
    title: "Invalid credentials",
    statusCode: StatusCodes.Status401Unauthorized);
```

Every other failure in the API goes through `ApiProblem`, which stamps both. The
frontend has to special-case the login form so it doesn't try to read `code`.

**Change.** Throw `NotAuthenticatedException` (code `not_authenticated`, 401,
already handled by `GlobalExceptionHandler`), or call `ApiProblem.From` with an
explicit `invalid_credentials` code.

Keep the current behaviour of returning the same response for an unknown email
and a wrong password — that is deliberate and documented in the endpoint's
`.WithDescription()`. Do not add a code that distinguishes them.

**Frontend follow-up:** ~~once merged, the login form can use the shared error
mapper and Open Question 6 in `web/SPEC.md` closes.~~ — done. The form still
matches on the 401 status rather than the `invalid_credentials` code, so it keeps
working whichever way the endpoint shapes its response.

---

## 5. Features the frontend is skipping because no endpoint exists — deferred

Listed so the reason each is absent from the UI is recorded. Each needs its own
spec; none is a defect.

| Missing | What exists today | What a UI would need |
|---|---|---|
| **Attachments** | `Attachment` entity, config, and `AttachmentResponse` metadata on `TradeDetailResponse`. No route accepts or serves bytes. | `POST /api/trades/{id}/attachments` (multipart), `GET /api/attachments/{id}/content`, `DELETE`. Plus a storage decision (filesystem vs S3-compatible), size and content-type limits, and how bytes are authorised — `UserId` ownership must be enforced on the content route, not just the metadata. |
| **Editing a plan** | `POST /api/plans`, `GET /api/plans`, `POST /api/plans/{id}/abandon`, `POST /api/plans/calculate`. | `PATCH /api/plans/{id}` for a plan still in `Draft`/`Active`. |
| **Manually linking a plan to a trade** | `TradePlanMatcher` matches automatically during sync; `PlanResponse.LinkedTradeId` is read-only. | `POST /api/plans/{id}/link` taking a `tradeId`, going through `trade.AdoptPlan(plan)`. Needed when the matcher misses. |
| **Listing snapshots** | `POST /api/snapshots` only. The equity curve reads them but never exposes the rows. | `GET /api/snapshots?accountId=&from=&to=` returning `BalanceSnapshotResponse`, for auditing a suspicious equity curve. |
| **WebSocket live track** | `BitunixOptions` holds a WS URL; nothing consumes it. | The full live track from `CLAUDE.md` §4, including `ClientWebSocket.Options.Proxy` set from the same resolved egress endpoint. Until it exists, the frontend polls. |

---

## 6. `TradeDetailResponse` carries taxonomy names but not ids — nit

**Problem.** `TradeDetailResponse` exposes `StrategyName`, `TimeframeName`,
`EntryTypeName`, `ExitTypeName`, `EntryMentalStateName` and `ExitMentalStateName`
— names only. `JournalTradeRequest` takes `StrategyId`, `TimeframeId` and the
rest. So the response cannot round-trip into the request that produced it, and an
edit form has no id to pre-select its pickers with.

The frontend works around it by matching each name back against
`GET /api/taxonomy` within its kind (`JournalForm.findTermId`). That is sound as
long as names are unique per kind per user, which they are today. It breaks
quietly in one case: a term renamed after a trade was journalled no longer
matches, and the picker opens empty. The saved value is not lost — it is still on
the trade — but the form cannot show it, and saving then leaves it unchanged
because the journal endpoint ignores an absent field.

**Change, when picked up.** Add the ids alongside the names:

```csharp
Guid? StrategyId,
Guid? TimeframeId,
Guid? EntryTypeId,
Guid? ExitTypeId,
Guid? EntryMentalStateId,
Guid? ExitMentalStateId,
```

Additive, so nothing breaks. `TradeTermResponse` already does this correctly —
it carries `TermId` next to `Name`, which is why mistakes and trackings
round-trip without any name matching.

**Frontend follow-up:** once merged, `findTermId` and Open Question 7 in
`web/SPEC.md` both go away.

---

## Order of work

1. ~~**§1 CORS**~~ — done. Nothing in `web/` could be built or verified until it merged.
2. ~~**§2 MarketSession**~~ — done.
3. ~~**§4 login `ProblemDetails`**~~ — done.
4. **§3 settings write endpoints** — turns four read-only fields into real settings.
5. **§6 taxonomy ids on the trade detail** — additive, a few lines, and removes a
   name-matching workaround from the journal form.
6. **§5** — each on its own schedule.
