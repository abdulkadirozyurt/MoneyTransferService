# Smoke Test — Health Readiness

## What this test tests
Checks whether the API is ready before load, race, or spike tests run.

It calls `/health/ready`, which includes SQL Server and MongoDB readiness checks.

## How it tests it
1. Runs 1 virtual user for 5 iterations.
2. Sends `GET /health/ready`.
3. Expects HTTP `200`.
4. Expects response body `Healthy`.

## Expected result
- `200` responses only.
- `http_req_failed` should be `0%`.
- All checks should pass.

## How to read failures
Any failure here is real. Do not start load tests until readiness is stable.

## Grafana / observability hints
Use the general Money Transfer Observability dashboard:

- `Readiness Status (/health/ready)`
- `HTTP Latency (p50, p95, p99)` filtered to `/health/ready`
- `Server Errors & Fatal Exceptions Logs`
