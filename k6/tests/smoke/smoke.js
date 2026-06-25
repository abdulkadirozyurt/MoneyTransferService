import http from "k6/http";
import { check } from "k6";
import { generateHtmlReport, counterValue } from "../../lib/html-report.js";
import { healthTags, deriveJsonReportPath, buildReportMetadata } from "../../lib/seed-helper.js";

export const options = {
  vus: 1,
  iterations: 5,
  // Readiness gate: every check must pass, no failed HTTP request, low latency.
  thresholds: {
    checks: ["rate==1"],
    http_req_duration: ["p(95)<500"],
    http_req_failed: ["rate==0"],
  },
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const REPORT_PATH = __ENV.REPORT_PATH || "smoke-report.html";
const JSON_REPORT_PATH = deriveJsonReportPath(REPORT_PATH);
const TEST_DOC = open("./README.md");

export default function () {
  const response = http.get(`${BASE_URL}/health/ready`, { tags: healthTags });

  check(response, {
    "health ready is 200": (res) => res.status === 200,
    "health ready says Healthy": (res) => res.body === "Healthy",
  });
}

export function handleSummary(data) {
  return {
    [REPORT_PATH]: generateHtmlReport(data, {
      title: {
        en: "Smoke Test Report — Health Readiness",
        tr: "Smoke Test Raporu — Sağlık Hazırlığı",
      },
      purpose: {
        en: "This smoke test verifies that the API is ready before any load or stress test starts. It calls /health/ready, which should confirm that the application and required dependencies are available.",
        tr: "Bu smoke testi, herhangi bir yük veya stres testi başlamadan önce API'nin hazır olduğunu doğrular. /health/ready çağırır ve uygulama ile gerekli bağımlılıkların erişilebilir olduğunu teyit eder.",
      },
      metadata: buildReportMetadata({
        baseUrl: BASE_URL,
        testType: "smoke",
        reportPath: REPORT_PATH,
        jsonReportPath: JSON_REPORT_PATH,
      }),
      testDoc: TEST_DOC,
      verdict: (summary) => {
        const failedChecks = summary.metrics.checks?.values?.fails || 0;
        const failedRequests = summary.metrics.http_req_failed?.values?.fails || 0;
        const ok = failedChecks === 0 && failedRequests === 0;

        return {
          ok,
          message: ok
            ? {
                en: "The service is ready. /health/ready returned Healthy for every request. It is safe to start load tests.",
                tr: "Servis hazır. /health/ready her istekte Healthy döndü. Yük testlerine başlamak güvenlidir.",
              }
            : {
                en: "The service is not ready. Do not start load tests until /health/ready is stable and all checks pass.",
                tr: "Servis hazır değil. /health/ready stabil hale gelene ve tüm kontroller geçene kadar yük testi başlatma.",
              },
        };
      },
      howToRead: [
        {
          en: "checks_succeeded must be 100%. Any failed check means readiness is not reliable.",
          tr: "checks_succeeded %100 olmalı. Başarısız kontrol, hazırlığın güvenilir olmadığı anlamına gelir.",
        },
        {
          en: "http_req_failed should be 0%. For this smoke test, any non-success HTTP response is a real problem.",
          tr: "http_req_failed %0 olmalı. Bu smoke testinde başarısız HTTP cevabı gerçek bir sorundur.",
        },
        {
          en: "p95 latency should stay low because /health/ready is a lightweight pre-flight endpoint.",
          tr: "p95 düşük kalmalı çünkü /health/ready hafif bir ön kontrol endpoint'idir.",
        },
      ],
      checkExplanations: {
        "health ready is 200": {
          en: "The readiness endpoint returned HTTP 200, meaning ASP.NET Core considered the dependency checks healthy.",
          tr: "Hazırlık endpoint'i HTTP 200 döndü; ASP.NET Core bağımlılık kontrollerini sağlıklı kabul etti.",
        },
        "health ready says Healthy": {
          en: "The response body was exactly Healthy. This confirms the expected health check output shape.",
          tr: "Response body tam olarak Healthy. Beklenen sağlık çıktı formatını doğrular.",
        },
      },
      thresholdExplanations: {
        checks: {
          en: "All readiness checks must pass (100%).",
          tr: "Tüm hazırlık kontrolleri geçmeli (%100).",
        },
        http_req_duration: {
          en: "p95 readiness latency should stay under 500ms.",
          tr: "p95 hazırlık gecikmesi 500ms altında kalmalı.",
        },
        http_req_failed: {
          en: "No failed HTTP responses allowed for a readiness check.",
          tr: "Hazırlık kontrolünde başarısız HTTP cevabı olmamalı.",
        },
      },
      customCounters: [],
      interpretation: (summary) => [
        {
          en: `The test sent ${counterValue(summary, "http_reqs")} readiness requests.`,
          tr: `Test ${counterValue(summary, "http_reqs")} hazırlık isteği gönderdi.`,
        },
        {
          en: "If this report passes, the API, SQL Server, and MongoDB readiness path is working.",
          tr: "Bu rapor geçerse API, SQL Server ve MongoDB hazırlık yolu çalışıyor demektir.",
        },
        {
          en: "If this report fails, fix infrastructure/readiness first; business load tests would be misleading.",
          tr: "Bu rapor başarısız olursa önce altyapıyı/hazırlığı düzelt; aksi halde yük testleri yanıltıcı olur.",
        },
      ],
      nextAction: () => ({
        en: "Run the transfer load test only after this smoke test passes consistently.",
        tr: "Bu smoke testi stabil geçtikten sonra transfer yük testini çalıştır.",
      }),
    }),
    [JSON_REPORT_PATH]: JSON.stringify(data, null, 2),
  };
}
