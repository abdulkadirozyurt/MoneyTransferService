# Scenario 2 — Hotspot Load

## What this test tests
Measures sustained fan-in writes to one receiver account.

Many funded senders transfer to the same receiver, creating a database hotspot. This reveals receiver-row contention, optimistic concurrency conflicts, latency growth, and server stability.

## How it tests it
1. Uses 50 funded sender accounts.
2. Uses one shared receiver account.
3. Ramps to 50 virtual users.
4. Sends back-to-back `POST /api/transactions/transfer` requests with no sleep.
5. Treats `200`, `201`, and `409` as expected HTTP statuses.

## Expected result
- `200`/`201`: accepted transfer.
- `409`: expected contention on the hot receiver.
- `400`: unexpected because senders are funded.
- `500`: real server failure; must stay `0`.

## How to read failures
`409` is expected signal, not server crash.

If p95/p99 grows while `409` grows and `500` stays `0`, the system is protecting consistency under contention.

If `500` appears, inspect API logs, ApplicationLogs, and Tempo traces with `k6-hotspot-load` correlation ids.

## Grafana / observability hints
Use the general Money Transfer Observability dashboard:

- `HTTP Request Rate by Route & Status`
- `HTTP Latency (p50, p95, p99)` filtered to `/api/transactions/transfer`
- `Client Conflicts (409) Rate`
- `Server Errors (5xx) Rate`
- `Memory Working Set & GC Heap Size`
- `ThreadPool & Active Threads`
- `Business Warnings Logs`
- `Server Errors & Fatal Exceptions Logs`
