# MoneyTransferService — Claude Working Notes

Caveman mode: short. Clear. Why > what. Keep intent.

This file is living organism.
Update it when architecture/intent/decision changes.
Keep it aligned with current project goal.
Remove stale/wrong notes.
Do not let it become graveyard.

## Work Style

- Build only at meaningful checkpoints. Avoid continuous builds.
- Understand the problem first. If it is complex, create a plan before execution.
- Always decompose work into independent, testable units.

## Task Decomposition

- Every task must be split into independent components when possible.
- If dependencies exist, construct a dependency graph before execution and respect it.
- Parallel execution is allowed only when tasks are truly independent (no shared state, no shared schema, no file coupling).

### Parallel Task Rule

- If there are 2 or more independent tasks, spawn separate Tasks for each.
- Do NOT blindly parallelize based on count alone (e.g. “edit 3 files” is not automatically 3 tasks).
- Parallelization is only valid if tasks do not require coordination or sequential validation.

## Execution Model

- The main agent does not execute subtasks directly.
- The main agent is responsible for:
  - Planning
  - Task decomposition
  - Task delegation
  - Result validation and integration

- After each Task completes:
  - Validate output against expected behavior
  - If invalid, re-decompose or re-run the Task with corrected scope

## Workflow

1. Understand the requirement
2. Identify constraints and hidden dependencies
3. Decompose into tasks
4. Decide execution strategy (sequential vs parallel)
5. Spawn Tasks
6. Validate outputs
7. Integrate results
8. Proceed to next milestone

## Model Routing

### Opus (Planner / Architect)

Use for:

- System design and architecture decisions
- Cross-module refactoring plans
- Ambiguous or incomplete requirements
- Concurrency, distributed systems, and state consistency problems
- High-level reasoning about correctness and approach

Output should be limited to plans, reasoning, and decisions (not full implementation).

### Sonnet (Default Worker)

Use for:

- Feature implementation
- Bug fixing (local or medium complexity scope)
- API and service layer development
- Database logic (SQL / EF Core / MongoDB)
- Integration tasks

This is the default execution model.

### Haiku (Fast Worker)

Use for:

- Boilerplate generation
- DTO mapping
- Parsing, transformation, and formatting tasks
- Log analysis
- Small refactors
- File scanning and extraction tasks

### Haiku (Router Mode)

Use for:

- Task classification (deciding between Opus / Sonnet / Haiku)
- Context reduction and filtering
- Breaking work into subtasks

This is the cost optimization layer.

### Small Fast Model

Use for:

- Trivial read-only queries
- Single-step deterministic commands
- Tasks requiring no reasoning or planning

## Constraints

- Opus must never be used as an execution worker.
- Sonnet is the default execution layer.
- Haiku is used for routing, parallel lightweight tasks, and repetitive work.
- Parallel execution is only allowed for stateless, independent tasks.
- Any shared state, shared schema, or shared file requires sequential execution.

## Key Principle

Optimize for correctness first, then cost, then speed. Never sacrifice correctness for parallelism or model cost reduction.

## Current architecture goal

Goal: clean layered money transfer service.

Layers:

- WebAPI = HTTP contract + endpoint routing.
- Business = use-case input, validation, rules, service logic.
- DataAccess = EF/Mongo persistence.
- Entities = persisted domain entities.
- Core = shared abstractions/constants.

Keep layers separated.
Do not leak WebAPI DTO into Business.
Do not expose Entity as API contract unless intentional.

## Request / DTO decision

Decision: keep API request model and Business command model separate.

Why:

- API contract = external shape.
- Business command = use-case input.
- They may look same today, can diverge later.
- Prevent over-posting / accidental field exposure.
- Keep Business independent from HTTP layer.

Pattern:

```text
WebAPI.Contracts.CreateTransferRequest
  -> map in endpoint
Business.Requests.TransferCommand
  -> validated/used by TransactionService
```

Do not pass `CreateTransferRequest` into Business service.

## Transaction vs Transfer language

Decision: `Transaction` is main resource/domain aggregate.

Why:

- Service name is `TransactionService`.
- Entity is `Transaction`.
- Future operations: transfer, deposit, withdraw.
- All are transaction types/operations.

Decision: `Transfer` remains operation/use-case name.

Why:

- Current implemented action is money transfer.
- Transfer has sender + receiver account.
- Deposit/withdraw will have different rules/commands later.

Naming rule:

- Resource/query/entity/root endpoint → `Transaction`.
- Operation/command/rules specific to transfer → `Transfer`.

Examples:

```text
TransactionService
ITransactionService
TransactionResponse
GetTransactionByIdAsync
GetTransactionHistoryAsync
```

```text
TransferAsync
TransferCommand
TransferCommandValidator
TransferBusinessRules
```

Do not blindly rename every `Transfer*` to `Transaction*`.
Only rename when meaning is resource-level, not operation-level.

## API routing decision

Decision: API prefix exists.

```text
/api
```

Decision: main transaction root:

```text
/api/transactions
```

Decision: operations below transaction root:

```text
POST /api/transactions/transfer
future: POST /api/transactions/deposit
future: POST /api/transactions/withdraw
```

Why:

- Transaction is resource family.
- Transfer/deposit/withdraw are actions that create transaction records.
- Future extension clearer.

Program routing pattern:

```csharp
var api = app.MapGroup("/api");
api.MapAccountEndpoints();
api.MapCustomerEndpoints();
api.MapTransactionEndpoints();
```

This means:

```text
/api/accounts
/api/customers
/api/transactions
```

## Endpoint style

Decision: endpoint classes should stay thin.

Endpoint responsibilities:

- accept API DTO.
- map DTO -> Business command.
- call service.
- return success response.

Endpoint should not:

- contain business rule logic.
- contain repeated try/catch for known business errors.
- know persistence details.

## Global exception handling decision

Decision: central error handling via `GlobalExceptionHandler`.

Why:

- Endpoint try-catch blocks duplicated.
- Response format inconsistent.
- Error mapping belongs in one place.
- Logs + `ProblemDetails` consistent.

Pattern:

- Business errors throw `BusinessException` subclasses.
- Validation errors throw `FluentValidation.ValidationException`.
- Unknown errors -> 500 generic response.

Endpoint should let exceptions bubble.
Global handler maps them.

## BusinessException decision

Decision: custom business exceptions inherit from `BusinessException`.

Why:

- Each business error owns HTTP status.
- Global handler can map all business errors generically.
- Endpoint no longer needs exception switch/catch.

Pattern:

```csharp
BusinessException(HttpStatusCode statusCode, string message, Exception? innerException = null)
```

Examples:

- invalid request -> 400.
- not found -> 404.
- conflict/business rule -> 409.

Be careful:

- Real DB/system failures should usually be 500.
- If exception means domain conflict, 409 okay.

## ValidationException decision

Decision: FluentValidation exceptions handled globally.

Why:

- Validation response should be standard.
- Use `HttpValidationProblemDetails`.
- Client gets `errors` dictionary.
- Endpoint stays clean.

Pattern:

- `ValidationException` -> 400.
- Include traceId.
- Use `errors` property.

## ProblemDetails response decision

Decision: use `ProblemDetails` / `HttpValidationProblemDetails`.

Why:

- Standard API error shape.
- Easier client handling.
- Avoid random `{ error = ... }` bodies.
- Good .NET practice.

Include:

- `status`
- `title`
- `detail`
- `instance`
- `traceId`

Hide internal exception details for unknown/system errors.
Expose safe business messages only.

## Logging / observability decision

Current status:

- Serilog is integrated in WebAPI.
- Bootstrap logger writes console logs before app build.
- Runtime logger writes structured compact JSON to console.
- MongoDB operational logs go to `ApplicationLogs` collection when `ConnectionStrings:MongoDb` exists.
- Business audit remains separate in `TransactionAuditLogs`.
- Correlation ID middleware exists.
- `X-Correlation-Id`, response header, `HttpContext.TraceIdentifier`, ProblemDetails `correlationId`, ProblemDetails `traceId`, and Serilog log context should line up.
- Serilog request logging is enabled via `UseConfiguredSerilogRequestLogging()`.
- Global exception handler logs expected validation/business failures as warning and unknown/system failures as error.
- Health checks exist:
  - `/health/live` = process liveness only, no dependencies.
  - `/health/ready` = all registered readiness checks.
- OpenAPI + Scalar are currently always mapped, not dev-only.
- OpenTelemetry is integrated:
  - ASP.NET Core tracing/metrics.
  - HttpClient tracing/metrics.
  - EF Core tracing.
  - SqlClient tracing.
  - Runtime metrics.
  - Console exporter can be enabled via config.
  - OTLP exporter can be enabled via config.

Why:

- Business/validation errors are expected client/use-case failures, not server bugs.
- Unknown exceptions are server bugs/failures.
- Stress test analysis needs correlation across k6 result, HTTP request log, exception log, DB state, and traces.

Config keys:

```text
OpenTelemetry:ConsoleExporterEnabled
OpenTelemetry:OtlpExporterEnabled
OpenTelemetry:TraceGrpcEndpoint
OpenTelemetry:MetricsHttpEndpoint
```

Docker compose currently wires:

```text
api -> Tempo gRPC trace endpoint: http://tempo:4317
api -> Prometheus OTLP metrics endpoint: http://prometheus:9090/api/v1/otlp/v1/metrics
OTEL_SERVICE_NAME=money-transfer-api
```

Important interpretation rule:

- k6 alone is only load generation + client-side metrics.
- Serilog logs are useful only if queried during/after the run.
- OpenTelemetry is useful only if exported to a collector/backend or intentionally inspected in console.
- HTML k6 report is not enough to diagnose root cause by itself.

Stress test evidence should combine:

```text
k6 summary/report
MongoDB ApplicationLogs
SQL account/transaction state
Grafana dashboard
Prometheus metrics
Tempo traces
Loki logs
Docker container health/resources
```

## Observability stack decision

Current local stack:

```text
Prometheus = metrics backend + OTLP metrics receiver
Tempo = trace backend + OTLP gRPC receiver
Loki = log backend
Alloy = Docker log collector
Grafana = dashboards + datasource provisioning
```

Provisioned files:

```text
observability/prometheus/prometheus.yml
observability/tempo/tempo.yml
observability/alloy/config.alloy
observability/grafana/provisioning/datasources/datasources.yml
observability/grafana/provisioning/dashboards/dashboards.yml
observability/grafana/dashboards/money-transfer-observability.json
```

Grafana datasources:

```text
Prometheus uid = prometheus
Tempo uid = tempo
Loki uid = loki
```

Trace/log linking intent:

- Tempo links traces to Loki logs by trace id.
- Loki derived field links logs back to Tempo.
- Query targets `service_name="money-transfer-api"`.
- Keep `TraceId` field naming compatible with Serilog/OpenTelemetry output.

## k6 load test decision

Current k6 structure:

```text
k6/lib/html-report.js
k6/lib/seed-helper.js
k6/setup/setup-scenario-data.sql
k6/tests/smoke/smoke.js
k6/tests/transfer-load/transfer-load.js
k6/tests/scenarios/hotspot-load/hotspot-load.js
k6/tests/scenarios/overdraft-race/overdraft-race.js
k6/tests/scenarios/spike-traffic/spike-traffic.js
```

Scenario intent:

- `smoke` = basic API/health confidence.
- `transfer-load` = baseline transfer throughput.
- `hotspot-load` = contention on popular accounts.
- `overdraft-race` = concurrency/race check around insufficient funds.
- `spike-traffic` = sudden traffic burst behavior.

Setup decision:

- Scenario tests should use seeded, known account data.
- Use `k6/setup/setup-scenario-data.sql` for scenario data shape.
- Use `seed-helper.js` for reusable setup helpers.

Report decision:

- k6 HTML report is useful for client-side summary.
- It is not root-cause evidence alone.
- Pair it with logs, traces, metrics, SQL state, and Mongo audit/application logs.

## Test organization decision

Observation:

- Large test file (~500 lines) is not ideal.
- But skip splitting for now.

Future preference:

```text
TransactionServiceTests/
  TransferAsyncTests.cs
  GetTransactionByIdTests.cs
  GetTransactionHistoryTests.cs
  TransactionServiceTestBase.cs
```

Why:

- Easier read.
- Less setup repetition.
- Future deposit/withdraw tests will not explode one file.

But current task: skip.

## Current known refactor state

Completed intended changes:

- `ITransferService` -> `ITransactionService`.
- `TransferEndpoints` -> `TransactionEndpoints`.
- `TransferContracts` -> `TransactionContracts`.
- `TransferResponse` -> `TransactionResponse`.
- `FromTransfer` -> `FromTransaction`.
- `GetTransferByIdAsync` -> `GetTransactionByIdAsync`.
- `GetTransferHistoryAsync` -> `GetTransactionHistoryAsync`.
- `TransferRequest` became `TransferCommand`.
- `TransferRequestValidator` -> `TransferCommandValidator`.
- EF migration exists for transfer -> transaction rename.

Keep `TransferCommand` because operation is transfer.
Keep `TransferBusinessRules` because rules are transfer-specific.
Keep `TransferStatus` for now unless/until statuses become operation-neutral transaction lifecycle language.

Current known naming debt:

- `TransactionService` still has resource-level locals/helper names using transfer language:
  - `existingTransfer` should become `existingTransaction`.
  - `GetExistingTransferAsync` should become `GetExistingTransactionAsync`.
  - `CompleteTransferAsync` should become `CompleteTransactionAsync`.
  - `CreatePendingTransfer` should become `CreatePendingTransaction`.
  - `CreateFailedTransfer` should become `CreateFailedTransaction`.
  - `transferRepository` parameter should become `transactionRepository`.
  - typo `transcationRepository` should become `transactionRepository`.
- Unit tests still use old class/file naming:
  - `TransferServiceTests.cs` should become transaction-service oriented when test split is done.
  - Transfer operation test method names can keep transfer wording.

## Endpoint metadata decision

Current state:

- Endpoint groups use `.WithTags(...)` and `.WithName(...)`.
- Transaction endpoints do not yet declare typed response metadata.
- `GetTransactionByIdAsync` still returns ad-hoc `{ error = "Transaction not found." }` for 404.

Preferred next endpoint cleanup:

```text
Produces<TSuccess>
ProducesProblem
ProducesValidationProblem
```

Then avoid ad-hoc error bodies where global ProblemDetails or typed endpoint result is better.

## Next likely tasks

1. Clean naming inside `TransactionService`:
   - `existingTransfer` -> `existingTransaction`.
   - `GetExistingTransferAsync` -> `GetExistingTransactionAsync`.
   - `CompleteTransferAsync` -> `CompleteTransactionAsync`.
   - `CreatePendingTransfer` -> `CreatePendingTransaction`.
   - `CreateFailedTransfer` -> `CreateFailedTransaction`.
   - `transcationRepository` typo -> `transactionRepository`.

2. Add endpoint metadata:
   - `Produces`
   - `ProducesProblem`
   - `ProducesValidationProblem`

3. Decide 404 style for `GetTransactionByIdAsync`:
   - use `Results.NotFound()` with no body, or
   - route not-found through ProblemDetails consistently.

4. Later: design deposit/withdraw:

```text
DepositCommand
WithdrawCommand
DepositAsync
WithdrawAsync
DepositBusinessRules
WithdrawBusinessRules
```

## Important guardrails

- Do not over-rename.
- Ask if domain language unclear.
- Keep transaction vs transfer distinction.
- Keep endpoint thin.
- Keep errors centralized.
- Keep DTOs separated by layer.
- Mention next task after completing any work.
