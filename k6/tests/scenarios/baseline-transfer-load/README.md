# Transfer Load — Hot Account Contention

## What this test tests
Measures transfer behavior under sustained contention between one sender account and one receiver account.

It checks throughput, latency, optimistic concurrency behavior, and server stability.

## How it tests it
1. Verifies fixed fixture accounts from `k6/setup/setup-scenario-data.sql`.
2. Runs 10 virtual users for 20 seconds.
3. Sends `POST /api/transactions/transfer` with unique idempotency keys.
4. Treats `200`, `201`, and `409` as expected HTTP statuses.

## Expected result
- `200`/`201`: accepted transfer.
- `409`: expected optimistic concurrency conflict.
- `400`: unexpected with seeded funded accounts.
- `500`: real server failure; must stay `0`.

## How to read failures
`409` is not server failure here. It means concurrency protection worked.

If `500` appears, inspect API logs and traces with `k6-transfer` correlation ids.

## Grafana / observability hints
Use the general Money Transfer Observability dashboard:

- `HTTP Request Rate by Route & Status`
- `HTTP Latency (p50, p95, p99)` filtered to `/api/transactions/transfer`
- `Client Conflicts (409) Rate`
- `Server Errors (5xx) Rate`
- `Business Warnings Logs`
- `Server Errors & Fatal Exceptions Logs`
