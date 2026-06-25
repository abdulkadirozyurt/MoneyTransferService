import http from "k6/http";
import exec from "k6/execution";

export const setupTags = { phase: "setup" };
export const loadTags = { phase: "load" };

/**
 * Standard set of request tags used by transfer requests so the HTML report
 * can group requests by endpoint (top slow / top failing endpoint analysis).
 * Keep these values in sync with the real endpoint from TransactionEndpoints.
 */
export const transferTags = {
  name: "POST /api/transactions/transfer",
  method: "POST",
  endpoint: "/api/transactions/transfer",
};

export const healthTags = {
  name: "GET /health/ready",
  method: "GET",
  endpoint: "/health/ready",
};

/**
 * Build common JSON request params (Content-Type + optional phase tag) merged
 * with endpoint tags. Returns a fresh object each call so per-iteration headers
 * (e.g. X-Correlation-Id) can be added without mutating shared state.
 */
export function jsonParams(tags = loadTags) {
  return {
    headers: {
      "Content-Type": "application/json",
    },
    tags,
  };
}

function parseJson(response, operation) {
  try {
    return response.json();
  } catch (error) {
    exec.test.abort(`${operation} returned invalid JSON. status=${response.status} body=${response.body}`);
  }
}

function requireSuccess(response, operation, scenarioName) {
  if (response.status < 200 || response.status >= 300) {
    const setupHint = scenarioName ? ` Run k6/setup/setup-scenario-data.sql before this scenario.` : "";
    exec.test.abort(`${operation} failed. status=${response.status} body=${response.body}.${setupHint}`);
  }

  return parseJson(response, operation);
}

export function verifyBalance(baseUrl, accountId, options = {}) {
  const response = http.get(`${baseUrl}/api/accounts/${accountId}/balance`, {
    tags: options.tags || setupTags,
  });

  return requireSuccess(response, `verify account balance ${accountId}`, options.scenarioName);
}

export function verifyAccounts(baseUrl, accountIds, scenarioName) {
  for (const accountId of accountIds) {
    verifyBalance(baseUrl, accountId, { scenarioName, tags: setupTags });
  }
}

/**
 * Increment the right status counter based on the response status code.
 * Centralised here to remove the duplicated trackStatus() that used to live in
 * every scenario script.
 *
 * @param {object} res  - k6 http response
 * @param {object} counters - object exposing status200/201/400/409/500/other Counters
 */
export function trackStatus(res, counters) {
  const status = res.status;
  if (status === 200) counters.status200.add(1);
  else if (status === 201) counters.status201.add(1);
  else if (status === 400) counters.status400.add(1);
  else if (status === 409) counters.status409.add(1);
  else if (status >= 500) counters.status500.add(1);
  else counters.statusOther.add(1);
}

/**
 * Create a stable, traceable correlation id for a request.
 * Matches the previous k6-<scenario>-<vu>-<iter> convention used across scripts.
 */
export function createCorrelationId(prefix) {
  return `${prefix}-${__VU}-${__ITER}`;
}

/**
 * Build transfer request params (JSON content type, load phase + endpoint tags,
 * and an X-Correlation-Id header derived from the given scenario prefix).
 */
export function createTransferParams(prefix, extraTags = {}, expectedStatuses = []) {
  const params = jsonParams(loadTags);
  // Merge phase, endpoint-level tags, and any per-scenario extra tags.
  params.tags = { ...loadTags, ...transferTags, ...extraTags };
  params.headers["X-Correlation-Id"] = createCorrelationId(prefix);

  if (expectedStatuses.length > 0) {
    params.responseCallback = http.expectedStatuses(...expectedStatuses);
  }

  return params;
}

/**
 * Derive a JSON summary path from an HTML report path.
 *   "reports/transfer-load.html" -> "reports/transfer-load.summary.json"
 *   "transfer-load"              -> "transfer-load.summary.json"
 * Always returns a string ending in ".summary.json".
 */
export function deriveJsonReportPath(reportPath) {
  const htmlPath = typeof reportPath === "string" && reportPath.length > 0 ? reportPath : "report.html";
  const withoutHtml = htmlPath.replace(/\.html$/i, "");
  return `${withoutHtml}.summary.json`;
}

/**
 * Production safety guard for non-smoke load/race/spike tests.
 *
 * Aborts the run (k6 native abort, not just a thrown Error) if the configured
 * target looks production-like and ALLOW_PROD_LOAD_TEST is not explicitly "true".
 *
 * Smoke tests intentionally skip this guard because readiness checks may be a
 * valid operation against production.
 */
export function assertSafeLoadTarget(baseUrl) {
  const environment = (__ENV.ENVIRONMENT || "").toLowerCase();
  const allowProd = __ENV.ALLOW_PROD_LOAD_TEST === "true";

  const looksProduction =
    environment === "prod" ||
    environment === "production" ||
    /prod|production/i.test(baseUrl || "");

  if (looksProduction && !allowProd) {
    exec.test.abort(
      "Refusing to run a load/race/spike test against a production-like target. " +
        "Set ALLOW_PROD_LOAD_TEST=true to override intentionally."
    );
  }
}

/**
 * Build the report metadata object passed into generateHtmlReport().
 * Centralised so all scripts expose the same metadata shape and every field
 * has a documented fallback instead of "unknown" leaking into the report.
 */
export function buildReportMetadata(overrides = {}) {
  return {
    baseUrl: overrides.baseUrl || __ENV.BASE_URL || "",
    environment: overrides.environment || __ENV.ENVIRONMENT || "local",
    testType: overrides.testType || __ENV.TEST_TYPE || "",
    scenarioName: overrides.scenarioName || "",
    buildId: __ENV.BUILD_ID || "",
    commitSha: __ENV.COMMIT_SHA || "",
    reportPath: overrides.reportPath || __ENV.REPORT_PATH || "",
    jsonReportPath: overrides.jsonReportPath || __ENV.JSON_REPORT_PATH || "",
    summaryTrendStats: overrides.summaryTrendStats || [],
    ...overrides,
  };
}
