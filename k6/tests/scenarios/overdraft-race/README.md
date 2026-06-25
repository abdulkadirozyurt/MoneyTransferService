# Scenario 1 — Overdraft Race

## What this test tests
Checks consistency when many concurrent transfers try to spend the same limited balance.

The goal is to prove that the system does not allow overdraft or double-spend.

## How it tests it
1. Uses one sender account with exactly `100 TRY`.
2. Runs 20 concurrent iterations.
3. Each iteration tries to transfer `100 TRY` to a different receiver.
4. Expects exactly one successful transfer.
5. Expects the remaining requests to be rejected safely.

## Expected result
- Exactly one `200`/`201` success.
- Remaining requests should be `400` insufficient funds or `409` concurrency conflict.
- `500` must stay `0`.
- Unknown statuses must stay `0`.

## How to read failures
`400` and `409` are valid business outcomes in this race test.

More than one success means money consistency is broken.

Any `500` means server-side failure and must be investigated.

## Grafana / observability hints
Use the general Money Transfer Observability dashboard:

- `HTTP Status Distribution (Past 15m)`
- `Client Conflicts (409) Rate`
- `Server Errors (5xx) Rate`
- `Business Warnings Logs`
- `Server Errors & Fatal Exceptions Logs`
- Tempo traces for slow or failed requests with `k6-overdraft-race` correlation ids
