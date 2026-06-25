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
// Business-aware metrics. 409 is expected contention on the hot receiver,
// not a technical failure; server_errors is the real gate.
const serverErrors = new Counter("server_errors");
const validBusinessResponses = new Rate("valid_business_responses");

const counters = { status200, status201, status400, status409, status500, statusOther };

// NOTE: no think time / sleep is intentional here — the goal is sustained
// contention on the hot receiver account, so back-to-back requests are desired.
export const options = {
  stages: [
    { duration: "10s", target: 50 },
    { duration: "30s", target: 50 },
    { duration: "5s", target: 0 },
  ],
  thresholds: {
    // Phase-tagged: only count load-phase transfer requests, not setup health checks.
    "http_req_duration{phase:load}": ["p(95)<2000", "p(99)<4000"],
    checks: ["rate>0.95"],
    server_errors: ["count==0"],
    valid_business_responses: ["rate>0.95"],
  },
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const REPORT_PATH = __ENV.REPORT_PATH || "reports/hotspot-load-report.html";
const JSON_REPORT_PATH = deriveJsonReportPath(REPORT_PATH);
const CURRENCY_CODE = "TRY";
const SCENARIO_NAME = "hotspot-load";
const TEST_DOC = open("./README.md");
const RECEIVER_ACCOUNT_ID = "21000000-0000-7000-8000-000000000001";
const SENDER_ACCOUNT_IDS = Array.from(
  { length: 50 },
  (_, index) => `21000000-0000-7000-8000-${String(index + 101).padStart(12, "0")}`
);

assertSafeLoadTarget(BASE_URL);

// Small local helper: read a Rate metric as a formatted percentage.
function rateOf(summary, name) {
  const rate = summary.metrics[name]?.values?.rate;
  return rate === undefined ? "-" : `${formatNumber(rate * 100, 2)}%`;
}

export function setup() {
  verifyAccounts(BASE_URL, [RECEIVER_ACCOUNT_ID, ...SENDER_ACCOUNT_IDS], SCENARIO_NAME);

  return {
    receiverAccountId: RECEIVER_ACCOUNT_ID,
    senderAccounts: SENDER_ACCOUNT_IDS.map((id) => ({ id })),
  };
}

export default function (data) {
  const sender = data.senderAccounts[(__VU - 1) % data.senderAccounts.length];
  const payload = JSON.stringify({
    senderAccountId: sender.id,
    receiverAccountId: data.receiverAccountId,
    amount: 1010,
    currencyCode: CURRENCY_CODE,
    description: "Hotspot load scenario transfer",
    idempotencyKey: crypto.randomUUID(),
  });

  const response = http.post(
    `${BASE_URL}/api/transactions/transfer`,
    payload,
    createTransferParams("k6-hotspot-load", {}, [200, 201, 409])
  );

  trackStatus(response, counters);

  if (response.status >= 500) {
    serverErrors.add(1);
  }
  if (response.status >= 500 || response.status === 400) {
    console.log(`status=${response.status} body=${response.body}`);
  }

  // Valid business outcome: success (200/201) or expected 409 contention on the
  // hot receiver. 400/500/other are invalid because all senders are funded.
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
        en: "Scenario 2 — Hotspot Load",
        tr: "Senaryo 2 — Hotspot Yük Testi",
      },
      metadata: buildReportMetadata({
        baseUrl: BASE_URL,
        testType: "load",
        scenarioName: SCENARIO_NAME,
        reportPath: REPORT_PATH,
        jsonReportPath: JSON_REPORT_PATH,
      }),
      testDoc: TEST_DOC,
      purpose: {
        en: "Uses fixed SQL fixture accounts: many funded senders and one popular receiver, then sends sustained concurrent transfers to reveal database hotspot and contention behavior.",
        tr: "Sabit SQL fixture hesaplarını kullanır: çok sayıda bakiyeli sender ve tek popüler receiver ile sürekli eş zamanlı transferler göndererek database hotspot ve contention davranışını gösterir.",
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
                en: "Hotspot load stayed inside expected outcomes. No 400, 500, or unknown statuses.",
                tr: "Hotspot yük beklenen sonuçlarda kaldı. 400, 500 veya bilinmeyen status yok.",
              }
            : {
                en: `Unexpected hotspot failures: 400=${badRequests}, 500=${serverFailures}, other=${unknownStatus}.`,
                tr: `Beklenmeyen hotspot hataları: 400=${badRequests}, 500=${serverFailures}, other=${unknownStatus}.`,
              },
        };
      },
      howToRead: [
        { en: "200/201 means a transfer was accepted.", tr: "200/201 transferin kabul edildiği anlamına gelir." },
        { en: "409 can be expected when many requests update the same receiver account.", tr: "409, çok sayıda istek aynı receiver hesabını güncellediğinde beklenebilir." },
        { en: "400 is unexpected because all senders are reset with enough balance.", tr: "400 beklenmez çünkü tüm sender hesapları yeterli bakiye ile resetlenir." },
        { en: "500 is a real server failure and must remain 0.", tr: "500 gerçek server hatasıdır ve 0 kalmalıdır." },
        { en: "p95 below 2000ms is measured only on load-phase transfer requests.", tr: "p95 2000ms hedefi sadece load-phase transfer istekleri üzerinde ölçülür." },
      ],
      checkExplanations: {
        "transfer success or expected contention": { en: "Accepts successful transfer or expected 409 contention.", tr: "Başarılı transfer veya beklenen 409 contention kabul edilir." },
        "transfer did not fail with 400 or 500": { en: "Fixture-backed load should not hit insufficient funds or server errors.", tr: "Fixture tabanlı yük yetersiz bakiye veya server hatası üretmemelidir." },
      },
      thresholdExplanations: {
        "http_req_duration{phase:load}": { en: "p95 under 2000ms and p99 under 4000ms, load-phase only.", tr: "p95 2000ms altında, p99 4000ms altında, sadece load-phase." },
        checks: { en: "At least 95% of checks must pass.", tr: "Kontrollerin en az %95'i geçmeli." },
        server_errors: { en: "No server errors allowed.", tr: "Server hatası olmamalı." },
        valid_business_responses: { en: "At least 95% of responses must be a valid business outcome.", tr: "Cevapların en az %95'i geçerli iş kuralı sonucu olmalı." },
      },
      customCounters: [
        { name: "status_200", explanation: { en: "Successful transfer with 200 if API returns OK.", tr: "API OK dönerse 200 başarılı transfer." } },
        { name: "status_201", explanation: { en: "Successful transfer with 201 Created.", tr: "201 Created başarılı transfer." } },
        { name: "status_400", explanation: { en: "Unexpected bad request/insufficient funds. Must be 0.", tr: "Beklenmeyen bad request/yetersiz bakiye. 0 olmalıdır." } },
        { name: "status_409", explanation: { en: "Expected contention on the hot receiver account.", tr: "Hot receiver hesabında beklenen contention." } },
        { name: "status_500", explanation: { en: "Server failure. Must be 0.", tr: "Server hatası. 0 olmalıdır." } },
        { name: "status_other", explanation: { en: "Untracked status. Investigate if non-zero.", tr: "Takip edilmeyen status. 0 değilse incelenmeli." } },
      ],
      extraMetricRows: (summary) => {
        const successes = counterValue(summary, "status_200") + counterValue(summary, "status_201");
        const conflicts = counterValue(summary, "status_409");
        const iterations = counterValue(summary, "iterations");
        return [
          { label: { en: "Accepted transfers", tr: "Kabul edilen transfer" }, value: successes, explanation: { en: "Successful transfer count.", tr: "Başarılı transfer sayısı." } },
          { label: { en: "Contention conflicts", tr: "Contention conflict" }, value: conflicts, explanation: { en: "409 responses from hotspot writes.", tr: "Hotspot yazımlarından gelen 409 cevapları." } },
          { label: { en: "Success rate", tr: "Başarı oranı" }, value: iterations === 0 ? "0%" : `${formatNumber((successes / iterations) * 100, 2)}%`, explanation: { en: "Accepted transfers / total iterations.", tr: "Kabul edilen transfer / toplam iterasyon." } },
          { label: { en: "Valid business response rate", tr: "Geçerli iş kuralı oranı" }, value: rateOf(summary, "valid_business_responses"), explanation: { en: "valid_business_responses rate. Target >95%.", tr: "valid_business_responses oranı. Hedef >%95." } },
        ];
      },
      interpretation: (summary) => [
        { en: `${counterValue(summary, "status_200") + counterValue(summary, "status_201")} transfers succeeded under receiver hotspot.`, tr: `${counterValue(summary, "status_200") + counterValue(summary, "status_201")} transfer receiver hotspot altında başarılı oldu.` },
        { en: `${counterValue(summary, "status_409")} requests returned 409. This is contention signal, not server failure.`, tr: `${counterValue(summary, "status_409")} istek 409 döndü. Bu contention sinyalidir, server hatası değildir.` },
        { en: `${counterValue(summary, "status_500")} requests returned 500. This must remain 0.`, tr: `${counterValue(summary, "status_500")} istek 500 döndü. Bu 0 kalmalıdır.` },
      ],
      nextAction: (summary) =>
        counterValue(summary, "status_500") > 0
          ? { en: "Investigate 500 errors in ApplicationLogs using k6-hotspot-load correlation ids.", tr: "500 hatalarını k6-hotspot-load correlation id ile ApplicationLogs içinde incele." }
          : { en: "Next: reset fixtures with k6/setup/setup-scenario-data.sql, then run spike-traffic.", tr: "Sonraki: fixture'ları k6/setup/setup-scenario-data.sql ile resetle, sonra spike-traffic çalıştır." },
    }),
    [JSON_REPORT_PATH]: JSON.stringify(data, null, 2),
  };
}
