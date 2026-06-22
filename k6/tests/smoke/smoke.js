import http from "k6/http";
import { check } from "k6";
import { generateHtmlReport, counterValue } from "../../lib/html-report.js";

export const options = {
  vus: 1,
  iterations: 5,
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";

export default function () {
  const response = http.get(`${BASE_URL}/health/ready`);

  check(response, {
    "health ready is 200": (res) => res.status === 200,
    "health ready says Healthy": (res) => res.body === "Healthy",
  });
}

export function handleSummary(data) {
  return {
    [REPORT_PATH]: generateHtmlReport(data, {
      title: "Smoke Test Report — Health Readiness",
      purpose:
        "This smoke test verifies that the API is ready before any load or stress test starts. It calls /health/ready, which should confirm that the application and required dependencies are available.",
      verdict: (summary) => {
        const failedChecks = summary.metrics.checks?.values?.fails || 0;
        const failedRequests = summary.metrics.http_req_failed?.values?.fails || 0;
        const ok = failedChecks === 0 && failedRequests === 0;

        return {
          ok,
          message: ok
            ? "The service is ready. /health/ready returned Healthy for every request. It is safe to start load tests."
            : "The service is not ready. Do not start load tests until /health/ready is stable and all checks pass.",
        };
      },
      howToRead: [
        "checks_succeeded must be 100%. Any failed check means readiness is not reliable.",
        "http_req_failed should be 0%. For this smoke test, any non-success HTTP response is a real problem.",
        "p95 latency should stay low because /health/ready is a lightweight pre-flight endpoint.",
      ],
      checkExplanations: {
        "health ready is 200": "The readiness endpoint returned HTTP 200, meaning ASP.NET Core considered the dependency checks healthy.",
        "health ready says Healthy": "The response body was exactly Healthy. This confirms the expected health check output shape.",
      },
      thresholdExplanations: {},
      customCounters: [],
      interpretation: (summary) => [
        `The test sent ${counterValue(summary, "http_reqs")} readiness requests.`,
        "If this report passes, the API, SQL Server, and MongoDB readiness path is working.",
        "If this report fails, fix infrastructure/readiness first; business load tests would be misleading.",
      ],
      nextAction: () => "Run the transfer load test only after this smoke test passes consistently.",
    }),
  };
}
