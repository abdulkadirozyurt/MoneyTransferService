import http from "k6/http";
import { check } from "k6";
import { Counter, Rate } from "k6/metrics";
import { generateHtmlReport, counterValue, formatNumber } from "../../../lib/html-report.js";
import {
  verifyAccounts,
  verifyBalance,
  trackStatus,
  createTransferParams,
  deriveJsonReportPath,
  buildReportMetadata,
  assertSafeLoadTarget,
  fixtureIban,
} from "../../../lib/seed-helper.js";

const status200 = new Counter("status_200");
const status201 = new Counter("status_201");
const status400 = new Counter("status_400");
const status409 = new Counter("status_409");
const status500 = new Counter("status_500");
const statusOther = new Counter("status_other");
// Business-aware metrics: race test expects exactly ONE success and the rest rejected.
const serverErrors = new Counter("server_errors");
const validBusinessResponses = new Rate("valid_business_responses");

const counters = { status200, status201, status400, status409, status500, statusOther };

export const options = {
  vus: 20,
  iterations: 20,
  thresholds: {
    // Race test: no server errors, checks must hold. Note: the deterministic
    // "exactly one success" rule is enforced in the verdict (final counters),
    // since a simple k6 threshold cannot express a final aggregate count.
    checks: ["rate>0.95"],
    server_errors: ["count==0"],
    http_req_duration: ["p(95)<2000"],
    valid_business_responses: ["rate==1"],
  },
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const REPORT_PATH = __ENV.REPORT_PATH || "reports/overdraft-race-report.html";
const JSON_REPORT_PATH = deriveJsonReportPath(REPORT_PATH);
const CURRENCY_CODE = "TRY";
const SCENARIO_NAME = "overdraft-race";
const TEST_DOC = open("./README.md");
const SENDER_ACCOUNT_ID = "20000000-0000-7000-8000-000000000001";
const SENDER_IBAN = fixtureIban("10", 1);
const RECEIVER_ACCOUNT_IDS = Array.from(
  { length: 20 },
  (_, index) => `20000000-0000-7000-8000-${String(index + 101).padStart(12, "0")}`
);
const RECEIVER_IBANS = Array.from(
  { length: 20 },
  (_, index) => fixtureIban("10", index + 101)
);

assertSafeLoadTarget(BASE_URL);

// Small local helper: read a Rate metric as a formatted percentage.
function rateOf(summary, name) {
  const rate = summary.metrics[name]?.values?.rate;
  return rate === undefined ? "-" : `${formatNumber(rate * 100, 2)}%`;
}

export function setup() {
  verifyAccounts(BASE_URL, [SENDER_ACCOUNT_ID, ...RECEIVER_ACCOUNT_IDS], SCENARIO_NAME);

  return {
    senderAccountId: SENDER_ACCOUNT_ID,
    senderIban: SENDER_IBAN,
    receiverAccountIds: RECEIVER_ACCOUNT_IDS,
    receiverIbans: RECEIVER_IBANS,
  };
}

export default function (data) {
  const receiverIban = data.receiverIbans[(__VU - 1) % data.receiverIbans.length];
  const payload = JSON.stringify({
    senderIban: data.senderIban,
    receiverIban,
    amount: 100,
    currencyCode: CURRENCY_CODE,
    description: "Overdraft race scenario transfer",
    idempotencyKey: crypto.randomUUID(),
  });

  const response = http.post(
    `${BASE_URL}/api/transactions/transfer`,
    payload,
    createTransferParams("k6-overdraft-race", {}, [200, 201, 400, 409])
  );

  trackStatus(response, counters);

  if (response.status >= 500) {
    serverErrors.add(1);
    console.log(`status=${response.status} body=${response.body}`);
  }

  // Valid business outcome for this race: one success (200/201) OR an expected
  // rejection (400 insufficient funds / 409 concurrency). Server errors are invalid.
  const isValidBusiness =
    response.status === 200 ||
    response.status === 201 ||
    response.status === 400 ||
    response.status === 409;
  validBusinessResponses.add(isValidBusiness);

  check(response, {
    "transfer success or expected business failure": (res) =>
      res.status === 200 || res.status === 201 || res.status === 400 || res.status === 409,
    "transfer did not fail with 500": (res) => res.status < 500,
  });
}

export function teardown(data) {
  const balance = verifyBalance(BASE_URL, data.senderAccountId, { scenarioName: SCENARIO_NAME });
  // NOTE: the final-balance assertion lives here for traceability. The verdict
  // below additionally enforces the deterministic "exactly one success" rule.
  console.log(`overdraft-race sender final balance=${balance.balance ?? balance.Balance}`);
}

export function handleSummary(data) {
  return {
    [REPORT_PATH]: generateHtmlReport(data, {
      title: {
        en: "Scenario 1 — Overdraft Race",
        tr: "Senaryo 1 — Yetersiz Bakiye Yarışı",
      },
      metadata: buildReportMetadata({
        baseUrl: BASE_URL,
        testType: "race",
        scenarioName: SCENARIO_NAME,
        reportPath: REPORT_PATH,
        jsonReportPath: JSON_REPORT_PATH,
      }),
      testDoc: TEST_DOC,
      purpose: {
        en: "Uses fixed SQL fixture accounts: one sender with exactly 100 TRY, then sends 20 concurrent 100 TRY transfers. Only one transfer should move money; the rest should fail as expected business/concurrency outcomes.",
        tr: "Sabit SQL fixture hesaplarını kullanır: tam 100 TRY bakiyesi olan tek sender ile aynı anda 20 adet 100 TRY transfer dener. Sadece bir transfer para taşımalı; kalanlar beklenen business/concurrency sonucu olmalıdır.",
      },
      verdict: (summary) => {
        const successes = counterValue(summary, "status_200") + counterValue(summary, "status_201");
        const expectedFailures = counterValue(summary, "status_400") + counterValue(summary, "status_409");
        const serverFailures = counterValue(summary, "status_500");
        const unknownStatus = counterValue(summary, "status_other");
        const iterations = counterValue(summary, "iterations");
        const expectedTotal = successes + expectedFailures;
        const ok =
          successes === 1 &&
          serverFailures === 0 &&
          unknownStatus === 0 &&
          expectedTotal === iterations;

        return {
          ok,
          message: ok
            ? {
                en: `Race protected correctly: ${successes} successful transfer and ${expectedFailures} expected failures. No server errors.`,
                tr: `Yarış doğru korundu: ${successes} başarılı transfer ve ${expectedFailures} beklenen failure. Server hatası yok.`,
              }
            : {
                en: `Unexpected overdraft result. successes=${successes}, expectedFailures=${expectedFailures}, serverFailures=${serverFailures}, unknownStatus=${unknownStatus}, iterations=${iterations}.`,
                tr: `Beklenmeyen overdraft sonucu. successes=${successes}, expectedFailures=${expectedFailures}, serverFailures=${serverFailures}, unknownStatus=${unknownStatus}, iterations=${iterations}.`,
              },
        };
      },
      howToRead: [
        { en: "Exactly one 201/200 means only one transfer moved the 100 TRY balance.", tr: "Tam bir 201/200, 100 TRY bakiyeyi sadece bir transferin taşıdığı anlamına gelir." },
        { en: "400 means expected insufficient funds after the first success.", tr: "400, ilk başarıdan sonra beklenen yetersiz bakiye anlamına gelir." },
        { en: "409 means expected optimistic concurrency protection under simultaneous writes.", tr: "409, eş zamanlı yazım altında beklenen optimistic concurrency korumasıdır." },
        { en: "500 must remain 0; it means server-side failure, not business rejection.", tr: "500 her zaman 0 kalmalı; business reddi değil server hatasıdır." },
      ],
      checkExplanations: {
        "transfer success or expected business failure": { en: "Accepts 200/201 success, 400 insufficient funds, or 409 concurrency conflict.", tr: "200/201 başarı, 400 yetersiz bakiye veya 409 concurrency conflict kabul edilir." },
        "transfer did not fail with 500": { en: "The API must not crash under the race.", tr: "API yarış altında crash olmamalıdır." },
      },
      thresholdExplanations: {
        checks: { en: "At least 95% of checks must pass.", tr: "Kontrollerin en az %95'i geçmeli." },
        server_errors: { en: "No server errors allowed during the race.", tr: "Yarış sırasında server hatası olmamalı." },
        http_req_duration: { en: "p95 latency should stay under 2000ms.", tr: "p95 gecikme 2000ms altında kalmalı." },
        valid_business_responses: { en: "Every response must be a valid business outcome (success or expected rejection).", tr: "Her cevap geçerli bir iş kuralı sonucu (başarı veya beklenen reddi) olmalı." },
      },
      customCounters: [
        { name: "status_200", explanation: { en: "Successful transfer with 200 if API returns OK.", tr: "API OK dönerse 200 başarılı transfer." } },
        { name: "status_201", explanation: { en: "Successful transfer with 201 Created. Expected count: 1.", tr: "201 Created başarılı transfer. Beklenen sayı: 1." } },
        { name: "status_400", explanation: { en: "Expected insufficient funds failures after balance is spent.", tr: "Bakiye harcandıktan sonra beklenen yetersiz bakiye hataları." } },
        { name: "status_409", explanation: { en: "Expected optimistic concurrency conflicts.", tr: "Beklenen optimistic concurrency conflict cevapları." } },
        { name: "status_500", explanation: { en: "Server failure. Must be 0.", tr: "Server hatası. 0 olmalıdır." } },
        { name: "status_other", explanation: { en: "Untracked status. Investigate if non-zero.", tr: "Takip edilmeyen status. 0 değilse incelenmeli." } },
      ],
      extraMetricRows: (summary) => {
        const successes = counterValue(summary, "status_200") + counterValue(summary, "status_201");
        const expectedFailures = counterValue(summary, "status_400") + counterValue(summary, "status_409");
        const iterations = counterValue(summary, "iterations");
        return [
          { label: { en: "Successful transfers", tr: "Başarılı transfer" }, value: successes, explanation: { en: "Must be exactly 1.", tr: "Tam olarak 1 olmalıdır." } },
          { label: { en: "Expected failures", tr: "Beklenen failure" }, value: expectedFailures, explanation: { en: "400 + 409 business/concurrency outcomes.", tr: "400 + 409 business/concurrency sonuçları." } },
          { label: { en: "Expected response coverage", tr: "Beklenen cevap kapsamı" }, value: iterations === 0 ? "0%" : `${formatNumber(((successes + expectedFailures) / iterations) * 100, 2)}%`, explanation: { en: "Expected statuses / total iterations.", tr: "Beklenen status / toplam iterasyon." } },
          { label: { en: "Valid business response rate", tr: "Geçerli iş kuralı oranı" }, value: rateOf(summary, "valid_business_responses"), explanation: { en: "valid_business_responses rate. Target 100%.", tr: "valid_business_responses oranı. Hedef %100." } },
        ];
      },
      interpretation: (summary) => [
        { en: `${counterValue(summary, "status_200") + counterValue(summary, "status_201")} transfers succeeded. This must be exactly 1.`, tr: `${counterValue(summary, "status_200") + counterValue(summary, "status_201")} transfer başarılı oldu. Tam 1 olmalı.` },
        { en: `${counterValue(summary, "status_400") + counterValue(summary, "status_409")} requests were rejected safely.`, tr: `${counterValue(summary, "status_400") + counterValue(summary, "status_409")} istek güvenli şekilde reddedildi.` },
        { en: `${counterValue(summary, "status_500")} requests returned 500. This must remain 0.`, tr: `${counterValue(summary, "status_500")} istek 500 döndü. Bu 0 kalmalıdır.` },
      ],
      nextAction: (summary) =>
        counterValue(summary, "status_500") > 0
          ? { en: "Investigate 500 errors in ApplicationLogs using k6-overdraft-race correlation ids.", tr: "500 hatalarını k6-overdraft-race correlation id ile ApplicationLogs içinde incele." }
          : { en: "Next: reset fixtures with k6/setup/setup-scenario-data.sql, then run hotspot-load.", tr: "Sonraki: fixture'ları k6/setup/setup-scenario-data.sql ile resetle, sonra hotspot-load çalıştır." },
    }),
    [JSON_REPORT_PATH]: JSON.stringify(data, null, 2),
  };
}
