# Scenario 5 — Spike Traffic

## What this test tests
Measures behavior under sudden traffic shock.

This is not a steady load test. It checks whether the API, database path, and runtime stay stable when traffic jumps quickly.

## How it tests it
1. Uses 100 funded sender accounts.
2. Uses 100 receiver accounts.
3. Ramps to 200 virtual users in 5 seconds.
4. Holds spike traffic for 30 seconds.
5. Sends `POST /api/transactions/transfer` across account pools.
6. Treats `200`, `201`, and `409` as expected HTTP statuses.

## Expected result
- `200`/`201`: accepted transfer.
- `409`: acceptable contention during spike.
- `400`: unexpected because senders are funded.
- `500`: real server failure; must stay `0`.

## How to read failures
Spike tests can tolerate higher latency than sustained load tests.

Focus first on `500` and recovery behavior, then inspect p95/p99 latency.

## Grafana / observability hints
Use the general Money Transfer Observability dashboard:

- `Total HTTP Requests`
- `HTTP Request Rate by Route & Status`
- `HTTP Latency (p50, p95, p99)`
- `Server Errors (5xx) Rate`
- `App Exception Rate`
- `Memory Working Set & GC Heap Size`
- `ThreadPool & Active Threads`
- `Server Errors & Fatal Exceptions Logs`
