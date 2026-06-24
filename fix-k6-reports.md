# Task: Improve Grafana k6 Performance Test Suite and Bilingual HTML Reporting

You are working on an existing Grafana k6 performance testing project.

Your goal is to inspect, refactor, and improve the current k6 test scripts and shared reporting utilities so that the project produces mature, reliable, business-aware, bilingual performance reports in both **English** and **Turkish**.

Do not rewrite the entire project from scratch. Preserve the current structure and intent of the tests, but fix the identified gaps, improve consistency, remove duplication where appropriate, and make the HTML reporting output more complete and CI-friendly.

---

## Existing Files to Review

The project contains the following k6 test scripts:

```text
k6/tests/smoke/smoke.js
k6/tests/transfer-load/transfer-load.js
k6/tests/scenarios/overdraft-race/overdraft-race.js
k6/tests/scenarios/hotspot-load/hotspot-load.js
k6/tests/scenarios/spike-traffic/spike-traffic.js
```

The project also contains the following helper/setup files:

```text
k6/lib/html-report.js
k6/lib/seed-helper.js
k6/setup/setup-data.sql
k6/setup/setup-scenario-data.sql
```

---

## Current Assessment Summary

The existing k6 suite is partially mature:

### Strengths

- A shared `html-report.js` exists and already generates bilingual EN/TR HTML reports.
- Several scripts already use `handleSummary()`.
- Business-aware reporting exists in some scenarios.
- Some scripts use setup gates through fixture/account validation.
- Status code counters are already collected in multiple scripts.
- Correlation IDs and idempotency keys are used in transfer-related scenarios.
- Phase-tagged thresholds exist in some load scenarios.

### Main Problems to Fix

The current project is not yet fully suitable for complete performance reporting because:

- `summaryTrendStats` is missing from all scripts.
- Median, p90, p99, and detailed percentile data are missing from the reports.
- RPS / throughput is not clearly displayed.
- Peak RPS is not available or not shown.
- Latency breakdown is missing:
  - blocked
  - connecting
  - TLS handshaking
  - sending
  - waiting
  - receiving
- Request-level `tags.name` is missing.
- Endpoint-level analysis is not possible.
- “Top slow endpoint” and “top failing endpoint” style summaries are not possible yet.
- Threshold sets are too weak.
- `smoke.js` and `overdraft-race.js` have no thresholds.
- Most checks only validate HTTP status codes.
- Response body and business validation are missing in most scripts.
- Setup/fixture validation is inconsistent.
- `trackStatus`, transfer params, headers, and request helper logic are duplicated.
- Metadata is weak:
  - BASE_URL missing from report
  - environment missing
  - test type missing
  - build/commit info missing
  - scenario name missing
- `handleSummary()` only generates HTML; raw JSON summary output is missing.
- There is no production safety guard to prevent accidental load/spike tests against production.
- Some wording in the previous report had minor language issues that should not appear in final reporting.

---

## High-Level Goal

After your changes, the project should be able to generate:

1. A bilingual HTML report in **English and Turkish**
2. A raw JSON summary file for CI/CD and future dashboard integrations
3. Rich percentile metrics:
   - avg
   - min
   - med
   - max
   - p90
   - p95
   - p99
4. RPS and throughput information
5. Latency breakdown
6. Endpoint/request-name based visibility
7. Stronger thresholds
8. Business-aware validations
9. Safer execution through production guards
10. Cleaner and less duplicated k6 code

---

## Important Constraints

Follow these constraints carefully:

1. Do not remove existing test intent.
2. Do not remove bilingual HTML reporting.
3. HTML report content must support both:
   - English
   - Turkish
4. Do not convert the project to another test framework.
5. Keep using Grafana k6.
6. Do not introduce unnecessary external dependencies unless already used by the project.
7. Avoid large rewrites unless needed.
8. Prefer small, clear, maintainable changes.
9. Keep scripts understandable for a junior developer.
10. Avoid hiding business logic inside overly generic abstractions.
11. Preserve existing environment variable usage where possible.
12. If a script intentionally allows certain 4xx responses, do not treat them as technical failures without business context.
13. Be careful with `http_req_failed`; some scenarios may intentionally produce `400` or `409` as valid business outcomes.
14. Use custom counters/rates for business outcomes when needed.
15. Do not blindly fail tests on every 4xx response if the scenario expects conflict or validation responses.
16. Do not fake data.
17. Do not assume unavailable endpoints or response schemas. Inspect existing code before modifying checks.
18. If response body validation is not possible due to missing schema certainty, add conservative checks and clear TODO comments.

---

## Required Improvements

---

# 1. Add `summaryTrendStats` to All k6 Scripts

Every k6 script must define `summaryTrendStats`.

Use a consistent set like:

```js
summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)']
```

Apply this to all scripts:

```text
k6/tests/smoke/smoke.js
k6/tests/transfer-load/transfer-load.js
k6/tests/scenarios/overdraft-race/overdraft-race.js
k6/tests/scenarios/hotspot-load/hotspot-load.js
k6/tests/scenarios/spike-traffic/spike-traffic.js
```

The HTML report should display these values clearly.

Turkish labels should include equivalents such as:

```text
Average / Ortalama
Minimum / Minimum
Median / Medyan
Maximum / Maksimum
p90 / p90
p95 / p95
p99 / p99
```

---

# 2. Improve Threshold Coverage

Create stronger threshold sets for each script.

Do not use the exact same thresholds blindly for every scenario. Thresholds should reflect the purpose of each test.

## Smoke Test Thresholds

For `k6/tests/smoke/smoke.js`, add lightweight readiness thresholds.

Example intent:

```js
thresholds: {
  checks: ['rate==1'],
  http_req_duration: ['p(95)<500'],
  http_req_failed: ['rate==0']
}
```

If the project’s expected latency is different, choose a reasonable value and document it in a comment.

## Transfer Load Thresholds

For `k6/tests/transfer-load/transfer-load.js`, add:

- duration threshold
- check success threshold
- server error threshold
- business success threshold if applicable

Do not treat expected business conflicts as technical failures unless the scenario expects all transfers to succeed.

Use custom metrics if necessary, for example:

```js
const serverErrors = new Counter('server_errors');
const validBusinessResponses = new Rate('valid_business_responses');
```

Possible threshold style:

```js
thresholds: {
  http_req_duration: ['p(95)<500', 'p(99)<1000'],
  checks: ['rate>0.95'],
  server_errors: ['count==0'],
  valid_business_responses: ['rate>0.95']
}
```

## Overdraft Race Thresholds

For `k6/tests/scenarios/overdraft-race/overdraft-race.js`, add thresholds that reflect the business rule:

- exactly one successful transfer should occur
- server errors should be zero
- checks should pass
- latency should stay within an acceptable range

Important:

The final “exactly one success” rule may not be directly expressible as a simple k6 threshold if it depends on final aggregated counters. If needed, preserve the existing deterministic verdict logic in the HTML report and add custom counters/rates for visibility.

Do not incorrectly convert expected rejected transfers into failures.

## Hotspot Load Thresholds

For `k6/tests/scenarios/hotspot-load/hotspot-load.js`, add:

- load-phase latency threshold
- check rate threshold
- server error count threshold
- business valid response threshold

Preserve existing phase-tagged threshold logic, for example:

```js
'http_req_duration{phase:load}': ['p(95)<1000', 'p(99)<2000']
```

Adjust values only if the current script already implies other expectations.

## Spike Traffic Thresholds

For `k6/tests/scenarios/spike-traffic/spike-traffic.js`, add spike-appropriate thresholds:

- p95/p99 latency for load phase
- server error count threshold
- check rate threshold
- business valid response rate

Spike tests may tolerate higher latency than normal load tests, but should not tolerate uncontrolled 500 errors.

Example intent:

```js
'http_req_duration{phase:load}': ['p(95)<3000', 'p(99)<5000'],
server_errors: ['count==0'],
checks: ['rate>0.90']
```

---

# 3. Add Request Tags for Endpoint-Level Analysis

Every HTTP request must include meaningful tags.

At minimum, add:

```js
tags: {
  name: 'POST /api/transactions/transfer',
  method: 'POST',
  endpoint: '/api/transactions/transfer'
}
```

For smoke:

```js
tags: {
  name: 'GET /health',
  method: 'GET',
  endpoint: '/health'
}
```

Use the actual endpoints from the existing code.

Do not guess endpoint paths. Inspect the files first.

The goal is to make these possible in the report:

- endpoint/request-name based grouping
- top slow request
- top failing request
- request-level thresholds in the future

---

# 4. Improve Checks Beyond Status-Only Validation

Most scripts currently check only HTTP status codes. Improve this carefully.

## Smoke Test

Keep both:

- status check
- body check

For example:

```js
check(res, {
  'status is 200': (r) => r.status === 200,
  'body is Healthy': (r) => r.body === 'Healthy'
});
```

Also make sure failed smoke checks are visible in the HTML report.

## Transfer Load

Add conservative response validation.

If a successful transfer response has a known transaction ID field, validate it.

Examples:

```js
'response has transaction id': (r) => {
  try {
    const body = r.json();
    return Boolean(body.id || body.transactionId);
  } catch {
    return false;
  }
}
```

Only add this if the existing response format supports it or can be reasonably inferred from the code/tests.

If the schema is uncertain, add a guarded check and comment:

```js
// TODO: Confirm final transfer response schema with API contract.
```

## Overdraft Race

Preserve the existing business expectation:

```text
Exactly one transfer should succeed.
Other competing transfers should be rejected consistently.
```

Improve visibility by adding checks/custom metrics for:

- successful transfer count
- rejected transfer count
- server error count
- valid business outcome count

Do not classify expected conflict/rejection responses as test failures if they prove correct race-condition handling.

## Hotspot Load and Spike Traffic

Add checks for:

- valid successful responses
- valid business rejections, if expected
- server errors are not considered valid
- malformed/unexpected responses are not valid

---

# 5. Standardize Custom Metrics

Create or reuse common custom metrics where appropriate.

Recommended metrics:

```js
import { Counter, Rate } from 'k6/metrics';

export const status200 = new Counter('status_200');
export const status201 = new Counter('status_201');
export const status400 = new Counter('status_400');
export const status409 = new Counter('status_409');
export const status500 = new Counter('status_500');
export const statusOther = new Counter('status_other');

export const serverErrors = new Counter('server_errors');
export const validBusinessResponses = new Rate('valid_business_responses');
export const businessFailures = new Counter('business_failures');
```

However, do not export metrics from a helper if k6 module behavior or project style makes this awkward. The most important goal is consistency and clarity.

If `trackStatus()` is duplicated in multiple scripts, move it to `k6/lib/seed-helper.js` or another shared helper file.

Example helper intent:

```js
export function trackStatus(res, counters) {
  if (res.status === 200) counters.status200.add(1);
  else if (res.status === 201) counters.status201.add(1);
  else if (res.status === 400) counters.status400.add(1);
  else if (res.status === 409) counters.status409.add(1);
  else if (res.status >= 500) counters.status500.add(1);
  else counters.statusOther.add(1);
}
```

If adding a shared helper, keep it simple and readable.

---

# 6. Add Raw JSON Summary Output

Each script’s `handleSummary(data)` should return both:

1. HTML report
2. JSON summary

Example:

```js
export function handleSummary(data) {
  return {
    [REPORT_PATH]: htmlReport(data, reportOptions),
    [JSON_REPORT_PATH]: JSON.stringify(data, null, 2)
  };
}
```

Add an environment variable for JSON path:

```js
const JSON_REPORT_PATH = __ENV.JSON_REPORT_PATH || 'k6-summary.json';
```

If each test already has a unique report path, derive JSON path consistently.

Example:

```js
const REPORT_PATH = __ENV.REPORT_PATH || 'reports/transfer-load.html';
const JSON_REPORT_PATH = __ENV.JSON_REPORT_PATH || REPORT_PATH.replace(/\.html$/, '.summary.json');
```

Make sure this works even if the report path does not end with `.html`.

The JSON output is required for:

- CI parsing
- trend tracking
- future dashboards
- automated regression detection

---

# 7. Improve HTML Report Data and Layout

Update `k6/lib/html-report.js` so that the generated HTML report includes richer information.

The report must remain bilingual:

```text
English + Turkish
```

Do not remove existing bilingual text. Improve it.

## Required Report Sections

The HTML report should include the following sections:

### 1. Header / Başlık

Show:

- test name
- test type
- timestamp
- verdict
- environment
- base URL
- report language support: EN/TR

### 2. Configuration / Konfigürasyon

Show:

- BASE_URL
- ENVIRONMENT
- TEST_TYPE
- scenario name
- VUs
- iterations
- duration
- stages, if available
- build ID, if available
- commit SHA, if available
- report generation timestamp

Read these from environment variables where possible:

```js
__ENV.BASE_URL
__ENV.ENVIRONMENT
__ENV.TEST_TYPE
__ENV.BUILD_ID
__ENV.COMMIT_SHA
__ENV.REPORT_PATH
__ENV.JSON_REPORT_PATH
```

### 3. Summary Metrics / Özet Metrikler

Show:

- total requests
- failed request rate
- checks rate
- average duration
- median duration
- p90
- p95
- p99
- max duration
- RPS average
- data sent
- data received

### 4. Percentiles / Yüzdelikler

Show percentile metrics from `http_req_duration`:

- p90
- p95
- p99

If values are missing, display a clear fallback:

```text
Not available / Mevcut değil
```

### 5. Throughput / Trafik Hacmi

Show:

- average requests per second
- total requests
- data sent
- data received

If possible, estimate or display peak RPS. If peak RPS cannot be calculated reliably from k6 summary data alone, do not fake it. Instead show:

```text
Peak RPS requires time-series output such as JSON/InfluxDB/Prometheus remote write.
Tepe RPS için JSON/InfluxDB/Prometheus remote write gibi zaman serisi çıktısı gerekir.
```

### 6. Latency Breakdown / Gecikme Dağılımı

Show available metrics:

- `http_req_blocked`
- `http_req_connecting`
- `http_req_tls_handshaking`
- `http_req_sending`
- `http_req_waiting`
- `http_req_receiving`

For each, show:

- avg
- med
- p90
- p95
- p99
- max

If a metric is unavailable, display a bilingual fallback.

### 7. Status Code Distribution / Durum Kodu Dağılımı

Show counters:

- 200
- 201
- 400
- 409
- 500
- other

If a counter is not present, display zero or unavailable safely.

### 8. Business Validation / İş Kuralı Doğrulama

Show:

- valid business response rate
- business failure count
- expected outcome explanation
- actual observed outcome
- pass/fail result

Examples:

```text
Expected: Exactly one transfer succeeds.
Beklenen: Tam olarak bir transfer başarılı olmalı.
```

```text
Expected: No server errors during spike.
Beklenen: Spike sırasında sunucu hatası olmamalı.
```

### 9. Thresholds / Eşikler

Show:

- threshold name
- condition
- pass/fail status
- bilingual explanation

### 10. Checks / Kontroller

Show:

- check names
- pass/fail rate
- failed checks if available

### 11. Interpretation / Yorum

Keep the existing business-aware interpretation style, but make it more precise.

For each test type, explain what the result means.

Examples:

```text
English:
The test passed the technical latency target, but the business validation rate is below the expected threshold.

Turkish:
Test teknik gecikme hedefini geçti; ancak iş kuralı doğrulama oranı beklenen eşiğin altında.
```

### 12. Recommended Next Actions / Önerilen Sonraki Aksiyonlar

Show actionable next steps based on failures.

Examples:

- investigate server errors
- inspect slow endpoint
- check database contention
- verify account fixture data
- compare p99 latency against previous build
- review rejected transfer reason codes

---

# 8. Add Metadata to Report Options

Update each script’s `handleSummary()` call so it passes metadata into `htmlReport()`.

Example intent:

```js
const reportOptions = {
  title: 'Transfer Load Test',
  testName: 'Transfer Load Test',
  testType: 'load',
  language: 'en-tr',
  baseUrl: BASE_URL,
  environment: __ENV.ENVIRONMENT || 'local',
  buildId: __ENV.BUILD_ID || 'unknown',
  commitSha: __ENV.COMMIT_SHA || 'unknown',
  reportPath: REPORT_PATH,
  jsonReportPath: JSON_REPORT_PATH,
  expectedOutcome: 'Transfers should complete without server errors and with valid business responses.'
};
```

Turkish equivalent should be included either inside `html-report.js` or passed in options:

```js
expectedOutcomeTr: 'Transfer işlemleri sunucu hatası olmadan ve geçerli iş kuralı yanıtlarıyla tamamlanmalıdır.'
```

---

# 9. Add Production Safety Guard

Add a safety guard to all non-smoke load/spike/race tests.

The purpose is to prevent accidental execution against production environments.

Use environment variables such as:

```js
ALLOW_PROD_LOAD_TEST
ENVIRONMENT
BASE_URL
```

Suggested behavior:

- If `ENVIRONMENT=production` or `BASE_URL` appears to target production
- And `ALLOW_PROD_LOAD_TEST !== 'true'`
- Then abort the test before load starts

Example intent:

```js
function assertSafeTarget(baseUrl) {
  const environment = (__ENV.ENVIRONMENT || '').toLowerCase();
  const allowProd = __ENV.ALLOW_PROD_LOAD_TEST === 'true';

  const looksProduction =
    environment === 'prod' ||
    environment === 'production' ||
    /prod|production/i.test(baseUrl);

  if (looksProduction && !allowProd) {
    throw new Error(
      'Refusing to run load test against a production-like target. Set ALLOW_PROD_LOAD_TEST=true to override intentionally.'
    );
  }
}
```

Use this guard in:

```text
k6/tests/transfer-load/transfer-load.js
k6/tests/scenarios/overdraft-race/overdraft-race.js
k6/tests/scenarios/hotspot-load/hotspot-load.js
k6/tests/scenarios/spike-traffic/spike-traffic.js
```

For `smoke.js`, either skip the guard or make it non-blocking, because smoke checks may be valid in production.

Add Turkish explanation in the HTML report metadata or interpretation:

```text
Üretim benzeri hedeflere yanlışlıkla yük testi gönderilmesini önlemek için güvenlik kontrolü uygulanır.
```

---

# 10. Improve Setup and Fixture Validation

Review the current usage of `seed-helper.js`.

Ensure that scripts depending on accounts or balances validate their fixture data before generating load.

Specifically review:

```text
k6/tests/transfer-load/transfer-load.js
```

It currently uses hardcoded account IDs and does not appear to use setup fixture validation consistently.

Improve it by using the existing fixture validation helper if possible.

Expected behavior:

- If required accounts are missing, fail early.
- If account balance/state is invalid, fail early.
- Report setup failure clearly.
- Avoid misleading test results caused by bad fixture data.

Do not invent account IDs. Use existing fixture data from the SQL files or existing helper conventions.

---

# 11. Refactor Duplication Carefully

The following logic appears duplicated across scripts:

- `trackStatus`
- transfer request params
- headers
- correlation ID creation
- idempotency key creation
- common metadata
- JSON report path derivation
- production guard

Refactor duplication into shared helper functions where appropriate.

Possible shared helper file:

```text
k6/lib/seed-helper.js
```

or create:

```text
k6/lib/k6-utils.js
```

Only create a new helper file if it improves clarity.

Suggested helpers:

```js
export function createCorrelationId(prefix) {}

export function createTransferParams(correlationId, extraTags = {}) {}

export function deriveJsonReportPath(reportPath) {}

export function assertSafeLoadTarget(baseUrl) {}

export function buildReportMetadata(overrides = {}) {}

export function trackStatus(res, counters) {}
```

Keep helpers simple and easy to read.

---

# 12. Scenario-Specific Expectations

## `k6/tests/smoke/smoke.js`

Improve it as a readiness gate.

Required changes:

- Add `summaryTrendStats`
- Add basic thresholds
- Add request tags
- Preserve status and body checks
- Add metadata to report
- Add JSON summary output
- Keep it simple

Expected score after improvement: high.

---

## `k6/tests/transfer-load/transfer-load.js`

Improve it as a real load test.

Required changes:

- Add `summaryTrendStats`
- Add stronger thresholds
- Add request tags
- Add setup/fixture validation
- Add JSON summary output
- Add production guard
- Improve response/business validation
- Reduce duplicated status tracking
- Add report metadata

Special care:

- Do not treat valid business rejections as technical failures unless the scenario expects every transfer to succeed.
- If all transfers are expected to succeed, document that clearly.

---

## `k6/tests/scenarios/overdraft-race/overdraft-race.js`

Improve it as a race-condition/business correctness test.

Required changes:

- Add `summaryTrendStats`
- Add thresholds
- Add request tags
- Add JSON summary output
- Add production guard
- Preserve the deterministic rule:
  - exactly one success
- Improve reporting for final balance validation
- Add custom metrics for:
  - successful transfers
  - rejected transfers
  - server errors
  - valid business outcomes
- Keep setup and teardown validation

Important:

Do not turn expected rejected transfers into failed requests incorrectly.

---

## `k6/tests/scenarios/hotspot-load/hotspot-load.js`

Improve it as a sustained hotspot load test.

Required changes:

- Add `summaryTrendStats`
- Preserve stages
- Preserve phase-tagged threshold approach
- Add stronger thresholds
- Add request tags
- Add JSON summary output
- Add production guard
- Add business validation
- Add report metadata
- Document whether no think time is intentional

---

## `k6/tests/scenarios/spike-traffic/spike-traffic.js`

Improve it as a spike traffic test.

Required changes:

- Add `summaryTrendStats`
- Preserve sudden spike behavior
- Add stronger spike-appropriate thresholds
- Add request tags
- Add JSON summary output
- Add production guard
- Add business validation
- Add p99 emphasis in the report
- Document whether no think time is intentional

Important:

The report should clearly explain that spike tests may have higher latency tolerance than normal sustained load tests, but server errors should still be treated seriously.

---

# 13. Fix Report Language and Wording Issues

Ensure report wording is professional and clear.

Avoid mixed-language mistakes such as:

```text
En成熟的 tasarım
```

Use:

```text
English: The most mature design among the scenarios.
Turkish: Senaryolar arasındaki en olgun tasarım.
```

Avoid awkward wording such as:

```text
Check hem status hem body doğrulaması yapıyor — işlerden biri.
```

Use:

```text
English: The check validates both the HTTP status and the response body, which is appropriate for a smoke test.
Turkish: Check hem HTTP durum kodunu hem de response body değerini doğruluyor; bu smoke testi için uygundur.
```

---

# 14. CI-Friendly Output

Ensure every script can produce both HTML and JSON outputs.

Expected output behavior:

```text
HTML: human-readable bilingual performance report
JSON: machine-readable k6 summary
```

Example output names:

```text
reports/smoke.html
reports/smoke.summary.json

reports/transfer-load.html
reports/transfer-load.summary.json

reports/overdraft-race.html
reports/overdraft-race.summary.json

reports/hotspot-load.html
reports/hotspot-load.summary.json

reports/spike-traffic.html
reports/spike-traffic.summary.json
```

If existing naming differs, preserve existing names where reasonable but add matching JSON outputs.

---

# 15. Validation After Changes

After implementing the changes, perform a self-review.

Check all of the following:

- [ ] All five k6 scripts still run syntactically.
- [ ] All five scripts define `summaryTrendStats`.
- [ ] All five scripts generate HTML reports.
- [ ] All five scripts generate JSON summary reports.
- [ ] HTML report remains bilingual EN/TR.
- [ ] Metadata appears in HTML.
- [ ] BASE_URL appears in HTML.
- [ ] ENVIRONMENT appears in HTML.
- [ ] TEST_TYPE appears in HTML.
- [ ] Build/commit metadata appears if provided.
- [ ] p90, p95, p99 appear in HTML.
- [ ] Median appears in HTML.
- [ ] RPS appears in HTML.
- [ ] Data sent/received appears in HTML.
- [ ] Latency breakdown appears in HTML.
- [ ] Status code distribution appears in HTML.
- [ ] Business validation appears in HTML where applicable.
- [ ] Threshold results appear in HTML.
- [ ] Check results appear in HTML.
- [ ] Every HTTP request has `tags.name`.
- [ ] Load/spike/race tests have production guard.
- [ ] Expected 400/409 business responses are not misclassified.
- [ ] Duplicate `trackStatus` logic is reduced.
- [ ] No existing scenario intent is broken.
- [ ] No fake data is introduced.
- [ ] No unsupported assumptions are added without TODO comments.

---

# 16. Deliverables

When finished, provide:

## 1. Summary of Changes

Explain what changed file by file.

Use this format:

```text
File: k6/tests/smoke/smoke.js
Changes:
- Added summaryTrendStats
- Added thresholds
- Added request tags
- Added JSON summary output
- Added report metadata
```

## 2. Risk Notes

Mention any assumptions or areas that still need API contract confirmation.

Examples:

```text
- Transfer response body schema should be confirmed.
- Expected 409 behavior should be verified with business rules.
- Peak RPS cannot be accurately calculated from final k6 summary alone.
```

## 3. Recommended Follow-Up Work

Suggest next steps such as:

- Add Prometheus remote write or InfluxDB output for time-series analysis.
- Add trend comparison between builds.
- Add endpoint-level historical regression detection.
- Add CI threshold gate.
- Add README documentation for running each scenario safely.

---

# 17. Definition of Done

The task is complete only when:

1. All identified report gaps are addressed or explicitly documented.
2. HTML reports are bilingual in English and Turkish.
3. JSON summaries are generated.
4. Thresholds are meaningful for each scenario.
5. Request tags enable endpoint-level analysis.
6. Business validation is visible in reports.
7. Production guard prevents accidental destructive load testing.
8. Repeated helper logic is reduced.
9. Existing scenario behavior is preserved.
10. The final code is readable, maintainable, and suitable for a junior developer to understand.

---

## Final Instruction

Implement the improvements directly in the existing files.  
Do not provide only recommendations.  
Make the code changes, then summarize exactly what you changed and why.