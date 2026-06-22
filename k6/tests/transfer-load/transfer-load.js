import http from "k6/http";
import { check } from "k6";
import { Counter } from "k6/metrics";
import { generateHtmlReport, counterValue, formatNumber } from "../../lib/html-report.js";

const status200 = new Counter("status_200");
const status201 = new Counter("status_201");
const status400 = new Counter("status_400");
const status409 = new Counter("status_409");
const status500 = new Counter("status_500");
const statusOther = new Counter("status_other");

export const options = {
  vus: 10,
  duration: "20s",
  thresholds: {
    http_req_duration: ["p(95)<500"],
  },
};

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";
const REPORT_PATH = __ENV.REPORT_PATH || "transfer-load-report.html";

const senderAccountId = "33333333-3333-7333-8333-333333333333";
const receiverAccountId = "44444444-4444-7444-8444-444444444444";

export default function () {
  const payload = JSON.stringify({
    senderAccountId,
    receiverAccountId,
    amount: 1,
    currencyCode: "TRY",
    description: "Performance test transfer",
    idempotencyKey: crypto.randomUUID(),
  });

  const params = {
    headers: {
      "Content-Type": "application/json",
      "X-Correlation-Id": `k6-transfer-${__VU}-${__ITER}`,
    },
  };

  const response = http.post(`${BASE_URL}/api/transactions/transfer`, payload, params);

  if (response.status === 200) status200.add(1);
  else if (response.status === 201) status201.add(1);
  else if (response.status === 400) status400.add(1);
  else if (response.status === 409) status409.add(1);
  else if (response.status === 500) status500.add(1);
  else statusOther.add(1);

  if (response.status >= 500) {
    console.log(`status=${response.status} body=${response.body}`);
  }

  check(response, {
    "transfer success or expected conflict": (res) => res.status === 200 || res.status === 201 || res.status === 409,
    "transfer did not fail with 500": (res) => res.status < 500,
  });
}

export function handleSummary(data) {
  return {
    [REPORT_PATH]: generateHtmlReport(data, {
      title: {
        en: "Transfer Load — Hot Account Contention",
        tr: "Transfer Load — Yoğun Hesap Çakışması",
      },
      purpose: {
        en: "Sends concurrent transfers from one sender to one receiver. It intentionally creates contention to verify consistency, optimistic concurrency, server errors, and latency.",
        tr: "Tek gönderici hesaptan tek alıcı hesaba eş zamanlı transfer gönderir. Tutarlılık, optimistic concurrency, server hataları ve gecikmeyi ölçmek için kasıtlı çakışma üretir.",
      },
      verdict: (summary) => {
        const failedChecks = summary.metrics.checks?.values?.fails || 0;
        const serverFailures = counterValue(summary, "status_500");
        const ok = failedChecks === 0 && serverFailures === 0;

        return {
          ok,
          message: ok
            ? {
                en: "No server failure. Successful transfers and expected 409 conflicts are valid for this test.",
                tr: "Server hatası yok. Başarılı transferler ve beklenen 409 conflict bu test için geçerli sonuçtur.",
              }
            : {
                en: "Failed checks or 500 responses exist. Inspect ApplicationLogs with k6 correlation ids.",
                tr: "Failed check veya 500 response var. k6 correlation id ile ApplicationLogs incelenmeli.",
              },
        };
      },
      howToRead: [
        {
          en: "201 means money moved successfully once.",
          tr: "201 paranın bir kez başarıyla taşındığı anlamına gelir.",
        },
        {
          en: "409 means expected optimistic concurrency conflict on the hot sender account.",
          tr: "409 hot sender account üzerinde beklenen optimistic concurrency conflict anlamına gelir.",
        },
        {
          en: "500 is a real server failure and must stay at 0.",
          tr: "500 gerçek server hatasıdır ve 0 kalmalıdır.",
        },
        {
          en: "http_req_failed can be high because k6 treats 409 as failed HTTP, even if business behavior is correct.",
          tr: "k6 409'u HTTP failed saydığı için http_req_failed yüksek olabilir; bu business davranışı doğru olsa bile olur.",
        },
        {
          en: "p95 below 500ms is the current latency target.",
          tr: "p95 değerinin 500ms altında kalması mevcut gecikme hedefidir.",
        },
      ],
      checkExplanations: {
        "transfer success or expected conflict": {
          en: "Accepts completed transfer or expected 409 conflict. Other statuses are suspicious.",
          tr: "Tamamlanan transfer veya beklenen 409 conflict kabul edilir. Diğer status'lar şüphelidir.",
        },
        "transfer did not fail with 500": {
          en: "API must not produce server errors.",
          tr: "API server hatası üretmemelidir.",
        },
      },
      thresholdExplanations: {
        http_req_duration: {
          en: "95% of requests should complete below 500ms.",
          tr: "İsteklerin %95'i 500ms altında tamamlanmalıdır.",
        },
      },
      customCounters: [
        {
          name: "status_200",
          explanation: {
            en: "HTTP 200 responses. Usually not expected for create-transfer.",
            tr: "HTTP 200 cevapları. Create-transfer için genelde beklenmez.",
          },
        },
        {
          name: "status_201",
          explanation: { en: "Successful transfers. Each should move exactly 1 TRY.", tr: "Başarılı transferler. Her biri tam 1 TRY taşımalıdır." },
        },
        {
          name: "status_400",
          explanation: {
            en: "Bad request or validation error. Unexpected in seeded test.",
            tr: "Bad request veya validation hatası. Seed'li testte beklenmez.",
          },
        },
        {
          name: "status_409",
          explanation: {
            en: "Expected optimistic concurrency conflict from hot-account writes.",
            tr: "Hot-account yazımlarından beklenen optimistic concurrency conflict.",
          },
        },
        { name: "status_500", explanation: { en: "Server failure. Must be 0.", tr: "Server hatası. 0 olmalıdır." } },
        {
          name: "status_other",
          explanation: { en: "Untracked status. Investigate if non-zero.", tr: "Takip edilmeyen status. 0 değilse incelenmeli." },
        },
      ],
      extraMetricRows: (summary) => {
        const successfulTransfers = counterValue(summary, "status_201") + counterValue(summary, "status_200");
        const iterations = counterValue(summary, "iterations");
        const conflicts = counterValue(summary, "status_409");
        const successRate = iterations === 0 ? 0 : (successfulTransfers / iterations) * 100;
        const conflictRate = iterations === 0 ? 0 : (conflicts / iterations) * 100;

        return [
          {
            label: { en: "Successful transfers", tr: "Başarılı transfer" },
            value: successfulTransfers,
            explanation: {
              en: "Accepted transfers. Balance delta must equal this count.",
              tr: "Kabul edilen transferler. Bakiye değişimi bu sayıya eşit olmalı.",
            },
          },
          {
            label: { en: "Expected conflicts", tr: "Beklenen conflict" },
            value: conflicts,
            explanation: {
              en: "Optimistic concurrency conflicts from the single sender account.",
              tr: "Tek sender account kaynaklı optimistic concurrency conflict sayısı.",
            },
          },
          {
            label: { en: "Success rate", tr: "Başarı oranı" },
            value: `${formatNumber(successRate, 2)}%`,
            explanation: { en: "Accepted transfers / total iterations.", tr: "Kabul edilen transfer / toplam iterasyon." },
          },
          {
            label: { en: "Conflict rate", tr: "Conflict oranı" },
            value: `${formatNumber(conflictRate, 2)}%`,
            explanation: { en: "409 conflicts / total iterations.", tr: "409 conflict / toplam iterasyon." },
          },
        ];
      },
      interpretation: (summary) => {
        const successfulTransfers = counterValue(summary, "status_201") + counterValue(summary, "status_200");
        const conflicts = counterValue(summary, "status_409");
        const serverFailures = counterValue(summary, "status_500");

        return [
          {
            en: `${successfulTransfers} transfers were accepted. Sender must decrease and receiver must increase by this amount.`,
            tr: `${successfulTransfers} transfer kabul edildi. Sender bu kadar azalmalı, receiver bu kadar artmalı.`,
          },
          {
            en: `${conflicts} requests returned 409. This usually means consistency protection worked under contention.`,
            tr: `${conflicts} istek 409 döndü. Bu genelde çakışma altında tutarlılık koruması çalıştı demektir.`,
          },
          {
            en: `${serverFailures} requests returned 500. This must remain 0.`,
            tr: `${serverFailures} istek 500 döndü. Bu 0 kalmalıdır.`,
          },
        ];
      },
      nextAction: (summary) => {
        const serverFailures = counterValue(summary, "status_500");
        return serverFailures > 0
          ? {
              en: "Investigate 500 errors in ApplicationLogs using k6 correlation ids.",
              tr: "500 hatalarını k6 correlation id ile ApplicationLogs içinde incele.",
            }
          : {
              en: "Next: create a multi-account load test to measure throughput with less contention.",
              tr: "Sonraki: daha az çakışmayla throughput ölçmek için multi-account load test oluştur.",
            };
      },
    }),
  };
}
