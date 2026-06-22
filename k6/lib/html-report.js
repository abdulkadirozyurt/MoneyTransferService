export function counterValue(data, name) {
  return data.metrics[name]?.values?.count || 0;
}

export function metricValue(data, name, key) {
  return data.metrics[name]?.values?.[key];
}

export function rateValue(data, name) {
  return data.metrics[name]?.values?.rate;
}

export function formatNumber(value, digits = 2) {
  if (value === undefined || value === null || Number.isNaN(value)) return "-";
  return Number(value).toFixed(digits);
}

export function formatPercent(value) {
  if (value === undefined || value === null || Number.isNaN(value)) return "-";
  return `${formatNumber(value * 100, 2)}%`;
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function tr(key, lang) {
  const dict = {
    en: {
      verdict: "Verdict",
      purpose: "Purpose",
      read: "How to read",
      kpi: "Dashboard",
      checks: "Checks",
      thresholds: "Thresholds",
      counters: "Status counters",
      interpretation: "Interpretation",
      next: "Next action",
      metric: "Metric",
      value: "Value",
      explanation: "Explanation",
      passed: "Passed",
      failed: "Failed",
      meaning: "Meaning",
      rule: "Rule",
      status: "Status",
      navOverview: "Overview",
      navDetails: "Details",
      language: "Language",
    },
    tr: {
      verdict: "Karar",
      purpose: "Amaç",
      read: "Nasıl okunur",
      kpi: "Dashboard",
      checks: "Kontroller",
      thresholds: "Eşikler",
      counters: "Durum sayaçları",
      interpretation: "Yorum",
      next: "Sonraki aksiyon",
      metric: "Metrik",
      value: "Değer",
      explanation: "Açıklama",
      passed: "Başarılı",
      failed: "Başarısız",
      meaning: "Anlamı",
      rule: "Kural",
      status: "Durum",
      navOverview: "Özet",
      navDetails: "Detay",
      language: "Dil",
    },
  };

  return dict[lang]?.[key] || dict.en[key] || key;
}

function localize(value, lang) {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return value[lang] || value.en || value.tr || "";
  }

  return value;
}

function localizeList(list, lang) {
  return (list || []).map((item) => localize(item, lang));
}

function buildCheckRows(data, explanations, lang) {
  const checks = data.root_group?.checks || [];
  if (checks.length === 0) return `<p class="muted">No checks.</p>`;

  return checks.map((check) => {
    const passes = check.passes || 0;
    const fails = check.fails || 0;
    const total = passes + fails;
    const passRate = total === 0 ? "-" : `${formatNumber((passes / total) * 100, 2)}%`;
    const ok = fails === 0;

    return `<tr>
      <td><span class="dot ${ok ? "good" : "bad-dot"}"></span>${escapeHtml(check.name)}</td>
      <td><strong>${passRate}</strong></td>
      <td>${passes}</td>
      <td>${fails}</td>
      <td>${escapeHtml(localize(explanations[check.name], lang) || "-")}</td>
    </tr>`;
  }).join("\n");
}

function buildThresholdRows(data, explanations, lang) {
  const rows = [];

  for (const [metricName, metric] of Object.entries(data.metrics)) {
    if (!metric.thresholds) continue;

    for (const [expression, threshold] of Object.entries(metric.thresholds)) {
      rows.push(`<tr>
        <td>${escapeHtml(metricName)}</td>
        <td><code>${escapeHtml(expression)}</code></td>
        <td><span class="pill ${threshold.ok ? "pass" : "fail"}">${threshold.ok ? "PASS" : "FAIL"}</span></td>
        <td>${escapeHtml(localize(explanations[metricName], lang) || "-")}</td>
      </tr>`);
    }
  }

  return rows.length ? rows.join("\n") : `<tr><td colspan="4" class="muted">No thresholds.</td></tr>`;
}

function buildCounterCards(data, counters, lang) {
  if (!counters || counters.length === 0) {
    return `<div class="empty">No custom counters.</div>`;
  }

  return counters.map((counter) => `<article class="counter-card">
    <div class="counter-name">${escapeHtml(counter.name)}</div>
    <div class="counter-value">${counterValue(data, counter.name)}</div>
    <p>${escapeHtml(localize(counter.explanation, lang))}</p>
  </article>`).join("\n");
}

function metricCard(label, value, explanation, tone = "neutral") {
  return `<article class="metric-card ${tone}">
    <div class="metric-label">${escapeHtml(label)}</div>
    <div class="metric-value">${escapeHtml(value)}</div>
    <p>${escapeHtml(explanation)}</p>
  </article>`;
}

function baseMetricCards(data, lang) {
  const text = {
    en: {
      req: "Total requests",
      reqExp: "All HTTP requests sent during this run.",
      fail: "HTTP fail rate",
      failExp: "k6 marks 4xx/5xx as failed. Expected 409 conflicts may make this high.",
      p95: "p95 latency",
      p95Exp: "95% of requests completed below this value.",
      avg: "Average latency",
      avgExp: "Mean response time across requests.",
      max: "Max latency",
      maxExp: "Slowest observed request.",
      iterations: "Iterations",
      iterationsExp: "Completed scenario iterations.",
    },
    tr: {
      req: "Toplam istek",
      reqExp: "Bu koşuda gönderilen tüm HTTP istekleri.",
      fail: "HTTP hata oranı",
      failExp: "k6 4xx/5xx cevapları failed sayar. Beklenen 409 conflict bu oranı yükseltebilir.",
      p95: "p95 gecikme",
      p95Exp: "İsteklerin %95'i bu sürenin altında tamamlandı.",
      avg: "Ortalama gecikme",
      avgExp: "Tüm isteklerin ortalama cevap süresi.",
      max: "Maks gecikme",
      maxExp: "Gözlenen en yavaş istek.",
      iterations: "Iterasyon",
      iterationsExp: "Tamamlanan senaryo iterasyonu.",
    },
  }[lang];

  return [
    metricCard(text.req, counterValue(data, "http_reqs"), text.reqExp),
    metricCard(text.fail, formatPercent(rateValue(data, "http_req_failed")), text.failExp, rateValue(data, "http_req_failed") > 0 ? "warn" : "good"),
    metricCard(text.p95, `${formatNumber(metricValue(data, "http_req_duration", "p(95)"))} ms`, text.p95Exp),
    metricCard(text.avg, `${formatNumber(metricValue(data, "http_req_duration", "avg"))} ms`, text.avgExp),
    metricCard(text.max, `${formatNumber(metricValue(data, "http_req_duration", "max"))} ms`, text.maxExp),
    metricCard(text.iterations, counterValue(data, "iterations"), text.iterationsExp),
  ];
}

function buildMetricTable(data, extraMetricRows, lang) {
  const rows = [
    ["http_reqs", counterValue(data, "http_reqs"), localize({ en: "Total HTTP requests sent by k6.", tr: "k6 tarafından gönderilen toplam HTTP isteği." }, lang)],
    ["http_req_failed", formatPercent(rateValue(data, "http_req_failed")), localize({ en: "Non-2xx/3xx response ratio from k6 perspective.", tr: "k6 açısından 2xx/3xx dışı cevap oranı." }, lang)],
    ["http_req_duration avg", `${formatNumber(metricValue(data, "http_req_duration", "avg"))} ms`, localize({ en: "Average response time.", tr: "Ortalama cevap süresi." }, lang)],
    ["http_req_duration med", `${formatNumber(metricValue(data, "http_req_duration", "med"))} ms`, localize({ en: "Median response time.", tr: "Ortanca cevap süresi." }, lang)],
    ["http_req_duration p90", `${formatNumber(metricValue(data, "http_req_duration", "p(90)"))} ms`, localize({ en: "90th percentile latency.", tr: "90. yüzdelik gecikme." }, lang)],
    ["http_req_duration p95", `${formatNumber(metricValue(data, "http_req_duration", "p(95)"))} ms`, localize({ en: "95th percentile latency.", tr: "95. yüzdelik gecikme." }, lang)],
    ["http_req_duration max", `${formatNumber(metricValue(data, "http_req_duration", "max"))} ms`, localize({ en: "Slowest request.", tr: "En yavaş istek." }, lang)],
    ["iterations", counterValue(data, "iterations"), localize({ en: "Completed scenario iterations.", tr: "Tamamlanan senaryo iterasyonu." }, lang)],
    ...(extraMetricRows || []).map((item) => [item.label, item.value, localize(item.explanation, lang)]),
  ];

  return rows.map(([label, value, explanation]) => `<tr><td>${escapeHtml(label)}</td><td><strong>${escapeHtml(value)}</strong></td><td>${escapeHtml(explanation)}</td></tr>`).join("\n");
}

function renderLanguage(data, options, lang) {
  const verdict = options.verdict(data);
  const title = localize(options.title, lang);
  const extraRows = options.extraMetricRows?.(data) || [];
  const customCounters = options.customCounters || [];

  return `<div class="lang-panel" data-lang="${lang}">
    <aside class="sidebar">
      <div class="brand">k6 Report</div>
      <nav>
        <a href="#${lang}-overview">${tr("navOverview", lang)}</a>
        <a href="#${lang}-details">${tr("navDetails", lang)}</a>
        <a href="#${lang}-checks">${tr("checks", lang)}</a>
        <a href="#${lang}-counters">${tr("counters", lang)}</a>
        <a href="#${lang}-next">${tr("next", lang)}</a>
      </nav>
    </aside>

    <main class="content">
      <header class="hero" id="${lang}-overview">
        <div>
          <div class="eyebrow">${tr("language", lang)}: ${lang.toUpperCase()}</div>
          <h1>${escapeHtml(title)}</h1>
          <p>${escapeHtml(localize(options.purpose, lang))}</p>
        </div>
        <div class="verdict-card ${verdict.ok ? "pass" : "fail"}">
          <span>${tr("verdict", lang)}</span>
          <strong>${verdict.ok ? "PASS" : "FAIL"}</strong>
          <p>${escapeHtml(localize(verdict.message, lang))}</p>
        </div>
      </header>

      <section class="metric-grid" aria-label="${tr("kpi", lang)}">
        ${baseMetricCards(data, lang).join("\n")}
        ${extraRows.map((item) => metricCard(localize(item.label, lang), item.value, localize(item.explanation, lang))).join("\n")}
      </section>

      <section class="panel compact">
        <h2>${tr("read", lang)}</h2>
        <ul class="dense-list">${localizeList(options.howToRead, lang).map((item) => `<li>${escapeHtml(item)}</li>`).join("\n")}</ul>
      </section>

      <section class="two-col" id="${lang}-details">
        <article class="panel">
          <h2>${tr("checks", lang)}</h2>
          <div class="table-wrap"><table><thead><tr><th>Check</th><th>Pass %</th><th>${tr("passed", lang)}</th><th>${tr("failed", lang)}</th><th>${tr("meaning", lang)}</th></tr></thead><tbody>${buildCheckRows(data, options.checkExplanations || {}, lang)}</tbody></table></div>
        </article>
        <article class="panel">
          <h2>${tr("thresholds", lang)}</h2>
          <div class="table-wrap"><table><thead><tr><th>${tr("metric", lang)}</th><th>${tr("rule", lang)}</th><th>${tr("status", lang)}</th><th>${tr("meaning", lang)}</th></tr></thead><tbody>${buildThresholdRows(data, options.thresholdExplanations || {}, lang)}</tbody></table></div>
        </article>
      </section>

      <section class="panel" id="${lang}-counters">
        <h2>${tr("counters", lang)}</h2>
        <div class="counter-grid">${buildCounterCards(data, customCounters, lang)}</div>
      </section>

      <section class="two-col">
        <article class="panel">
          <h2>${tr("metric", lang)} ${tr("details", lang) || "Details"}</h2>
          <div class="table-wrap"><table><thead><tr><th>${tr("metric", lang)}</th><th>${tr("value", lang)}</th><th>${tr("explanation", lang)}</th></tr></thead><tbody>${buildMetricTable(data, extraRows, lang)}</tbody></table></div>
        </article>
        <article class="panel" id="${lang}-next">
          <h2>${tr("interpretation", lang)}</h2>
          <ul class="dense-list">${localizeList(options.interpretation(data), lang).map((item) => `<li>${escapeHtml(item)}</li>`).join("\n")}</ul>
          <h2>${tr("next", lang)}</h2>
          <p>${escapeHtml(localize(options.nextAction(data), lang))}</p>
        </article>
      </section>
    </main>
  </div>`;
}

export function generateHtmlReport(data, options) {
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(localize(options.title, "en"))}</title>
  <style>
    :root { color-scheme: light; --bg:#eef3fb; --panel:#ffffff; --text:#172033; --muted:#64748b; --line:#dbe3ef; --blue:#2563eb; --green:#16a34a; --red:#dc2626; --amber:#d97706; --shadow:0 10px 30px rgba(15,23,42,.08); }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body { margin:0; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Arial, sans-serif; color:var(--text); background: radial-gradient(circle at top left, #dbeafe 0, transparent 34rem), var(--bg); }
    .toolbar { position: sticky; top:0; z-index:20; display:flex; justify-content:flex-end; gap:8px; padding:10px 18px; background:rgba(238,243,251,.82); backdrop-filter: blur(10px); border-bottom:1px solid var(--line); }
    .toolbar button { border:1px solid var(--line); background:white; color:var(--text); border-radius:999px; padding:8px 14px; cursor:pointer; font-weight:700; }
    .toolbar button.active { background:var(--blue); color:white; border-color:var(--blue); }
    .lang-panel { display:none; min-height:100vh; }
    .lang-panel.active { display:grid; grid-template-columns:240px 1fr; }
    .sidebar { position:sticky; top:49px; height:calc(100vh - 49px); padding:24px 18px; border-right:1px solid var(--line); background:rgba(255,255,255,.72); backdrop-filter: blur(10px); }
    .brand { font-weight:900; font-size:20px; margin-bottom:24px; letter-spacing:-.03em; }
    nav { display:flex; flex-direction:column; gap:8px; }
    nav a { text-decoration:none; color:var(--muted); padding:10px 12px; border-radius:10px; font-weight:700; }
    nav a:hover { background:#eaf1ff; color:var(--blue); }
    .content { width:100%; max-width: none; padding:24px; }
    .hero { display:grid; grid-template-columns:1fr 360px; gap:18px; align-items:stretch; margin-bottom:18px; }
    .hero > div:first-child, .verdict-card, .panel, .metric-card, .counter-card { background:rgba(255,255,255,.94); border:1px solid var(--line); border-radius:18px; box-shadow:var(--shadow); }
    .hero > div:first-child { padding:26px; }
    .eyebrow { color:var(--blue); font-weight:900; text-transform:uppercase; font-size:12px; letter-spacing:.12em; }
    h1 { margin:8px 0 10px; font-size:clamp(28px,4vw,48px); letter-spacing:-.05em; line-height:1.02; }
    h2 { margin:0 0 14px; font-size:18px; letter-spacing:-.02em; }
    p { color:var(--muted); line-height:1.55; margin:0; }
    .verdict-card { padding:24px; display:flex; flex-direction:column; justify-content:center; border-left:8px solid var(--green); }
    .verdict-card.fail { border-left-color:var(--red); }
    .verdict-card span { color:var(--muted); font-weight:800; text-transform:uppercase; font-size:12px; }
    .verdict-card strong { font-size:44px; letter-spacing:-.06em; color:var(--green); }
    .verdict-card.fail strong { color:var(--red); }
    .metric-grid { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:14px; margin-bottom:18px; }
    .metric-card { padding:18px; min-height:142px; }
    .metric-card.good { border-top:4px solid var(--green); }
    .metric-card.warn { border-top:4px solid var(--amber); }
    .metric-label, .counter-name { color:var(--muted); font-size:12px; text-transform:uppercase; font-weight:900; letter-spacing:.08em; }
    .metric-value, .counter-value { font-size:30px; font-weight:950; letter-spacing:-.05em; margin:8px 0; }
    .panel { padding:20px; margin-bottom:18px; }
    .panel.compact { padding:16px 20px; }
    .two-col { display:grid; grid-template-columns:1fr 1fr; gap:18px; }
    .counter-grid { display:grid; grid-template-columns:repeat(6,minmax(0,1fr)); gap:12px; }
    .counter-card { padding:16px; min-height:130px; }
    .table-wrap { overflow:auto; max-height:360px; border:1px solid var(--line); border-radius:12px; }
    table { width:100%; border-collapse:collapse; font-size:14px; }
    th,td { padding:10px 12px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; }
    th { position:sticky; top:0; background:#f8fafc; z-index:1; color:#334155; }
    code { background:#f1f5f9; border:1px solid #e2e8f0; padding:2px 5px; border-radius:6px; }
    .pill { display:inline-block; border-radius:999px; padding:4px 9px; font-weight:900; font-size:12px; }
    .pill.pass { background:#dcfce7; color:#166534; }
    .pill.fail { background:#fee2e2; color:#991b1b; }
    .dot { display:inline-block; width:9px; height:9px; border-radius:50%; margin-right:8px; background:var(--green); }
    .bad-dot { background:var(--red); }
    .dense-list { margin:0; padding-left:18px; color:var(--muted); line-height:1.55; }
    .empty { color:var(--muted); padding:20px; }
    @media (max-width: 1180px) { .metric-grid { grid-template-columns:repeat(2,minmax(0,1fr)); } .counter-grid { grid-template-columns:repeat(3,minmax(0,1fr)); } .hero { grid-template-columns:1fr; } }
    @media (max-width: 820px) { .lang-panel.active { display:block; } .sidebar { position:relative; top:0; height:auto; border-right:0; border-bottom:1px solid var(--line); } nav { flex-direction:row; flex-wrap:wrap; } .content { padding:14px; } .two-col { grid-template-columns:1fr; } .metric-grid { grid-template-columns:1fr; } .counter-grid { grid-template-columns:1fr 1fr; } }
    @media (max-width: 520px) { .counter-grid { grid-template-columns:1fr; } .toolbar { justify-content:center; } }
  </style>
</head>
<body>
  <div class="toolbar"><button class="active" data-switch="en">English</button><button data-switch="tr">Türkçe</button></div>
  ${renderLanguage(data, options, "en")}
  ${renderLanguage(data, options, "tr")}
  <script>
    const buttons = document.querySelectorAll('[data-switch]');
    const panels = document.querySelectorAll('.lang-panel');
    function setLang(lang) {
      buttons.forEach((button) => button.classList.toggle('active', button.dataset.switch === lang));
      panels.forEach((panel) => panel.classList.toggle('active', panel.dataset.lang === lang));
      document.documentElement.lang = lang;
    }
    buttons.forEach((button) => button.addEventListener('click', () => setLang(button.dataset.switch)));
    setLang('en');
  </script>
</body>
</html>`;
}
