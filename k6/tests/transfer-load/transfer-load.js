import http from "k6/http";
import { check } from "k6";
import { Counter, Rate } from "k6/metrics";
import { generateHtmlReport, counterValue, formatNumber } from "../../lib/html-report.js";
import {
  trackStatus,
  createTransferParams,
  deriveJsonReportPath,
  buildReportMetadata,
  assertSafeLoadTarget,
  verifyAccounts,
} from "../../lib/seed-helper.js";

const status200 = new Counter("status_200");
const status201 = new Counter("status_201");
const status400 = new Counter("status_400");
const status409 = new Counter("status_409");
const status500 = new Counter("status_500");
const statusOther = new Counter("status_other");
// Business-aware custom metrics. 409 is an expected concurrency conflict,
// not a technical failure; http_req_failed is therefore not used as the gate.
const serverErrors = new Counter("server_errors");
const validBusinessResponses = new Rate("valid_business_responses");

const counters = { status200, status201, status400, status409, status500, statusOther };

export const options = {
  vus: 10,
  duration: "20s",
  thresholds: {
    // Load test: keep latency bounded and require business-valid responses.
    http_req_duration: ["p(95)<500", "p(99)<1000"],
    checks: ["rate>0.95"],
    server_errors: ["count==0"],
    valid_business_responses: ["rate>0.95"],
  },
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const REPORT_PATH = __ENV.REPORT_PATH || "transfer-load-report.html";
const JSON_REPORT_PATH = deriveJsonReportPath(REPORT_PATH);
const SCENARIO_NAME = "transfer-load";

// Fixture-backed accounts (seeded by k6/setup/setup-scenario-data.sql).
// Same hot sender/receiver pair as the original script to preserve contention intent.
const senderAccountId = "33333333-3333-7333-8333-333333333333";
const receiverAccountId = "44444444-4444-7444-8444-444444444444";

assertSafeLoadTarget(BASE_URL);

export function setup() {
  verifyAccounts(BASE_URL, [senderAccountId, receiverAccountId], SCENARIO_NAME);
  return { senderAccountId, receiverAccountId };
}

// Small local helper: read a Rate metric as a formatted percentage.
function rateOf(summary, name) {
  const rate = summary.metrics[name]?.values?.rate;
  return rate === undefined ? "-" : `${formatNumber(rate * 100, 2)}%`;
}

export default function (data) {
  const payload = JSON.stringify({
    senderAccountId: data.senderAccountId,
    receiverAccountId: data.receiverAccountId,
    amount: 1,
    currencyCode: "TRY",
    description: "Performance test transfer",
    idempotencyKey: crypto.randomUUID(),
  });

  const response = http.post(
    `${BASE_URL}/api/transactions/transfer`,
    payload,
    createTransferParams("k6-transfer")
  );

  trackStatus(response, counters);

  // A server error is always a real technical failure, regardless of business outcome.
  if (response.status >= 500) {
    serverErrors.add(1);
    console.log(`status=${response.status} body=${response.body}`);
  }

  // Valid business outcome: success (200/201) or expected concurrency conflict (409).
  // 400/500/other are NOT valid here because all senders are funded and seeded.
  const isValidBusiness =
    response.status === 200 || response.status === 201 || response.status === 409;
  validBusinessResponses.add(isValidBusiness);

  check(response, {
    "transfer success or expected conflict": (res) =>
      res.status === 200 || res.status === 201 || res.status === 409,
    "transfer did not fail with 500": (res) => res.status < 500,
    // Conservative body validation: a successful transfer should carry a transaction id.
    // TODO: Confirm final transfer response schema with API contract (id vs transactionId).
    "successful transfer has transaction id": (res) => {
      if (res.status !== 200 && res.status !== 201) return true;
      try {
        const body = res.json();
        return Boolean(body.id || body.transactionId);
      } catch {
        return false;
      }
    },
  });
}

export function handleSummary(data) {
  return {
    [REPORT_PATH]: generateHtmlReport(data, {
      title: {
        en: "Transfer Load — Hot Account Contention",
        tr: "Transfer Load — Yoğun Hesap Çakışması",
      },
      metadata: buildReportMetadata({
        baseUrl: BASE_URL,
        testType: "load",
        scenarioName: SCENARIO_NAME,
        reportPath: REPORT_PATH,
        jsonReportPath: JSON_REPORT_PATH,
      }),
      purpose: {
        en: "Sends concurrent transfers from one sender to one receiver. It intentionally creates contention to verify consistency, optimistic concurrency, server errors, and latency.",
        tr: "Tek gönderici hesaptan tek alıcı hesaba eş zamanlı transfer gönderir. Tutarlılık, optimistic concurrency, server hataları ve gecikmeyi ölçmek için kasıtlı çakışma üretir.",
      },
      verdict: (summary) => {
        const failedChecks = summary.metrics.checks?.values?.fails || 0;
        const serverFailures = counterValue(summary, "status_500");
        const badRequests = counterValue(summary, "status_400");
        const unknownStatus = counterValue(summary, "status_other");
        const unexpected = serverFailures + badRequests + unknownStatus;
        const ok = failedChecks === 0 && unexpected === 0;

        if (!ok) {
          const parts = { en: [], tr: [] };
          if (serverFailures > 0) {
            parts.en.push(`${serverFailures} server errors (500)`);
            parts.tr.push(`${serverFailures} server hatası (500)`);
          }
          if (badRequests > 0) {
            parts.en.push(`${badRequests} bad requests (400)`);
            parts.tr.push(`${badRequests} geçersiz istek (400)`);
          }
          if (unknownStatus > 0) {
            parts.en.push(`${unknownStatus} unknown status codes`);
            parts.tr.push(`${unknownStatus} tanınmayan status kodu`);
          }
          if (failedChecks > 0) {
            parts.en.push(`${failedChecks} failed checks`);
            parts.tr.push(`${failedChecks} başarısız kontrol`);
          }
          return {
            ok: false,
            message: {
              en: `Unexpected failures detected: ${parts.en.join(", ")}. Inspect ApplicationLogs with k6 correlation ids.`,
              tr: `Beklenmeyen hatalar tespit edildi: ${parts.tr.join(", ")}. k6 correlation id ile ApplicationLogs incelenmeli.`,
            },
          };
        }

        const successes = counterValue(summary, "status_201") + counterValue(summary, "status_200");
        const conflicts = counterValue(summary, "status_409");
        return {
          ok: true,
          message: {
            en: `All responses are expected: ${successes} successful transfers, ${conflicts} optimistic concurrency conflicts (409). No server errors.`,
            tr: `Tüm cevaplar beklenen: ${successes} başarılı transfer, ${conflicts} optimistic concurrency conflict (409). Server hatası yok.`,
          },
        };
      },
      howToRead: [
        { en: "201 means money moved successfully once.", tr: "201 paranın bir kez başarıyla taşındığı anlamına gelir." },
        { en: "409 means expected optimistic concurrency conflict on the hot sender account.", tr: "409 hot sender account üzerinde beklenen optimistic concurrency conflict anlamına gelir." },
        { en: "500 is a real server failure and must stay at 0.", tr: "500 gerçek server hatasıdır ve 0 kalmalıdır." },
        { en: "http_req_failed can be high because k6 treats 409 as failed HTTP, even if business behavior is correct.", tr: "k6 409'u HTTP failed saydığı için http_req_failed yüksek olabilir; bu business davranışı doğru olsa bile olur." },
        { en: "p95 below 500ms is the current latency target.", tr: "p95 değerinin 500ms altında kalması mevcut gecikme hedefidir." },
      ],
      checkExplanations: {
        "transfer success or expected conflict": { en: "Accepts completed transfer or expected 409 conflict. Other statuses are suspicious.", tr: "Tamamlanan transfer veya beklenen 409 conflict kabul edilir. Diğer status'lar şüphelidir." },
        "transfer did not fail with 500": { en: "API must not produce server errors.", tr: "API server hatası üretmemelidir." },
        "successful transfer has transaction id": { en: "A successful (200/201) transfer should carry a transaction id in the response body.", tr: "Başarılı (200/201) bir transfer response body'sinde transaction id taşımalıdır." },
      },
      thresholdExplanations: {
        http_req_duration: { en: "p95 under 500ms and p99 under 1000ms.", tr: "p95 500ms altında, p99 1000ms altında." },
        checks: { en: "At least 95% of checks must pass.", tr: "Kontrollerin en az %95'i geçmeli." },
        server_errors: { en: "No server errors allowed.", tr: "Server hatası olmamalı." },
        valid_business_responses: { en: "At least 95% of responses must be a valid business outcome (success or expected 409).", tr: "Cevapların en az %95'i geçerli iş kuralı sonucu (başarı veya beklenen 409) olmalı." },
      },
      customCounters: [
        { name: "status_200", explanation: { en: "HTTP 200 responses. Usually not expected for create-transfer.", tr: "HTTP 200 cevapları. Create-transfer için genelde beklenmez." } },
        { name: "status_201", explanation: { en: "Successful transfers. Each should move exactly 1 TRY.", tr: "Başarılı transferler. Her biri tam 1 TRY taşımalıdır." } },
        { name: "status_400", explanation: { en: "Bad request or validation error. Unexpected in seeded test.", tr: "Bad request veya validation hatası. Seed'li testte beklenmez." } },
        { name: "status_409", explanation: { en: "Expected optimistic concurrency conflict from hot-account writes.", tr: "Hot-account yazımlarından beklenen optimistic concurrency conflict." } },
        { name: "status_500", explanation: { en: "Server failure. Must be 0.", tr: "Server hatası. 0 olmalıdır." } },
        { name: "status_other", explanation: { en: "Untracked status. Investigate if non-zero.", tr: "Takip edilmeyen status. 0 değilse incelenmeli." } },
      ],
      extraMetricRows: (summary) => {
        const successfulTransfers = counterValue(summary, "status_201") + counterValue(summary, "status_200");
        const iterations = counterValue(summary, "iterations");
        const conflicts = counterValue(summary, "status_409");
        const successRate = iterations === 0 ? 0 : (successfulTransfers / iterations) * 100;
        const conflictRate = iterations === 0 ? 0 : (conflicts / iterations) * 100;
        const serverErr = counterValue(summary, "server_errors");

        return [
          { label: { en: "Successful transfers", tr: "Başarılı transfer" }, value: successfulTransfers, explanation: { en: "Accepted transfers. Balance delta must equal this count.", tr: "Kabul edilen transferler. Bakiye değişimi bu sayıya eşit olmalı." } },
          { label: { en: "Expected conflicts", tr: "Beklenen conflict" }, value: conflicts, explanation: { en: "Optimistic concurrency conflicts from the single sender account.", tr: "Tek sender account kaynaklı optimistic concurrency conflict sayısı." } },
          { label: { en: "Success rate", tr: "Başarı oranı" }, value: `${formatNumber(successRate, 2)}%`, explanation: { en: "Accepted transfers / total iterations.", tr: "Kabul edilen transfer / toplam iterasyon." } },
          { label: { en: "Conflict rate", tr: "Conflict oranı" }, value: `${formatNumber(conflictRate, 2)}%`, explanation: { en: "409 conflicts / total iterations.", tr: "409 conflict / toplam iterasyon." } },
          { label: { en: "Server errors", tr: "Server hatası" }, value: serverErr, explanation: { en: "server_errors counter. Must be 0.", tr: "server_errors sayacı. 0 olmalı." } },
          { label: { en: "Valid business response rate", tr: "Geçerli iş kuralı oranı" }, value: rateOf(summary, "valid_business_responses"), explanation: { en: "valid_business_responses rate. Target >95%.", tr: "valid_business_responses oranı. Hedef >%95." } },
        ];
      },
      interpretation: (summary) => {
        const successfulTransfers = counterValue(summary, "status_201") + counterValue(summary, "status_200");
        const conflicts = counterValue(summary, "status_409");
        const serverFailures = counterValue(summary, "status_500");

        return [
          { en: `${successfulTransfers} transfers were accepted. Sender must decrease and receiver must increase by this amount.`, tr: `${successfulTransfers} transfer kabul edildi. Sender bu kadar azalmalı, receiver bu kadar artmalı.` },
          { en: `${conflicts} requests returned 409. This usually means consistency protection worked under contention.`, tr: `${conflicts} istek 409 döndü. Bu genelde çakışma altında tutarlılık koruması çalıştı demektir.` },
          { en: `${serverFailures} requests returned 500. This must remain 0.`, tr: `${serverFailures} istek 500 döndü. Bu 0 kalmalıdır.` },
        ];
      },
      nextAction: (summary) => {
        const serverFailures = counterValue(summary, "status_500");
        return serverFailures > 0
          ? { en: "Investigate 500 errors in ApplicationLogs using k6 correlation ids.", tr: "500 hatalarını k6 correlation id ile ApplicationLogs içinde incele." }
          : { en: "Next: create a multi-account load test to measure throughput with less contention.", tr: "Sonraki: daha az çakışmayla throughput ölçmek için multi-account load test oluştur." };
      },
    }),
    [JSON_REPORT_PATH]: JSON.stringify(data, null, 2),
  };
}
