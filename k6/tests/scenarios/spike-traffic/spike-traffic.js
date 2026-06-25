import http from "k6/http";
import { check } from "k6";
import { Counter, Rate } from "k6/metrics";
import { generateHtmlReport, counterValue, formatNumber } from "../../../lib/html-report.js";
import {
  verifyAccounts,
  trackStatus,
  createTransferParams,
  deriveJsonReportPath,
  buildReportMetadata,
  assertSafeLoadTarget,
} from "../../../lib/seed-helper.js";

const status200 = new Counter("status_200");
const status201 = new Counter("status_201");
const status400 = new Counter("status_400");
const status409 = new Counter("status_409");
const status500 = new Counter("status_500");
const statusOther = new Counter("status_other");
// Business-aware metrics. A spike tolerates higher latency than sustained load,
// but a server error is never acceptable — server_errors is the hard gate.
const serverErrors = new Counter("server_errors");
const validBusinessResponses = new Rate("valid_business_responses");

const counters = { status200, status201, status400, status409, status500, statusOther };

// NOTE: no think time / sleep is intentional here — the goal is a sudden load
// shock, so requests must ramp as fast as the stages allow.
export const options = {
  stages: [
    { duration: "5s", target: 200 },
    { duration: "30s", target: 200 },
    { duration: "5s", target: 0 },
  ],
  thresholds: {
    // Spike: tolerate higher latency (p95<3000, p99<5000) but no server errors.
    "http_req_duration{phase:load}": ["p(95)<3000", "p(99)<5000"],
    checks: ["rate>0.90"],
    server_errors: ["count==0"],
    valid_business_responses: ["rate>0.90"],
  },
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const REPORT_PATH = __ENV.REPORT_PATH || "reports/spike-traffic-report.html";
const JSON_REPORT_PATH = deriveJsonReportPath(REPORT_PATH);
const CURRENCY_CODE = "TRY";
const SCENARIO_NAME = "spike-traffic";
const TEST_DOC = open("./README.md");
const SENDER_ACCOUNT_IDS = Array.from(
  { length: 100 },
  (_, index) => `22000000-0000-7000-8000-${String(index + 1).padStart(12, "0")}`
);
const RECEIVER_ACCOUNT_IDS = Array.from(
  { length: 100 },
  (_, index) => `22000000-0000-7000-8000-${String(index + 101).padStart(12, "0")}`
);

assertSafeLoadTarget(BASE_URL);

// Small local helper: read a Rate metric as a formatted percentage.
function rateOf(summary, name) {
  const rate = summary.metrics[name]?.values?.rate;
  return rate === undefined ? "-" : `${formatNumber(rate * 100, 2)}%`;
}

export function setup() {
  verifyAccounts(BASE_URL, [...SENDER_ACCOUNT_IDS, ...RECEIVER_ACCOUNT_IDS], SCENARIO_NAME);

  return {
    senders: SENDER_ACCOUNT_IDS.map((id) => ({ id })),
    receivers: RECEIVER_ACCOUNT_IDS.map((id) => ({ id })),
  };
}

export default function (data) {
  const sender = data.senders[(__VU - 1) % data.senders.length];
  const receiver = data.receivers[(__ITER + __VU - 1) % data.receivers.length];
  const payload = JSON.stringify({
    senderAccountId: sender.id,
    receiverAccountId: receiver.id,
    amount: 1,
    currencyCode: CURRENCY_CODE,
    description: "Spike traffic scenario transfer",
    idempotencyKey: crypto.randomUUID(),
  });

  const response = http.post(
    `${BASE_URL}/api/transactions/transfer`,
    payload,
    createTransferParams("k6-spike-traffic", {}, [200, 201, 409])
  );

  trackStatus(response, counters);

  if (response.status >= 500) {
    serverErrors.add(1);
  }
  if (response.status >= 500 || response.status === 400) {
    console.log(`status=${response.status} body=${response.body}`);
  }

  // Valid business outcome: success (200/201) or expected 409 contention under
  // the sudden spike. 400/500/other are invalid because all senders are funded.
  const isValidBusiness =
    response.status === 200 || response.status === 201 || response.status === 409;
  validBusinessResponses.add(isValidBusiness);

  check(response, {
    "transfer success or expected contention": (res) =>
      res.status === 200 || res.status === 201 || res.status === 409,
    "transfer did not fail with 400 or 500": (res) => res.status !== 400 && res.status < 500,
  });
}

export function handleSummary(data) {
  return {
    [REPORT_PATH]: generateHtmlReport(data, {
      title: {
        en: "Scenario 5 — Spike Traffic",
        tr: "Senaryo 5 — Ani Trafik Şoku",
      },
      metadata: buildReportMetadata({
        baseUrl: BASE_URL,
        testType: "spike",
        scenarioName: SCENARIO_NAME,
        reportPath: REPORT_PATH,
        jsonReportPath: JSON_REPORT_PATH,
      }),
      testDoc: TEST_DOC,
      purpose: {
        en: "Uses fixed SQL fixture accounts, then jumps immediately to high traffic to observe container and API behavior under sudden load shock. Spike tests tolerate higher latency than sustained load tests, but server errors are always treated seriously.",
        tr: "Sabit SQL fixture hesaplarını kullanır; ardından ani yüksek trafiğe çıkarak container ve API davranışını ölçer. Spike testleri sürekli yük testlerinden daha yüksek gecikmeye toleranslıdır; ancak server hataları her zaman ciddiye alınır.",
      },
      verdict: (summary) => {
        const badRequests = counterValue(summary, "status_400");
        const serverFailures = counterValue(summary, "status_500");
        const unknownStatus = counterValue(summary, "status_other");
        const ok = badRequests === 0 && serverFailures === 0 && unknownStatus === 0;

        return {
          ok,
          message: ok
            ? {
                en: "Spike traffic stayed inside expected outcomes. No 400, 500, or unknown statuses.",
                tr: "Ani trafik beklenen sonuçlarda kaldı. 400, 500 veya bilinmeyen status yok.",
              }
            : {
                en: `Unexpected spike failures: 400=${badRequests}, 500=${serverFailures}, other=${unknownStatus}.`,
                tr: `Beklenmeyen spike hataları: 400=${badRequests}, 500=${serverFailures}, other=${unknownStatus}.`,
              },
        };
      },
      howToRead: [
        { en: "200/201 means a transfer was accepted during the spike.", tr: "200/201, spike sırasında transferin kabul edildiği anlamına gelir." },
        { en: "409 can be acceptable contention under sudden concurrent writes.", tr: "409, ani eş zamanlı yazımlarda kabul edilebilir contention sinyalidir." },
        { en: "400 is unexpected because all senders are reset with enough balance.", tr: "400 beklenmez çünkü tüm sender hesapları yeterli bakiye ile resetlenir." },
        { en: "500 means the API failed under shock traffic and must remain 0.", tr: "500 API'nin şok trafik altında hata verdiğini gösterir ve 0 kalmalıdır." },
        { en: "Spike p99 may be higher than in sustained load; focus on whether 500 stays at 0.", tr: "Spike p99 sürekli yüke göre daha yüksek olabilir; asıl odak 500'ün 0 kalmasıdır." },
      ],
      checkExplanations: {
        "transfer success or expected contention": { en: "Accepts successful transfer or expected 409 contention.", tr: "Başarılı transfer veya beklenen 409 contention kabul edilir." },
        "transfer did not fail with 400 or 500": { en: "Fixture-backed spike should not hit insufficient funds or server errors.", tr: "Fixture tabanlı spike yetersiz bakiye veya server hatası üretmemelidir." },
      },
      thresholdExplanations: {
        "http_req_duration{phase:load}": { en: "Spike p95 under 3000ms and p99 under 5000ms, load-phase only. Higher than sustained load on purpose.", tr: "Spike p95 3000ms altında, p99 5000ms altında, sadece load-phase. Sürekli yüke göre bilerek daha yüksek." },
        checks: { en: "At least 90% of checks must pass (spike tolerance).", tr: "Kontrollerin en az %90'ı geçmeli (spike toleransı)." },
        server_errors: { en: "No server errors allowed, even under spike.", tr: "Spike altında bile server hatası olmamalı." },
        valid_business_responses: { en: "At least 90% of responses must be a valid business outcome (spike tolerance).", tr: "Cevapların en az %90'ı geçerli iş kuralı sonucu olmalı (spike toleransı)." },
      },
      customCounters: [
        { name: "status_200", explanation: { en: "Successful transfer with 200 if API returns OK.", tr: "API OK dönerse 200 başarılı transfer." } },
        { name: "status_201", explanation: { en: "Successful transfer with 201 Created.", tr: "201 Created başarılı transfer." } },
        { name: "status_400", explanation: { en: "Unexpected bad request/insufficient funds. Must be 0.", tr: "Beklenmeyen bad request/yetersiz bakiye. 0 olmalıdır." } },
        { name: "status_409", explanation: { en: "Expected concurrency conflict under sudden spike.", tr: "Ani spike altında beklenen concurrency conflict." } },
        { name: "status_500", explanation: { en: "Server failure. Must be 0.", tr: "Server hatası. 0 olmalıdır." } },
        { name: "status_other", explanation: { en: "Untracked status. Investigate if non-zero.", tr: "Takip edilmeyen status. 0 değilse incelenmeli." } },
      ],
      extraMetricRows: (summary) => {
        const successes = counterValue(summary, "status_200") + counterValue(summary, "status_201");
        const conflicts = counterValue(summary, "status_409");
        const iterations = counterValue(summary, "iterations");
        return [
          { label: { en: "Accepted transfers", tr: "Kabul edilen transfer" }, value: successes, explanation: { en: "Successful transfer count during spike.", tr: "Spike sırasında başarılı transfer sayısı." } },
          { label: { en: "Spike conflicts", tr: "Spike conflict" }, value: conflicts, explanation: { en: "409 responses during sudden traffic.", tr: "Ani trafik sırasında gelen 409 cevapları." } },
          { label: { en: "Success rate", tr: "Başarı oranı" }, value: iterations === 0 ? "0%" : `${formatNumber((successes / iterations) * 100, 2)}%`, explanation: { en: "Accepted transfers / total iterations.", tr: "Kabul edilen transfer / toplam iterasyon." } },
          { label: { en: "p99 latency", tr: "p99 gecikme" }, value: `${formatNumber(summary.metrics.http_req_duration?.values?.["p(99)"])} ms`, explanation: { en: "p99 is the key spike indicator — the slowest 1%.", tr: "p99 spike için temel göstergedir — en yavaş %1." } },
          { label: { en: "Valid business response rate", tr: "Geçerli iş kuralı oranı" }, value: rateOf(summary, "valid_business_responses"), explanation: { en: "valid_business_responses rate. Target >90%.", tr: "valid_business_responses oranı. Hedef >%90." } },
        ];
      },
      interpretation: (summary) => [
        { en: `${counterValue(summary, "status_200") + counterValue(summary, "status_201")} transfers succeeded during spike traffic.`, tr: `${counterValue(summary, "status_200") + counterValue(summary, "status_201")} transfer spike trafik sırasında başarılı oldu.` },
        { en: `${counterValue(summary, "status_409")} requests returned 409. This is contention signal, not server failure.`, tr: `${counterValue(summary, "status_409")} istek 409 döndü. Bu contention sinyalidir, server hatası değildir.` },
        { en: `${counterValue(summary, "status_500")} requests returned 500. This must remain 0.`, tr: `${counterValue(summary, "status_500")} istek 500 döndü. Bu 0 kalmalıdır.` },
      ],
      nextAction: (summary) =>
        counterValue(summary, "status_500") > 0
          ? { en: "Investigate 500 errors in ApplicationLogs using k6-spike-traffic correlation ids.", tr: "500 hatalarını k6-spike-traffic correlation id ile ApplicationLogs içinde incele." }
          : { en: "Next: compare the three scenario reports and inspect ApplicationLogs for correlated warnings/errors.", tr: "Sonraki: üç senaryo raporunu karşılaştır ve ApplicationLogs içinde ilişkili warning/error kayıtlarını incele." },
    }),
    [JSON_REPORT_PATH]: JSON.stringify(data, null, 2),
  };
}
