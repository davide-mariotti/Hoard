"use strict";
// Hoard's window. The exe sends: init, settings, drives, progress (while reading), done, view (one folder at a time).
// The page sends back: ready, scan, home, view, reveal, admin, set, open. Nothing here touches the network.

const $ = id => document.getElementById(id);
const host = window.chrome && window.chrome.webview;
const send = m => host && host.postMessage(m);
const icon = (n, cls) => `<svg class="i ${cls || ""}" viewBox="0 0 24 24" aria-hidden="true">${ICONS[n] || ICONS.file}</svg>`;
const esc = s => String(s == null ? "" : s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

// the kinds of file, in the order the exe numbers them (Cats in Hoard.cs)
const CAT_ICONS = ["film", "archive", "image", "app-window", "music", "code-xml", "file-text", "cog", "file"];

const TEXT = {
  it: {
    homeKicker: "I tuoi dischi",
    homeTitle: 'Cosa riempie<span class="serif grad">il tuo disco?</span>',
    homeLead: "Scegli un disco. Hoard lo legge cartella per cartella e ti mostra dove sono finiti i gigabyte. Non cancella niente: quello lo decidi tu, da Esplora file.",
    kinds: { fixed: "disco interno", removable: "disco rimovibile", network: "disco di rete" }, system: "disco di sistema",
    used: "usati", free: "liberi", total: "in tutto", analyze: "Analizza", noDrives: "Nessun disco da leggere.",
    scanKicker: "In corso", scanTitle: r => `Sto leggendo <span class="serif grad">${r}</span>`,
    found: "Trovati", filesL: "File", dirsL: "Cartelle", time: "Tempo", stop: "Ferma",
    liveTitle: "Le cartelle principali, mentre crescono", rootFiles: "file nella radice",
    readIn: s => `letto in ${s} s`, resTitle: (s, f) => `<span class="serif grad">${s}</span> in ${f} file`,
    rescan: "Rileggi", otherDrives: "Altri dischi", explorer: "Apri in Esplora file", up: "Su di un livello",
    tabs: ["Cartella", "Più grandi", "Da guardare"],
    cats: ["Video", "Archivi e immagini disco", "Immagini", "Programmi e giochi", "Audio", "Codice e sviluppo", "Documenti", "Sistema, log e cache", "Altro"],
    ofDisk: p => `${p} del disco usato`, files: n => `${n} file`, folders: n => `${n} cartelle`,
    byType: "Per tipo", inside: "Dentro", own: "file in questa cartella", more: n => `${n} elementi piccoli`,
    folder: "Cartella", file: "File", denied: "Hoard non ha potuto leggerla",
    hint: "Clic su una cartella per entrarci · clic destro: mostra in Esplora file · Backspace: torna su",
    hEnter: "Clic per entrare · clic destro: Esplora file", hFile: "Clic destro: mostra in Esplora file", hMore: "Troppo piccoli per un riquadro ciascuno",
    reveal: "Mostra in Esplora file", go: "Vai alla cartella", mapLabel: "Mappa della cartella: ogni riquadro è grande quanto lo spazio che occupa",
    bigNote: "I 50 file più grandi di tutto il disco. Clic per vederli nella mappa.",
    hogsNote: "Cartelle che si riempiono da sole e che di solito si possono svuotare. Hoard non tocca niente: guarda e decidi tu.",
    noHogs: "Niente di ovvio da togliere. O sei una persona ordinata, o il disordine sta nelle tue cartelle: guarda la mappa.",
    hogs: {
      temp: ["File temporanei", "Programmi e installazioni li lasciano qui e se ne dimenticano. Si svuota da Impostazioni › Sistema › Archiviazione › File temporanei."],
      downloads: ["Download", "Tutto quello che hai scaricato, compresi gli installer usati una volta sola."],
      recycle: ["Cestino", "File già cancellati che occupano ancora spazio. Svuotalo e tornano liberi."],
      winold: ["Windows.old", "La versione di Windows prima dell'ultimo aggiornamento. Windows la toglie da solo dopo 10 giorni, o subito da Pulizia disco."],
      dumps: ["Crash dump", "Le istantanee dei programmi andati in crash. Servono a chi li sviluppa, non a te."],
      cache: n => [`Cache di ${n}`, "Pacchetti scaricati una volta e tenuti da parte. Si possono cancellare: tornano da soli alla prossima installazione."],
      node: n => [`node_modules (${n})`, "Le dipendenze dei progetti JavaScript. Quelle dei progetti fermi si cancellano senza pensieri: npm install le ricrea."],
      hiberfil: ["File di ibernazione", "Windows ci salva la RAM per l'ibernazione e l'avvio rapido. Si toglie con powercfg /h off, ma perdi tutte e due."],
    },
    gapTitle: g => `${g} che Hoard non vede`,
    gapText: (u, s, d) => `Il disco ne usa ${u}, Hoard ne ha trovati ${s}. Il resto sta nei punti di ripristino, nei file di sistema e nelle cartelle chiuse agli utenti normali${d ? ` (${d})` : ""}.`,
    gapAdmin: (u, s) => `Il disco ne usa ${u}, Hoard ne ha trovati ${s}. Il resto sono punti di ripristino e file che solo Windows può leggere, anche da amministratore.`,
    deniedTitle: n => `${n} cartelle non lette`, deniedText: "Sono chiuse agli utenti normali. Da amministratore Hoard ne legge quasi tutte.",
    overTitle: o => `${o} contati in più`,
    overText: "Succede con i file compressi da Windows e con quelli che hanno più nomi (hard link, Windows ne è pieno): Hoard li conta per intero, come Esplora file.",
    admin: "Riavvia come amministratore",
    settings: "Impostazioni", startup: "Avvia con Windows",
    startupText: "Al login resta nella tray e tiene d'occhio lo spazio libero, senza aprire finestre.",
    startupPortable: "Versione portable: le impostazioni stanno in Hoard.ini, accanto all'exe, e Hoard non parte da solo.",
    alerts: "Avvisa quando un disco si riempie", limit: "Sotto il",
    alertsText: "Un avviso solo, quando un disco interno scende sotto la soglia. Nella tray compare la percentuale libera: arancione vicino alla soglia, rossa sotto.",
    about: v => `Hoard ${v} · licenza MIT · niente internet, niente telemetria. Legge e basta: non cancella niente.`,
    links: ["Codice sorgente", "Offrimi un caffè"],
    footer: v => `Hoard ${v} · fatto da Davide Mariotti · legge e basta, non cancella niente`,
    pill: (d, p) => `${d} ${p} libero`, adminMode: "amministratore",
  },
  en: {
    homeKicker: "Your drives",
    homeTitle: 'What\'s filling<span class="serif grad">your disk?</span>',
    homeLead: "Pick a drive. Hoard reads it folder by folder and shows you where the gigabytes went. It deletes nothing: that's your call, in Explorer.",
    kinds: { fixed: "internal drive", removable: "removable drive", network: "network drive" }, system: "system drive",
    used: "used", free: "free", total: "in all", analyze: "Analyze", noDrives: "No drives to read.",
    scanKicker: "In progress", scanTitle: r => `Reading <span class="serif grad">${r}</span>`,
    found: "Found", filesL: "Files", dirsL: "Folders", time: "Time", stop: "Stop",
    liveTitle: "The main folders, as they grow", rootFiles: "files in the root",
    readIn: s => `read in ${s} s`, resTitle: (s, f) => `<span class="serif grad">${s}</span> in ${f} files`,
    rescan: "Read again", otherDrives: "Other drives", explorer: "Open in Explorer", up: "Up one level",
    tabs: ["Folder", "Biggest", "Worth a look"],
    cats: ["Video", "Archives and disk images", "Pictures", "Programs and games", "Audio", "Code and dev", "Documents", "System, logs and cache", "Other"],
    ofDisk: p => `${p} of the used space`, files: n => `${n} files`, folders: n => `${n} folders`,
    byType: "By kind", inside: "Inside", own: "files in this folder", more: n => `${n} small items`,
    folder: "Folder", file: "File", denied: "Hoard couldn't read it",
    hint: "Click a folder to go in · right click: show in Explorer · Backspace: go up",
    hEnter: "Click to go in · right click: Explorer", hFile: "Right click: show in Explorer", hMore: "Too small for a tile each",
    reveal: "Show in Explorer", go: "Go to the folder", mapLabel: "Map of the folder: each tile is as big as the space it takes",
    bigNote: "The 50 biggest files on the whole drive. Click one to see it on the map.",
    hogsNote: "Folders that fill up on their own and can usually be emptied. Hoard touches nothing: you look, you decide.",
    noHogs: "Nothing obvious to clear. Either you're a tidy person, or the mess is in your own folders: check the map.",
    hogs: {
      temp: ["Temporary files", "Programs and installers leave them here and forget about them. Clear them in Settings › System › Storage › Temporary files."],
      downloads: ["Downloads", "Everything you downloaded, including the installers you ran once."],
      recycle: ["Recycle Bin", "Deleted files still taking space. Empty it and the space comes back."],
      winold: ["Windows.old", "The Windows you had before the last upgrade. Windows removes it after 10 days, or right away from Disk Cleanup."],
      dumps: ["Crash dumps", "Snapshots of programs that crashed. Useful to whoever makes them, not to you."],
      cache: n => [`${n} cache`, "Packages downloaded once and kept around. Safe to delete: they come back by themselves at the next install."],
      node: n => [`node_modules (${n})`, "The dependencies of JavaScript projects. Those of idle projects can go without a second thought: npm install brings them back."],
      hiberfil: ["Hibernation file", "Windows saves the RAM here for hibernation and fast startup. powercfg /h off removes it, and both of those with it."],
    },
    gapTitle: g => `${g} Hoard can't see`,
    gapText: (u, s, d) => `The drive uses ${u}, Hoard found ${s}. The rest is restore points, system files and folders closed to normal users${d ? ` (${d})` : ""}.`,
    gapAdmin: (u, s) => `The drive uses ${u}, Hoard found ${s}. The rest is restore points and files only Windows can read, even as administrator.`,
    deniedTitle: n => `${n} folders not read`, deniedText: "They're closed to normal users. As administrator Hoard reads nearly all of them.",
    overTitle: o => `${o} counted twice`,
    overText: "It happens with files Windows compresses and with files that have more than one name (hard links, Windows is full of them): Hoard counts them in full, like Explorer does.",
    admin: "Restart as administrator",
    settings: "Settings", startup: "Start with Windows",
    startupText: "At login it stays in the tray and keeps an eye on free space, no window.",
    startupPortable: "Portable version: settings live in Hoard.ini, next to the exe, and Hoard doesn't start by itself.",
    alerts: "Warn me when a drive fills up", limit: "Under",
    alertsText: "One warning, when an internal drive drops under the limit. The tray shows the free percentage: amber near the limit, red under it.",
    about: v => `Hoard ${v} · MIT license · no internet, no telemetry. It only reads: it deletes nothing.`,
    links: ["Source code", "Buy me a coffee"],
    footer: v => `Hoard ${v} · made by Davide Mariotti · it only reads, it deletes nothing`,
    pill: (d, p) => `${d} ${p} free`, adminMode: "administrator",
  },
};
const CACHES = { npm: "npm", yarn: "Yarn", pip: "pip", nuget: "NuGet", gradle: "Gradle" };

let T = TEXT.en, locale = "en-US";
const nf = {};
// up to d decimals, no trailing zeros: 6.1 GB, not 6.10 GB
const num = (v, d) => (nf[d || 0] || (nf[d || 0] = new Intl.NumberFormat(locale, { maximumFractionDigits: d || 0 }))).format(v);
// bytes the way Explorer counts them (1 GB = 1024³), so the numbers match what Windows shows
function sizeParts(b) {
  const u = ["B", "KB", "MB", "GB", "TB", "PB"];
  let i = 0;
  while (b >= 1024 && i < u.length - 1) { b /= 1024; i++; }
  return [num(b, i === 0 || b >= 100 ? 0 : 1), u[i]];
}
const size = b => sizeParts(b).join(" ");
const pct = (a, b) => b > 0 ? `${num(a / b * 100, a / b < 0.1 ? 1 : 0)}%` : "";
const drive = r => String(r || "").replace(/\\$/, "");

const state = { init: null, settings: { alertAt: 10 }, drives: [], screen: "", progress: null, summary: null, view: null, tab: 0, selected: null, started: 0 };
let tiles = [], liveRows = new Map(), resizer = null;

if (host) host.addEventListener("message", e => {
  const m = e.data;
  if (m.t === "init") { state.init = m; T = TEXT[m.lang] || TEXT.en; locale = m.lang === "it" ? "it-IT" : "en-US"; document.documentElement.lang = m.lang; renderStatic(); show("home"); }
  else if (m.t === "settings") { state.settings = m; renderDrawer(); renderPill(); }
  else if (m.t === "drives") { state.drives = m.drives; renderPill(); if (state.screen === "home") renderDrives(); }
  else if (m.t === "progress") { state.progress = m; if (state.screen !== "scanning") show("scanning"); else updateScan(); }
  else if (m.t === "done") { state.summary = m; state.view = null; state.started = 0; show("result"); go(0); }
  else if (m.t === "view") { state.view = m; renderView(); }
});
send({ t: "ready" });

function renderStatic() {
  $("logo").innerHTML = icon("hard-drive");
  $("version").textContent = "v" + state.init.version;
  $("openSettings").innerHTML = icon("settings");
  $("openSettings").setAttribute("aria-label", T.settings);
  $("openSettings").onclick = () => document.body.classList.add("open");
  $("scrim").onclick = () => document.body.classList.remove("open");
}

// the free space of the system drive, the same number the tray shows when it gets low
function renderPill() {
  const d = state.drives.find(x => x.system) || state.drives[0];
  const p = $("pill");
  if (!d || !state.init) { p.hidden = true; return; }
  const free = d.free / d.total * 100, at = state.settings.alertAt || 10;
  p.hidden = false;
  p.className = "pill lvl-" + (free < at ? 2 : free < at + 5 ? 1 : 0);
  p.innerHTML = `<span class="dot"></span>${esc(T.pill(drive(d.root), `${Math.floor(free)}%`))}${state.init.admin ? ` · ${esc(T.adminMode)}` : ""}`;
}

function reveal() { requestAnimationFrame(() => requestAnimationFrame(() => document.querySelectorAll(".reveal:not(.in)").forEach(el => el.classList.add("in")))); }

function show(screen) {
  state.screen = screen;
  if (resizer) { resizer.disconnect(); resizer = null; }
  $("tip").hidden = true;
  const main = $("main");
  main.className = "wrap" + (screen === "result" ? " screen-result" : "");
  if (screen === "home") {
    main.innerHTML = `<section class="hero"><p class="kicker">${esc(T.homeKicker)}</p><h1>${T.homeTitle}</h1><p class="lead">${esc(T.homeLead)}</p></section>
      <section class="drives" id="drives"></section><footer>${esc(T.footer(state.init.version))}</footer>`;
    renderDrives();
  }
  else if (screen === "scanning") showScan();
  else showResult();
  window.scrollTo(0, 0);
}

// ── home: one card per drive ─────────────────────────────────────────────
function renderDrives() {
  const at = state.settings.alertAt || 10;
  $("drives").innerHTML = state.drives.map((d, i) => {
    const free = d.free / d.total * 100;
    const lvl = free < at ? 2 : free < at + 5 ? 1 : 0;
    const name = d.label ? `${d.label} (${drive(d.root)})` : drive(d.root);
    const [n, u] = sizeParts(d.used);
    return `<button class="drive spot reveal" style="--d:${i * 70}ms" data-root="${esc(d.root)}">
      <div class="drive-head"><span class="icon-tile">${icon(d.type === "network" ? "network" : d.type === "removable" ? "usb" : "hard-drive")}</span>
        <div class="what"><b>${esc(name)}</b><span class="sub">${esc([d.fs, d.system ? T.system : T.kinds[d.type]].filter(Boolean).join(" · "))}</span></div></div>
      <div class="big">${n}<small>${u} ${esc(T.used)}</small></div>
      <div class="meter lvl-${lvl}"><i style="width:${d.used / d.total * 100}%"></i></div>
      <div class="facts"><span><b>${size(d.free)}</b> ${esc(T.free)}</span><span><b>${size(d.total)}</b> ${esc(T.total)}</span></div>
      <span class="go">${icon("scan-search")}${esc(T.analyze)}</span></button>`;
  }).join("") || `<p class="empty">${esc(T.noDrives)}</p>`;
  document.querySelectorAll("[data-root]").forEach(b => b.onclick = () => { state.started = Date.now(); state.progress = { root: b.dataset.root, files: 0, dirs: 0, bytes: 0, used: 0, tops: [], current: "" }; show("scanning"); send({ t: "scan", root: b.dataset.root }); });
  reveal();
}

// ── reading: counters, and the top folders growing as they're read ───────
function showScan() {
  if (!state.started) state.started = Date.now();
  liveRows = new Map();
  const stat = (id, label, i) => `<div class="stat spot reveal" style="--d:${i * 60}ms"><span class="kicker">${esc(label)}</span><div class="big" id="${id}"></div></div>`;
  $("main").innerHTML = `
    <section class="scan-hero"><div class="radar"><span class="ring"></span><span class="ring r2"></span><span class="tile">${icon("hard-drive")}</span></div>
      <div class="what"><p class="kicker">${esc(T.scanKicker)}</p><h1 id="scanTitle"></h1></div>
      <button class="btn btn-ghost" id="stop">${icon("square")}${esc(T.stop)}</button></section>
    <section class="stats">${stat("sBytes", T.found, 0)}${stat("sFiles", T.filesL, 1)}${stat("sDirs", T.dirsL, 2)}${stat("sTime", T.time, 3)}</section>
    <div class="meter big-meter"><i id="sMeter"></i></div>
    <p class="current mono"><bdi id="sCurrent"></bdi></p>
    <section class="live spot reveal" style="--d:240ms"><div class="live-head"><span class="icon-tile sm">${icon("layout-grid")}</span><h2>${esc(T.liveTitle)}</h2></div><div class="live-bars" id="live"></div></section>`;
  $("stop").onclick = () => { state.started = 0; send({ t: "home" }); show("home"); };
  updateScan();
  reveal();
}

function updateScan() {
  const p = state.progress;
  if (!p || !$("sBytes")) return;
  $("scanTitle").innerHTML = T.scanTitle(esc(drive(p.root)));
  const [n, u] = sizeParts(p.bytes);
  $("sBytes").innerHTML = `${n}<small>${u}</small>`;
  $("sFiles").textContent = num(p.files);
  $("sDirs").textContent = num(p.dirs);
  const secs = Math.max(0, Math.round((Date.now() - state.started) / 1000));
  $("sTime").innerHTML = `${secs}<small>s</small>`;
  $("sMeter").style.width = (p.used > 0 ? Math.min(99, p.bytes / p.used * 100) : 0) + "%";
  $("sCurrent").textContent = p.current || "";
  const tops = [...p.tops].sort((a, b) => b.s - a.s).slice(0, 10);
  const max = tops.length ? tops[0].s : 1;
  const box = $("live");
  const seen = new Set();
  tops.forEach((t, rank) => {
    const key = t.n == null ? "\u0000" : t.n;
    seen.add(key);
    let row = liveRows.get(key);
    if (!row) {
      row = document.createElement("div");
      row.className = "live-row";
      row.innerHTML = `<span class="n">${t.n == null ? esc(T.rootFiles) : esc(t.n)}</span><span class="bar"><i></i></span><span class="v"></span>`;
      box.appendChild(row);
      liveRows.set(key, row);
    }
    row.style.order = rank;
    row.style.display = "";
    row.querySelector("i").style.width = Math.max(0.5, t.s / max * 100) + "%";
    row.querySelector(".v").textContent = size(t.s);
  });
  liveRows.forEach((row, key) => { if (!seen.has(key)) row.style.display = "none"; });
}

// ── the result: map on the left, folder / biggest / worth a look on the right ──
function showResult() {
  const s = state.summary;
  $("main").innerHTML = `
    <section class="res-head">
      <div class="what"><p class="kicker">${esc([drive(s.root), s.label].filter(Boolean).join(" · "))} · ${esc(T.readIn(num(s.seconds, 1)))}</p><h1>${T.resTitle(size(s.size), num(s.files))}</h1></div>
      <div class="actions"><button class="btn btn-ghost" id="rescan">${icon("rotate-ccw")}${esc(T.rescan)}</button><button class="btn btn-ghost" id="drivesBtn">${icon("hard-drive")}${esc(T.otherDrives)}</button></div>
    </section>
    <div id="notices"></div>
    <nav class="crumbs-row"><button class="icon-btn sm" id="up" aria-label="${esc(T.up)}">${icon("arrow-left")}</button><div class="crumbs" id="crumbs"></div>
      <button class="btn btn-ghost sm" id="explore">${icon("folder-search")}${esc(T.explorer)}</button></nav>
    <div class="result">
      <section class="map-card"><div class="map" id="map" role="img" aria-label="${esc(T.mapLabel)}"></div><div class="map-foot" id="foot"></div><div class="legend" id="legend"></div></section>
      <aside class="side"><div class="seg" id="tabs"></div><div class="panel" id="panel"></div></aside>
    </div>`;
  $("rescan").onclick = () => { state.started = Date.now(); state.progress = { root: s.root, files: 0, dirs: 0, bytes: 0, used: s.used, tops: [], current: "" }; show("scanning"); send({ t: "scan", root: s.root }); };
  $("drivesBtn").onclick = () => { send({ t: "home" }); show("home"); };
  $("up").onclick = up;
  $("explore").onclick = () => state.view && send({ t: "reveal", id: state.view.id });
  const map = $("map");
  map.addEventListener("pointermove", hover);
  map.addEventListener("pointerleave", () => { $("tip").hidden = true; renderFoot(); });
  map.addEventListener("click", e => act(e, false));
  map.addEventListener("contextmenu", e => { e.preventDefault(); act(e, true); });
  let frame = 0;
  resizer = new ResizeObserver(() => { cancelAnimationFrame(frame); frame = requestAnimationFrame(() => renderMap(false)); });
  resizer.observe(map);
  renderTabs();
  renderPanel();
  renderNotices();
}

function go(id) { $("tip").hidden = true; send({ t: "view", id }); }
function up() { const c = state.view && state.view.crumbs; if (c && c.length > 1) { state.selected = null; go(c[c.length - 2].id); } }

function renderView() {
  if (state.screen !== "result" || !state.view) return;
  const v = state.view;
  $("crumbs").innerHTML = v.crumbs.map((c, i) => `${i ? icon("chevron-right") : ""}<button data-crumb="${c.id}">${esc(c.n)}</button>`).join("");
  $("crumbs").querySelectorAll("[data-crumb]").forEach(b => b.onclick = () => { state.selected = null; go(+b.dataset.crumb); });
  $("up").disabled = v.crumbs.length < 2;
  renderMap(true);
  renderFoot();
  renderLegend();
  if (state.tab === 0) renderPanel();
}

// ── the map ──────────────────────────────────────────────────────────────
// Squarified treemap (Bruls, Huizing, van Wijk): rows of tiles as close to square as the sizes allow.
function squarify(items, x, y, w, h) {
  const out = [];
  const total = items.reduce((a, i) => a + i.s, 0);
  if (total <= 0 || w <= 0 || h <= 0) return out;
  const scale = w * h / total;
  let rest = items.map(i => ({ i, a: i.s * scale }));
  const worst = (row, sum, side) => {
    let max = 0, min = Infinity;
    for (const r of row) { if (r.a > max) max = r.a; if (r.a < min) min = r.a; }
    return Math.max(side * side * max / (sum * sum), sum * sum / (side * side * min));
  };
  while (rest.length) {
    const side = Math.min(w, h);
    let n = 1, sum = rest[0].a, best = worst(rest.slice(0, 1), sum, side);
    while (n < rest.length) {
      const next = worst(rest.slice(0, n + 1), sum + rest[n].a, side);
      if (next > best) break;
      best = next; sum += rest[n].a; n++;
    }
    const thick = sum / side;
    let off = 0;
    for (const r of rest.slice(0, n)) {
      const len = r.a / thick;
      out.push(w >= h ? { item: r.i, x, y: y + off, w: thick, h: len } : { item: r.i, x: x + off, y, w: len, h: thick });
      off += len;
    }
    if (w >= h) { x += thick; w -= thick; } else { y += thick; h -= thick; }
    rest = rest.slice(n);
  }
  return out;
}

const nameOf = it => it.k === "o" ? T.own : it.k === "m" ? T.more(num(it.x)) : it.n;

function tileHtml(r, parent, depth) {
  const it = r.item, idx = tiles.push({ it, parent }) - 1;
  const x = r.x + 1, y = r.y + 1, w = r.w - 2, h = r.h - 2;
  if (w < 2 || h < 2) return "";
  const pos = `left:${x.toFixed(1)}px;top:${y.toFixed(1)}px;width:${w.toFixed(1)}px;height:${h.toFixed(1)}px;--i:${Math.min(idx, 70)}`;
  const sel = it.k === "f" && !parent && it.n === state.selected ? " sel" : "";
  if (depth === 0 && it.ch && w >= 90 && h >= 64) {
    let inner = "";
    for (const c of squarify(it.ch.filter(i => i.s > 0), 2, 22, w - 4, h - 24)) inner += tileHtml(c, it, 1);
    return `<div class="tile group" data-t="${idx}" style="${pos}"><div class="gh">${icon(it.dn ? "lock" : "folder")}<span class="gn">${esc(it.n)}</span><span class="gs">${size(it.s)}</span></div>${inner}</div>`;
  }
  const cls = it.k === "m" ? "more" : it.k === "d" ? "dir" : it.k === "o" ? "own" : "file";
  const ic = it.k === "d" ? icon(it.dn ? "lock" : "folder") : it.k === "o" ? icon("files") : "";
  const label = w >= 54 && h >= 30 ? `<span class="tn">${ic}${esc(nameOf(it))}</span>${h >= 44 ? `<span class="ts">${size(it.s)}</span>` : ""}` : "";
  return `<div class="tile leaf ${cls}${sel}" data-t="${idx}" style="${pos};--c:var(--c${it.c})">${label}</div>`;
}

function renderMap(anim) {
  const map = $("map"), v = state.view;
  if (!map || !v) return;
  tiles = [];
  let html = "";
  for (const r of squarify(v.items.filter(i => i.s > 0), 0, 0, map.clientWidth, map.clientHeight)) html += tileHtml(r, null, 0);
  map.innerHTML = html;
  if (anim) { map.classList.remove("enter"); void map.offsetWidth; map.classList.add("enter"); }
}

function tileAt(e) {
  const el = e.target.closest && e.target.closest(".tile");
  return el ? tiles[+el.dataset.t] : null;
}

function hover(e) {
  const t = tileAt(e), tip = $("tip");
  if (!t) { tip.hidden = true; return; }
  const it = t.it, v = state.view;
  const kind = it.k === "d" ? T.folder : it.k === "f" ? T.file : "";
  const hint = it.k === "m" ? T.hMore : it.k === "f" ? T.hFile : T.hEnter;
  const facts = [size(it.s), pct(it.s, v.size)];
  if (it.k !== "f" && it.f) facts.push(T.files(num(it.f)));
  tip.innerHTML = `<b>${esc(nameOf(it))}</b>${t.parent ? `<span class="sub">${esc(t.parent.n)}</span>` : ""}
    <div class="tk" style="--c:var(--c${it.c})"><i></i>${esc([kind, T.cats[it.c]].filter(Boolean).join(" · "))}</div>
    <div class="tv">${esc(facts.join(" · "))}</div>${it.dn ? `<div class="th">${esc(T.denied)}</div>` : ""}<div class="th">${esc(hint)}</div>`;
  tip.hidden = false;
  const bw = tip.offsetWidth, bh = tip.offsetHeight;
  tip.style.left = Math.min(e.clientX + 16, innerWidth - bw - 8) + "px";
  tip.style.top = (e.clientY + 18 + bh > innerHeight ? e.clientY - bh - 12 : e.clientY + 18) + "px";
  renderFoot(t);
}

// click: into a folder, or select a file. Right click: Explorer.
function act(e, explorer) {
  const t = tileAt(e);
  if (!t) return;
  const it = t.it, v = state.view;
  const dirId = it.k === "d" ? it.id : t.parent ? t.parent.id : v.id;
  if (explorer) { send(it.k === "f" ? { t: "reveal", id: v.id, file: it.n } : { t: "reveal", id: dirId }); return; }
  if (it.k === "f") { state.selected = it.n; renderMap(false); renderFoot(); return; }
  if (it.k === "m" && !t.parent) return;
  state.selected = null;
  go(dirId);
}

function renderFoot(t) {
  const foot = $("foot");
  if (!foot) return;
  const v = state.view;
  const sel = !t && state.selected && v && v.items.find(i => i.k === "f" && i.n === state.selected);
  const it = t ? t.it : sel;
  if (!it) { foot.innerHTML = `${icon("info")}<span>${esc(T.hint)}</span>`; return; }
  foot.innerHTML = `<span class="dot" style="--c:var(--c${it.c})"></span><div class="what"><b>${esc(nameOf(it))}</b></div><span class="v">${size(it.s)} · ${pct(it.s, v.size)}</span>` +
    (sel ? `<button class="btn btn-ghost sm" id="revealSel">${icon("folder-search")}${esc(T.reveal)}</button>` : "");
  if (sel) $("revealSel").onclick = () => send({ t: "reveal", id: v.id, file: sel.n });
}

// the legend: the kinds of file on screen right now
function renderLegend() {
  const seen = new Set(), v = state.view;
  const add = list => list.forEach(i => { if (i.s > 0 && i.k !== "m") seen.add(i.c); if (i.ch) add(i.ch); });
  add(v.items);
  $("legend").innerHTML = [...seen].sort((a, b) => a - b).map(c => `<span style="--c:var(--c${c})"><i></i>${esc(T.cats[c])}</span>`).join("");
}

// ── side panel ───────────────────────────────────────────────────────────
function renderTabs() {
  const n = state.summary.hogs.length;
  $("tabs").innerHTML = T.tabs.map((t, i) => `<button data-tab="${i}" aria-pressed="${i === state.tab}">${icon(["folder-open", "files", "sparkles"][i])}${esc(t)}${i === 2 && n ? `<span class="count">${n}</span>` : ""}</button>`).join("");
  $("tabs").querySelectorAll("[data-tab]").forEach(b => b.onclick = () => { state.tab = +b.dataset.tab; renderTabs(); renderPanel(); });
}

function renderPanel() {
  const panel = $("panel");
  if (!panel) return;
  panel.style.animation = "none"; void panel.offsetWidth; panel.style.animation = "";
  if (state.tab === 0) panelFolder(panel);
  else if (state.tab === 1) panelBiggest(panel);
  else panelHogs(panel);
  panel.scrollTop = 0;
}

function panelFolder(panel) {
  const v = state.view, s = state.summary;
  if (!v) { panel.innerHTML = ""; return; }
  // small folders don't carry the breakdown: the tiles' main kinds are close enough there
  let cats = v.cats;
  if (!cats) { cats = new Array(9).fill(0); v.items.forEach(i => cats[i.c] += i.s); }
  const total = cats.reduce((a, b) => a + b, 0);
  const kinds = cats.map((b, c) => ({ c, b })).filter(x => x.b > 0).sort((a, b) => b.b - a.b);
  const max = Math.max(1, ...v.items.map(i => i.s));
  const [n, u] = sizeParts(v.size);
  const name = v.crumbs[v.crumbs.length - 1].n;
  panel.innerHTML = `
    <div class="fold-head"><span class="icon-tile sm">${icon(v.denied ? "lock" : "folder-open")}</span><div class="what"><b>${esc(name)}</b><span class="sub">${esc(v.path)}</span></div></div>
    <div class="big">${n}<small>${u}</small></div>
    <div class="facts"><span>${esc(T.ofDisk(pct(v.size, s.used)))}</span><span>${esc(T.files(num(v.files)))}</span></div>
    <div class="stack">${kinds.map(k => `<i style="flex:${k.b};--c:var(--c${k.c})"></i>`).join("")}</div>
    <ul class="types">${kinds.map(k => `<li><span class="dot" style="--c:var(--c${k.c})"></span>${icon(CAT_ICONS[k.c])}<span>${esc(T.cats[k.c])}</span><span class="v">${size(k.b)}</span><span class="p">${pct(k.b, total)}</span></li>`).join("")}</ul>
    <p class="list-title">${icon("list")}${esc(T.inside)}</p>
    ${v.items.map((it, i) => `<div class="row${it.k === "m" ? " static" : ""}" data-i="${i}">${icon(it.k === "d" ? (it.dn ? "lock" : "folder") : it.k === "m" ? "ellipsis" : CAT_ICONS[it.c])}<span class="n">${esc(nameOf(it))}</span><span class="v">${size(it.s)}</span><span class="bar"><i style="width:${it.s / max * 100}%;--c:var(--c${it.c})"></i></span></div>`).join("")}`;
  panel.querySelectorAll("[data-i]").forEach(r => {
    const it = v.items[+r.dataset.i];
    r.onclick = () => { if (it.k === "d") { state.selected = null; go(it.id); } else if (it.k === "f") { state.selected = it.n; renderMap(false); renderFoot(); } };
    r.oncontextmenu = e => { e.preventDefault(); if (it.k !== "m") send(it.k === "f" ? { t: "reveal", id: v.id, file: it.n } : { t: "reveal", id: it.id }); };
  });
}

function panelBiggest(panel) {
  const list = state.summary.biggest;
  panel.innerHTML = `<p class="note">${icon("info")}<span>${esc(T.bigNote)}</span></p>` + list.map((b, i) =>
    `<div class="row big-row" data-b="${i}"><span class="dot" style="--c:var(--c${b.c})"></span><div class="what"><b>${esc(b.n)}</b><span class="sub">${esc(b.p)}</span></div><span class="v">${size(b.s)}</span>
      <button class="icon-btn sm" data-reveal="${i}" aria-label="${esc(T.reveal)}" title="${esc(T.reveal)}">${icon("folder-search")}</button></div>`).join("");
  panel.querySelectorAll("[data-b]").forEach(r => r.onclick = e => {
    const b = list[+r.dataset.b];
    if (e.target.closest("[data-reveal]")) { send({ t: "reveal", id: b.id, file: b.n }); return; }
    state.selected = b.n;
    go(b.id);
  });
}

function panelHogs(panel) {
  const list = state.summary.hogs;
  if (!list.length) { panel.innerHTML = `<p class="note">${icon("sparkles")}<span>${esc(T.noHogs)}</span></p>`; return; }
  const words = h => h.key === "node" ? T.hogs.node(num(h.x)) : CACHES[h.key] ? T.hogs.cache(CACHES[h.key]) : T.hogs[h.key];
  const ic = { temp: "timer", downloads: "download", recycle: "trash-2", winold: "house", dumps: "file", node: "package", hiberfil: "power" };
  panel.innerHTML = `<p class="note">${icon("info")}<span>${esc(T.hogsNote)}</span></p>` + list.map((h, i) => {
    const [title, text] = words(h);
    return `<div class="hog"><div class="hog-head"><span class="icon-tile sm">${icon(ic[h.key] || "package")}</span><div class="what"><b>${esc(title)}</b></div><span class="v">${size(h.s)}</span></div>
      <p>${esc(text)}</p><span class="sub mono">${esc(h.p)}</span>
      <div class="actions"><button class="btn btn-ghost" data-go="${i}">${icon("layout-grid")}${esc(T.go)}</button><button class="btn btn-ghost" data-rv="${i}">${icon("folder-search")}${esc(T.reveal)}</button></div></div>`;
  }).join("");
  panel.querySelectorAll("[data-go]").forEach(b => b.onclick = () => { const h = list[+b.dataset.go]; state.selected = h.file || null; go(h.id); });
  panel.querySelectorAll("[data-rv]").forEach(b => b.onclick = () => { const h = list[+b.dataset.rv]; send(h.file ? { t: "reveal", id: h.id, file: h.file } : { t: "reveal", id: h.id }); });
}

// what Hoard couldn't see, or saw twice
function renderNotices() {
  const s = state.summary, admin = state.init.admin;
  const gap = s.used - s.size, over = s.size - s.used;
  const notice = (cls, ic, title, text, button) => `<div class="notice ${cls}"><span class="icon-tile sm">${icon(ic)}</span><div class="text"><b>${esc(title)}</b><p>${esc(text)}</p></div>${button || ""}</div>`;
  const adminBtn = admin ? "" : `<button class="btn btn-primary sm" id="adminBtn">${icon("shield-check")}${esc(T.admin)}</button>`;
  let html = "";
  if (gap > Math.max(1 << 30, s.used * 0.02))
    html = notice("", "shield-exclamation-point", T.gapTitle(size(gap)), admin ? T.gapAdmin(size(s.used), size(s.size)) : T.gapText(size(s.used), size(s.size), s.denied ? T.folders(num(s.denied)) : ""), adminBtn);
  else if (s.denied > 0 && !admin) html = notice("", "lock", T.deniedTitle(num(s.denied)), T.deniedText, adminBtn);
  else if (over > s.used * 0.03) html = notice("info", "info", T.overTitle(size(over)), T.overText);
  $("notices").innerHTML = html;
  if ($("adminBtn")) $("adminBtn").onclick = () => send({ t: "admin" });
}

// ── settings ─────────────────────────────────────────────────────────────
function renderDrawer() {
  if (!state.init) return;
  const s = state.settings, i = state.init;
  $("drawer").innerHTML = `
    <div class="drawer-head"><h2>${esc(T.settings)}</h2><button class="icon-btn" data-close aria-label="close">${icon("x")}</button></div>
    <div class="setting"><label class="setting-row"><span class="label">${esc(T.startup)}</span><input type="checkbox" class="switch" id="sStartup"${s.startup ? " checked" : ""}${i.portable ? " disabled" : ""}></label><p>${esc(i.portable ? T.startupPortable : T.startupText)}</p></div>
    <div class="setting"><label class="setting-row"><span class="label">${esc(T.alerts)}</span><input type="checkbox" class="switch" id="sAlerts"${s.alertsOn ? " checked" : ""}></label>
      <div class="setting-row" style="margin-top:.8rem"><span class="label" style="font-weight:400;color:var(--muted)">${esc(T.limit)}</span><span class="num"><input type="number" id="sLimit" min="${i.alertMin}" max="${i.alertMax}" value="${s.alertAt}"> % ${esc(T.free)}</span></div>
      <p>${esc(T.alertsText)}</p></div>
    <div class="setting"><p class="about">${esc(T.about(i.version))}</p>
      <div class="links"><button class="btn btn-ghost" data-open="https://github.com/davide-mariotti/Hoard">${icon("code-xml")}${esc(T.links[0])}</button><button class="btn btn-ghost" data-open="https://ko-fi.com/Z0X32ATNHL">${icon("coffee")}${esc(T.links[1])}</button></div></div>`;
  $("sStartup").onchange = e => send({ t: "set", startup: e.target.checked });
  $("sAlerts").onchange = e => send({ t: "set", alertsOn: e.target.checked });
  $("sLimit").onchange = e => send({ t: "set", alertAt: Math.max(i.alertMin, Math.min(i.alertMax, +e.target.value || 10)) });
  $("drawer").querySelector("[data-close]").onclick = () => document.body.classList.remove("open");
  $("drawer").querySelectorAll("[data-open]").forEach(b => b.onclick = () => send({ t: "open", url: b.dataset.open }));
}

document.addEventListener("keydown", e => {
  if (e.key === "Escape") document.body.classList.remove("open");
  if (e.key === "Backspace" && state.screen === "result" && !/INPUT/.test(document.activeElement.tagName)) { e.preventDefault(); up(); }
});

document.addEventListener("pointermove", e => {
  const el = e.target.closest && e.target.closest(".spot");
  if (!el) return;
  const b = el.getBoundingClientRect();
  el.style.setProperty("--x", e.clientX - b.left + "px");
  el.style.setProperty("--y", e.clientY - b.top + "px");
});
