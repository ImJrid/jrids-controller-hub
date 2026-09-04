const THEME_KEY = "jrids-theme-color";
const BG_KEY = "jrids-bg-color";
const APP_VERSION = "1.0.11";
const UPDATE_API = "https://api.github.com/repos/ImJrid/jrids-controller-hub/releases/latest";
const UPDATE_PAGE = "https://github.com/ImJrid/jrids-controller-hub/releases/latest";
const THEME_PRESETS = ["#e10600", "#ff9c00", "#ff7a18", "#3aa0ff", "#7c5cff", "#2ecc71"];
const BG_PRESETS = ["#0b0c0e", "#121826", "#1a1220", "#101610", "#1a1410", "#e8eaed"];

const TOOLS = {
  hyperstrike: {
    title: "Hyperstrike",
    kicker: "Setup",
    blurb:
      "Open the Hyperstrike connect and setup portal. This launches the official setup page in your browser.",
    url: "https://hs2.evua.cc/connect",
    logo: "assets/hyperstrike-logo.png",
    logoClass: "logo-lift",
  },
  firebird: {
    title: "Firebird",
    kicker: "Setup",
    blurb: "Open the Firebird setup tool. This launches the Firebird portal in your browser.",
    url: "https://bzl-web.com/tool/firebird/",
    logo: "assets/firebird-logo.png",
    logoClass: "",
  },
  suiovoi: {
    title: "Suiovoi",
    kicker: "Setup",
    blurb: "Open the Suiovoi setup portal in your browser.",
    url: "https://sy2.suiovoi.cc/",
    logo: "assets/suiovoi-logo.png",
    logoClass: "",
  },
  marius: {
    title: "Marius",
    kicker: "Setup",
    blurb: "Open Marius setup or firmware update in your browser.",
    url: "https://setup.mariusheier.com/",
    updateUrl: "https://update.mariusheier.com/",
    logo: "",
    logoClass: "",
  },
};

function hexToRgb(hex) {
  const value = hex.replace("#", "");
  const full = value.length === 3 ? value.split("").map((c) => c + c).join("") : value;
  const n = Number.parseInt(full, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function rgbToHex([r, g, b]) {
  return `#${[r, g, b].map((c) => c.toString(16).padStart(2, "0")).join("")}`;
}

function clampByte(n) {
  return Math.max(0, Math.min(255, Math.round(n)));
}

function shade(rgb, delta) {
  return rgb.map((c) => clampByte(c + delta));
}

function luminance(rgb) {
  const lin = rgb.map((v) => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2];
}

function applyTheme(hex) {
  const color = hex.startsWith("#") ? hex : `#${hex}`;
  const [r, g, b] = hexToRgb(color);
  const root = document.documentElement;
  root.style.setProperty("--accent", color);
  root.style.setProperty("--accent-rgb", `${r}, ${g}, ${b}`);
  root.style.setProperty("--glow", `rgba(${r}, ${g}, ${b}, 0.28)`);
  localStorage.setItem(THEME_KEY, color);
  document.querySelectorAll("#theme-color, #theme-color-settings").forEach((input) => {
    input.value = color;
  });
  document.querySelectorAll("[data-accent]").forEach((swatch) => {
    swatch.classList.toggle("active", swatch.dataset.accent?.toLowerCase() === color.toLowerCase());
  });
}

function applyBackground(hex) {
  const color = hex.startsWith("#") ? hex : `#${hex}`;
  const rgb = hexToRgb(color);
  const light = luminance(rgb) > 0.45;
  const chrome = rgbToHex(shade(rgb, light ? -18 : -6));
  const panel = rgbToHex(shade(rgb, light ? -10 : 12));
  const line = rgbToHex(shade(rgb, light ? -40 : 28));
  const root = document.documentElement;
  root.style.setProperty("--bg", color);
  root.style.setProperty("--chrome", chrome);
  root.style.setProperty("--panel", panel);
  root.style.setProperty("--line", line);
  root.style.setProperty("--text", light ? "#16181c" : "#e8eaed");
  root.style.setProperty("--muted", light ? "#5b616a" : "#8b919a");
  localStorage.setItem(BG_KEY, color);
  document.querySelectorAll("#bg-color, #bg-color-settings").forEach((input) => {
    input.value = color;
  });
  document.querySelectorAll("[data-bg]").forEach((swatch) => {
    swatch.classList.toggle("active", swatch.dataset.bg?.toLowerCase() === color.toLowerCase());
  });
}

function desktopMessage(type) {
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage({ type });
  }
}

document.querySelector(".titlebar")?.addEventListener("mousedown", (event) => {
  if (event.button !== 0 || event.target.closest(".win-btn, .theme-dock, .titlebar-tools")) return;
  desktopMessage("drag");
});

document.querySelector(".titlebar")?.addEventListener("dblclick", (event) => {
  if (event.target.closest(".win-btn, .theme-dock, .titlebar-tools")) return;
  desktopMessage("max");
});

document.querySelector(".titlebar-actions")?.addEventListener("click", (event) => {
  const button = event.target.closest("[data-win]");
  if (button) desktopMessage(button.dataset.win);
});

const views = document.querySelectorAll(".view");

function renderLaunchView(id) {
  const tool = TOOLS[id];
  const el = document.getElementById(`view-${id}`);
  const art = tool.logo
    ? `<img class="${tool.logoClass}" src="${tool.logo}" alt="${tool.title}" />`
    : `<span>${tool.title.slice(0, 2).toUpperCase()}</span>`;
  el.innerHTML = `
    <div class="page-head">
      <div>
        <p class="kicker">${tool.kicker}</p>
        <h1>${tool.title}</h1>
      </div>
    </div>
    <div class="launch-card">
      <div class="launch-copy">
        <p>${tool.blurb}</p>
        <div class="launch-actions">
          <button class="launch-btn" type="button" data-url="${tool.url}">Launch setup</button>
          ${tool.updateUrl ? `<button class="launch-btn alt" type="button" data-url="${tool.updateUrl}">Launch update</button>` : ""}
        </div>
      </div>
      <div class="launch-art">${art}</div>
    </div>
  `;
}

Object.keys(TOOLS).forEach(renderLaunchView);

const settingsSwatches = document.getElementById("settings-swatches");
if (settingsSwatches) {
  settingsSwatches.innerHTML = THEME_PRESETS.map(
    (color) => `<button class="swatch" type="button" data-accent="${color}" aria-label="${color}"></button>`
  ).join("");
}
const settingsBg = document.getElementById("settings-bg-swatches");
if (settingsBg) {
  settingsBg.innerHTML = BG_PRESETS.map(
    (color) => `<button class="swatch bg" type="button" data-bg="${color}" aria-label="${color}"></button>`
  ).join("");
}

function showView(id) {
  views.forEach((view) => view.classList.toggle("active", view.id === `view-${id}`));
  document.querySelectorAll(".nav-btn").forEach((btn) => {
    btn.classList.toggle("active", btn.dataset.view === id);
  });
}

document.body.addEventListener("click", (event) => {
  const accent = event.target.closest("[data-accent]");
  if (accent) {
    applyTheme(accent.dataset.accent);
    return;
  }
  const bg = event.target.closest("[data-bg]");
  if (bg) {
    applyBackground(bg.dataset.bg);
    return;
  }
  const nav = event.target.closest("[data-view]");
  if (nav?.dataset.view) {
    showView(nav.dataset.view);
    return;
  }
  const launch = event.target.closest(".launch-btn");
  if (launch?.dataset.url) {
    window.open(launch.dataset.url, "_blank", "noopener,noreferrer");
  }
});

document.querySelectorAll("#theme-color, #theme-color-settings").forEach((input) => {
  input.addEventListener("input", () => applyTheme(input.value));
});
document.querySelectorAll("#bg-color, #bg-color-settings").forEach((input) => {
  input.addEventListener("input", () => applyBackground(input.value));
});

applyTheme(localStorage.getItem(THEME_KEY) || "#e10600");
applyBackground(localStorage.getItem(BG_KEY) || "#0b0c0e");

function versionParts(value) {
  return String(value || "")
    .replace(/^v/i, "")
    .split(/[^\d]+/)
    .filter(Boolean)
    .map((part) => Number(part) || 0);
}

function versionNewer(latest, current) {
  const left = versionParts(latest);
  const right = versionParts(current);
  const count = Math.max(left.length, right.length);
  for (let i = 0; i < count; i += 1) {
    if ((left[i] || 0) > (right[i] || 0)) return true;
    if ((left[i] || 0) < (right[i] || 0)) return false;
  }
  return false;
}

function setUpdateUi({ latest, url, error, checking, installing } = {}) {
  const status = document.getElementById("update-status");
  const link = document.getElementById("update-open");
  const settingsNav = document.querySelector(".nav-settings");
  if (!status || !link) return;
  status.classList.remove("ok", "warn");
  if (checking) {
    status.textContent = "Checking for updates...";
    return;
  }
  if (installing) {
    status.textContent = `Updating to ${String(latest || "").replace(/^v/i, "")}. The app will restart.`;
    status.classList.add("warn");
    link.hidden = true;
    settingsNav?.classList.add("has-update");
    return;
  }
  if (error) {
    status.textContent = "Couldn't check right now. You can still open GitHub.";
    link.hidden = false;
    link.href = UPDATE_PAGE;
    return;
  }
  if (versionNewer(latest, APP_VERSION)) {
    status.textContent = `Version ${String(latest).replace(/^v/i, "")} is available.`;
    status.classList.add("warn");
    link.hidden = false;
    link.href = url || UPDATE_PAGE;
    settingsNav?.classList.add("has-update");
    return;
  }
  status.textContent = "You're on the latest version.";
  status.classList.add("ok");
  link.hidden = true;
  settingsNav?.classList.remove("has-update");
}

async function checkForUpdates() {
  setUpdateUi({ checking: true });
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage({ type: "check-update" });
    return;
  }
  try {
    const response = await fetch(UPDATE_API, { headers: { Accept: "application/vnd.github+json" } });
    if (!response.ok) throw new Error("update check failed");
    const data = await response.json();
    setUpdateUi({ latest: data.tag_name, url: data.html_url });
  } catch {
    setUpdateUi({ error: true });
  }
}

window.chrome?.webview?.addEventListener("message", (event) => {
  const data = typeof event.data === "string" ? JSON.parse(event.data) : event.data;
  if (data?.type === "update-result") {
    if (data.error) setUpdateUi({ error: true });
    else setUpdateUi({ latest: data.tag, url: data.html, installing: data.installing });
    return;
  }
  if (data?.type === "usb-poll-progress" || data?.type === "usb-poll-result") {
    applyUsbPoll(data);
  }
});

document.getElementById("update-check")?.addEventListener("click", () => {
  setUpdateUi({ checking: true });
  if (window.chrome?.webview) {
    window.chrome.webview.postMessage({ type: "install-update" });
    return;
  }
  checkForUpdates();
});
document.getElementById("uninstall-app")?.addEventListener("click", () => {
  window.chrome?.webview?.postMessage({ type: "uninstall" });
});
document.getElementById("usb-poll-measure")?.addEventListener("click", () => {
  const btn = document.getElementById("usb-poll-measure");
  if (btn) btn.disabled = true;
  window.chrome?.webview?.postMessage({ type: "usb-poll" });
});
const versionLabel = document.getElementById("app-version");
if (versionLabel) versionLabel.textContent = `Version ${APP_VERSION}`;

function updateClock() {
  document.getElementById("clock").textContent = new Date().toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });
}
updateClock();
setInterval(updateClock, 1000);

const statusDot = document.getElementById("pad-status-dot");
const statusText = document.getElementById("pad-status-text");
const htSlots = document.getElementById("ht-slots");
const htBody = document.getElementById("ht-body");

let chosenIndex = 0;
let uiKey = "";
let slotKey = "";
let testCircularity = false;
const traces = { left: {}, right: {} };

const ANGLE_STEP = Math.PI / 16;
const MOCK_PAD = { id: "", axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, () => ({ value: 0, pressed: false })) };

function detectFamily(pad) {
  const id = (pad?.id || "").toLowerCase();
  if (/playstation|dualsense|dualshock|054c/.test(id)) return "ps";
  if (/xbox|xinput|045e|microsoft/.test(id)) return "xbox";
  if (id.includes("wireless controller")) return "ps";
  return "xbox";
}

function btnVal(pad, i) {
  return pad?.buttons?.[i]?.value || 0;
}

function testerDark() {
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

function ink(v) {
  const a = Math.max(0, Math.min(1, v));
  return testerDark() ? `rgba(242, 244, 247, ${a})` : `rgba(0, 0, 0, ${a})`;
}

function controllerSvg() {
  const outline =
    "M220.5 92.0001C200.5 92.0001 154 92.0001 128 92.0001C95.5 92.0001 66.5 109.5 55 137.5C43.5 165.5 4 271.1 4 317.5C4 363.9 17.5 378.5 49.5 378.5C81.5 378.5 105 294.5 150 294.5C195 294.5 220.5 294.5 220.5 294.5C220.5 294.5 245.5 294.5 290.5 294.5C335.5 294.5 359 378.5 391 378.5C423 378.5 436.5 363.9 436.5 317.5C436.5 271.1 397 165.5 385.5 137.5C374 109.5 345 92.0001 312.5 92.0001C286.5 92.0001 240 92.0001 220.5 92.0001Z";
  const petal = {
    up: "M177.669 222.335C180.793 219.21 180.816 213.997 176.868 212.014C176.327 211.743 175.776 211.491 175.215 211.258C172.182 210.002 168.931 209.355 165.648 209.355C162.365 209.355 159.114 210.002 156.081 211.258C155.521 211.491 154.969 211.743 154.429 212.014C150.48 213.997 150.503 219.21 153.627 222.335L159.991 228.698C163.116 231.823 168.181 231.823 171.305 228.698L177.669 222.335Z",
    right: "M181.447 249.669C184.571 252.793 189.785 252.816 191.768 248.868C192.039 248.327 192.291 247.776 192.523 247.215C193.78 244.182 194.426 240.931 194.426 237.648C194.426 234.365 193.78 231.114 192.523 228.081C192.291 227.521 192.039 226.969 191.768 226.429C189.785 222.48 184.571 222.503 181.447 225.627L175.083 231.991C171.959 235.116 171.959 240.181 175.083 243.305L181.447 249.669Z",
    down: "M154.113 253.447C150.989 256.571 150.966 261.785 154.914 263.767C155.455 264.039 156.006 264.291 156.566 264.523C159.6 265.78 162.85 266.426 166.134 266.426C169.417 266.426 172.667 265.78 175.701 264.523C176.261 264.291 176.812 264.039 177.353 263.767C181.301 261.785 181.279 256.571 178.154 253.447L171.79 247.083C168.666 243.959 163.601 243.959 160.477 247.083L154.113 253.447Z",
    left: "M150.335 226.113C147.21 222.989 141.997 222.966 140.014 226.914C139.743 227.455 139.491 228.006 139.258 228.566C138.002 231.6 137.355 234.85 137.355 238.134C137.355 241.417 138.002 244.667 139.258 247.701C139.491 248.261 139.743 248.812 140.014 249.353C141.997 253.301 147.21 253.279 150.335 250.154L156.698 243.79C159.823 240.666 159.823 235.601 156.698 232.477L150.335 226.113Z",
    y: "M340.669 144.335C343.793 141.21 343.816 135.997 339.868 134.014C339.327 133.743 338.776 133.491 338.215 133.258C335.182 132.002 331.931 131.355 328.648 131.355C325.365 131.355 322.114 132.002 319.081 133.258C318.521 133.491 317.969 133.743 317.429 134.014C313.48 135.997 313.503 141.21 316.627 144.335L322.991 150.698C326.116 153.823 331.181 153.823 334.305 150.698L340.669 144.335Z",
    b: "M344.447 171.669C347.571 174.793 352.785 174.816 354.768 170.868C355.039 170.327 355.291 169.776 355.523 169.215C356.78 166.182 357.426 162.931 357.426 159.648C357.426 156.365 356.78 153.114 355.523 150.081C355.291 149.521 355.039 148.969 354.768 148.429C352.785 144.48 347.571 144.503 344.447 147.627L338.083 153.991C334.959 157.116 334.959 162.181 338.083 165.305L344.447 171.669Z",
    a: "M317.113 175.447C313.989 178.571 313.966 183.785 317.914 185.767C318.455 186.039 319.006 186.291 319.566 186.523C322.6 187.78 325.85 188.426 329.134 188.426C332.417 188.426 335.667 187.78 338.701 186.523C339.261 186.291 339.812 186.039 340.353 185.767C344.301 183.785 344.279 178.571 341.154 175.447L334.79 169.083C331.666 165.959 326.601 165.959 323.477 169.083L317.113 175.447Z",
    x: "M313.335 148.113C310.21 144.989 304.997 144.966 303.014 148.914C302.743 149.455 302.491 150.006 302.258 150.566C301.002 153.6 300.355 156.851 300.355 160.134C300.355 163.417 301.002 166.668 302.258 169.701C302.491 170.261 302.743 170.812 303.014 171.353C304.997 175.301 310.21 175.279 313.335 172.154L319.698 165.79C322.823 162.666 322.823 157.601 319.698 154.477L313.335 148.113Z",
  };
  return `<svg class="controller-svg" viewBox="0 0 441 403" fill="none" xmlns="http://www.w3.org/2000/svg">
    <path class="pad-glow" d="${outline}"/>
    <path class="pad-body" d="${outline}"/>
    <circle class="pad-ring" cx="166" cy="238" r="37.5"/>
    <circle class="pad-ring" cx="329" cy="160" r="37.5"/>
    <g id="lstick-well">
      <circle class="pad-ring" cx="113" cy="160" r="37.5"/>
      <circle id="lstick" class="pad-ctrl" cx="113" cy="160" r="28" fill="none"/>
      <circle id="lstick-dot" cx="113" cy="160" r="7"/>
    </g>
    <g id="rstick-well">
      <circle class="pad-ring" cx="278" cy="238" r="37.5"/>
      <circle id="rstick" class="pad-ctrl" cx="278" cy="238" r="28" fill="none"/>
      <circle id="rstick-dot" cx="278" cy="238" r="7"/>
    </g>
    <g id="dpad">
      <path class="pad-ctrl" data-btn="12" d="${petal.up}"/>
      <path class="pad-ctrl" data-btn="15" d="${petal.right}"/>
      <path class="pad-ctrl" data-btn="13" d="${petal.down}"/>
      <path class="pad-ctrl" data-btn="14" d="${petal.left}"/>
    </g>
    <path class="pad-ctrl" data-btn="3" d="${petal.y}"/>
    <path class="pad-ctrl" data-btn="1" d="${petal.b}"/>
    <path class="pad-ctrl" data-btn="0" d="${petal.a}"/>
    <path class="pad-ctrl" data-btn="2" d="${petal.x}"/>
    <circle class="pad-ctrl" data-btn="8" cx="185" cy="162" r="10"/>
    <circle class="pad-ctrl" data-btn="9" cx="259" cy="162" r="10"/>
    <circle class="pad-ctrl" data-btn="16" cx="222" cy="162" r="8"/>
    <rect class="pad-ctrl" data-btn="4" x="111.5" y="61.5" width="41" height="13" rx="6.5"/>
    <rect class="pad-ctrl" data-btn="5" x="289.5" y="61.5" width="41" height="13" rx="6.5"/>
    <path class="pad-ctrl" data-btn="6" d="M152.5 37C152.5 41.1421 149.142 44.5 145 44.5H132C127.858 44.5 124.5 41.1421 124.5 37V16.5C124.5 8.76801 130.768 2.5 138.5 2.5C146.232 2.5 152.5 8.76801 152.5 16.5V37Z"/>
    <path class="pad-ctrl" data-btn="7" d="M317.5 37C317.5 41.1421 314.142 44.5 310 44.5H297C292.858 44.5 289.5 41.1421 289.5 37V16.5C289.5 8.76801 295.768 2.5 303.5 2.5C311.232 2.5 317.5 8.76801 317.5 16.5V37Z"/>
    <line class="pad-seam" x1="30" y1="210" x2="130" y2="300"/>
    <line class="pad-seam" x1="411" y1="210" x2="311" y2="300"/>
  </svg>`;
}

function paintController(pad) {
  const svg = document.querySelector(".controller-svg");
  if (!svg) return;
  const ps = detectFamily(pad) === "ps";
  svg.querySelector("#dpad")?.setAttribute("transform", ps ? "translate(-53,-78)" : "");
  svg.querySelector("#lstick-well")?.setAttribute("transform", ps ? "translate(53,78)" : "");
  const lx = pad.axes[0] || 0;
  const ly = pad.axes[1] || 0;
  const rx = pad.axes[2] || 0;
  const ry = pad.axes[3] || 0;
  const ld = svg.querySelector("#lstick-dot");
  const rd = svg.querySelector("#rstick-dot");
  if (ld) {
    ld.setAttribute("cx", 113 + lx * 16);
    ld.setAttribute("cy", 160 + ly * 16);
  }
  if (rd) {
    rd.setAttribute("cx", 278 + rx * 16);
    rd.setAttribute("cy", 238 + ry * 16);
  }
  const ls = svg.querySelector("#lstick");
  const rs = svg.querySelector("#rstick");
  if (ls) ls.setAttribute("fill", btnVal(pad, 10) > 0.12 ? ink(btnVal(pad, 10)) : "none");
  if (rs) rs.setAttribute("fill", btnVal(pad, 11) > 0.12 ? ink(btnVal(pad, 11)) : "none");
  svg.querySelectorAll("[data-btn]").forEach((node) => {
    node.setAttribute("fill", ink(btnVal(pad, Number(node.dataset.btn))));
  });
}

function emptyArt() {
  return `<div class="ht-empty">${controllerSvg()}<p>Connect your gamepad and press buttons to begin...</p></div>`;
}

function hypot(x, y) {
  return Math.sqrt(x * x + y * y);
}

function shortName(id) {
  if (!id) return "None detected";
  return id.length > 34 ? `${id.slice(0, 32)}...` : id;
}

function snapAngle(x, y) {
  let a = Math.round(Math.atan2(y, x) / ANGLE_STEP) * ANGLE_STEP;
  if (a === -Math.PI) a = Math.PI;
  return a;
}

function avgError(map) {
  const vals = Object.values(map);
  if (vals.length < 8) return null;
  const rms = Math.sqrt(vals.reduce((sum, r) => sum + (1 - r) ** 2, 0) / vals.length);
  return rms * 100;
}

function renderSlots(pads) {
  const next = [0, 1, 2, 3].map((i) => (pads[i] ? `${i}:${pads[i].id}` : `${i}:none`)).join("|") + `|${chosenIndex}`;
  if (next === slotKey) return;
  slotKey = next;
  htSlots.innerHTML = [0, 1, 2, 3]
    .map((i) => {
      const pad = pads[i];
      const live = Boolean(pad);
      const active = i === chosenIndex;
      const label = live ? `${i + 1}: ${shortName(pad.id)}` : `${i + 1}: None detected`;
      return `<button class="ht-slot${live ? " live" : ""}${active ? " active" : ""}" data-slot="${i}" type="button">${label}</button>`;
    })
    .join("");
}

function padTitle(id) {
  const match = /([^(]+)(?:\s*\(([^)]+)\))?/.exec(id || "");
  return { big: (match ? match[1] : id).trim(), small: match?.[2]?.trim() || "" };
}

function applyUsbPoll(data) {
  const graph = document.getElementById("usb-poll-graph");
  const btn = document.getElementById("usb-poll-measure");
  if (!graph) return;
  if (data.type === "usb-poll-progress") {
    graph.textContent = "Capturing USB interrupts...\n\nAllow admin if asked. Leave the pad plugged in — you do not need to move the sticks.";
    if (btn) btn.disabled = true;
    return;
  }
  if (btn) btn.disabled = false;
  if (data.error) {
    graph.textContent = data.error;
    return;
  }
  const buckets = data.buckets || [];
  const maxPct = Math.max(...buckets.map((bucket) => Number(bucket.pct) || 0), 0.1);
  const lines = buckets.map((bucket) => {
    const width = Number(bucket.bar);
    const hashes = Number.isFinite(width)
      ? Math.max(0, width)
      : Math.round((56 * (Number(bucket.pct) || 0)) / maxPct);
    const bar = "#".repeat(hashes);
    const pct = Number(bucket.pct || 0).toFixed(1).padStart(5, " ");
    return `${String(bucket.label).padEnd(11)} ${bar.padEnd(56)} ${pct}%`;
  });
  const hz = Math.round(data.hz || 0);
  const samples = Number(data.samples || 0).toLocaleString();
  graph.textContent = [
    `Poll Rate:  ${hz} Hz`,
    `Samples:    ${samples}`,
    "",
    "Timing Distribution",
    "",
    ...lines,
  ].join("\n");
}

function ensureConnectedUi(pad) {
  const family = detectFamily(pad);
  const key = `${pad.index}:${family}:${pad.axes.length}:${pad.buttons.length}`;
  if (uiKey === key) return;
  uiKey = key;
  traces.left = {};
  traces.right = {};
  const vibrate = pad.vibrationActuator?.playEffect ? "Yes" : "n/a";
  const name = padTitle(pad.id);
  htBody.innerHTML = `
    <div class="ht-connected">
      <div class="ht-main">
        <h2 class="ht-name">${name.big}</h2>
        ${name.small ? `<p class="ht-sub">${name.small}</p>` : ""}
        <div class="ht-stats">
          <span>INDEX <b id="ht-index">${pad.index}</b></span>
          <span>CONNECTED <b>Yes</b></span>
          <span>MAPPING <b id="ht-map"></b></span>
          <span>TIMESTAMP <b id="ht-time"></b></span>
          <span>VIBRATION <b>${vibrate}</b></span>
        </div>
        <div class="ht-buttons" id="ht-buttons"></div>
        <div class="circ-panel">
          <div class="circ-col">
            <div class="circ-meta">
              <h4>L STICK</h4>
              <div class="circ-axes">
                <div>AXIS 0: <span id="l-ax0">0.00000</span></div>
                <div>AXIS 1: <span id="l-ax1">0.00000</span></div>
              </div>
            </div>
            <div class="circ-plot">
              <canvas id="stick-canvas-l" width="160" height="160"></canvas>
              <div class="avg-error" id="l-error"></div>
            </div>
          </div>
          <div class="circ-col">
            <div class="circ-meta">
              <h4>R STICK</h4>
              <div class="circ-axes">
                <div>AXIS 2: <span id="r-ax0">0.00000</span></div>
                <div>AXIS 3: <span id="r-ax1">0.00000</span></div>
              </div>
            </div>
            <div class="circ-plot">
              <canvas id="stick-canvas-r" width="160" height="160"></canvas>
              <div class="avg-error" id="r-error"></div>
            </div>
          </div>
        </div>
        <div class="ht-axes" id="ht-axes"></div>
        <div class="ht-controls">
          <button class="circ-btn${testCircularity ? " on" : ""}" id="circ-toggle" type="button">Test Circularity</button>
          <span class="circ-help">Spin joysticks slowly to test</span>
          <button id="rumble-btn" type="button">Vibration, 1 sec</button>
        </div>
      </div>
      ${controllerSvg()}
    </div>`;

  document.getElementById("circ-toggle").addEventListener("click", () => {
    testCircularity = !testCircularity;
    document.getElementById("circ-toggle").classList.toggle("on", testCircularity);
    traces.left = {};
    traces.right = {};
  });
  document.getElementById("rumble-btn").addEventListener("click", async () => {
    const current = navigator.getGamepads?.()[chosenIndex];
    const actuator = current?.vibrationActuator;
    if (!actuator?.playEffect) return;
    await actuator.playEffect("dual-rumble", {
      startDelay: 0,
      duration: 1000,
      strongMagnitude: 1,
      weakMagnitude: 1,
    });
  });

  document.getElementById("ht-axes").innerHTML = pad.axes
    .map((_, i) => `<div class="ht-axis"><span>Axis [${i}]</span><div class="track"><div class="mid"></div><div class="fill" id="axis-fill-${i}"></div></div><span id="axis-val-${i}">0.00</span></div>`)
    .join("");
  document.getElementById("ht-buttons").innerHTML = pad.buttons
    .map((_, i) => `<div class="ht-btn" id="ht-b-${i}"><div class="fill"></div><span>B${i}</span><small id="ht-bv-${i}">0.00</small></div>`)
    .join("");
}

function mixRgb(a, b, t) {
  return a.map((v, i) => Math.round(v + (b[i] - v) * t));
}

function drawCircularity(canvas, map, x, y, errorEl) {
  if (!canvas) return;
  const ctx = canvas.getContext("2d");
  const w = canvas.width;
  const h = canvas.height;
  const cx = w / 2;
  const cy = h / 2;
  const scale = Math.min(w, h) * 0.47;
  ctx.clearRect(0, 0, w, h);

  ctx.strokeStyle = testerDark() ? "rgba(255,255,255,0.28)" : "hsla(210,90%,20%,0.25)";
  ctx.beginPath();
  ctx.arc(cx, cy, scale, 0, Math.PI * 2);
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(cx, cy - scale);
  ctx.lineTo(cx, cy + scale);
  ctx.moveTo(cx - scale, cy);
  ctx.lineTo(cx + scale, cy);
  ctx.stroke();

  const blue = [0, 38, 255];
  const green = [13, 230, 13];
  const red = [255, 13, 13];
  Object.entries(map).forEach(([ang, radius]) => {
    const a = Number(ang);
    const f = Math.max(-1, Math.min(1, (radius - 1) * 5));
    const rgb = f < 0 ? mixRgb(blue, green, -f) : mixRgb(blue, red, f);
    ctx.beginPath();
    ctx.moveTo(cx, cy);
    ctx.lineTo(cx + Math.cos(a - ANGLE_STEP / 2) * radius * scale, cy + Math.sin(a - ANGLE_STEP / 2) * radius * scale);
    ctx.lineTo(cx + Math.cos(a + ANGLE_STEP / 2) * radius * scale, cy + Math.sin(a + ANGLE_STEP / 2) * radius * scale);
    ctx.closePath();
    ctx.fillStyle = `rgba(${rgb[0]},${rgb[1]},${rgb[2]},0.5)`;
    ctx.fill();
  });

  ctx.fillStyle = testerDark() ? "#f2f4f7" : "hsl(210,90%,20%)";
  ctx.beginPath();
  ctx.arc(cx + x * scale, cy + y * scale, 4, 0, Math.PI * 2);
  ctx.fill();

  const err = avgError(map);
  if (!errorEl) return;
  errorEl.innerHTML = err == null ? "" : `<span>Avg Error:</span><b>${err.toFixed(1)}%</b>`;
}

function renderTester() {
  const pads = [...(navigator.getGamepads?.() || [])];
  while (pads.length < 4) pads.push(null);
  renderSlots(pads);

  if (pads.some(Boolean) && !pads[chosenIndex]) {
    chosenIndex = pads.findIndex((p) => p);
    slotKey = "";
  }

  const pad = pads[chosenIndex];
  if (!pad) {
    statusDot.classList.remove("live");
    statusText.textContent = "Connect your gamepad and press buttons to begin...";
    if (uiKey !== "empty") {
      uiKey = "empty";
      htBody.innerHTML = emptyArt();
    }
    paintController(MOCK_PAD);
    return;
  }

  statusDot.classList.add("live");
  statusText.textContent = pad.id;
  ensureConnectedUi(pad);

  document.getElementById("ht-map").textContent = pad.mapping || "none";
  document.getElementById("ht-time").textContent = Number(pad.timestamp).toFixed(5);

  pad.axes.forEach((axis, i) => {
    const fill = document.getElementById(`axis-fill-${i}`);
    const val = document.getElementById(`axis-val-${i}`);
    if (!fill || !val) return;
    const pct = Math.abs(axis) * 50;
    fill.style.width = `${pct}%`;
    fill.style.left = axis < 0 ? `${50 - pct}%` : "50%";
    val.textContent = axis.toFixed(5);
  });

  pad.buttons.forEach((button, i) => {
    const el = document.getElementById(`ht-b-${i}`);
    if (!el) return;
    el.classList.toggle("on", button.pressed);
    el.querySelector(".fill").style.height = `${Math.round(button.value * 100)}%`;
    const small = document.getElementById(`ht-bv-${i}`);
    if (small) small.textContent = button.value.toFixed(2);
  });

  paintController(pad);

  const lx = pad.axes[0] || 0;
  const ly = pad.axes[1] || 0;
  const rx = pad.axes[2] || 0;
  const ry = pad.axes[3] || 0;
  if (testCircularity) {
    const lr = hypot(lx, ly);
    const rr = hypot(rx, ry);
    if (lr > 0.2) {
      const a = snapAngle(lx, ly);
      traces.left[a] = Math.max(traces.left[a] || 0, lr);
    }
    if (rr > 0.2) {
      const a = snapAngle(rx, ry);
      traces.right[a] = Math.max(traces.right[a] || 0, rr);
    }
  }
  drawCircularity(document.getElementById("stick-canvas-l"), traces.left, lx, ly, document.getElementById("l-error"));
  drawCircularity(document.getElementById("stick-canvas-r"), traces.right, rx, ry, document.getElementById("r-error"));
  const l0 = document.getElementById("l-ax0");
  const l1 = document.getElementById("l-ax1");
  const r0 = document.getElementById("r-ax0");
  const r1 = document.getElementById("r-ax1");
  if (l0) l0.textContent = lx.toFixed(5);
  if (l1) l1.textContent = ly.toFixed(5);
  if (r0) r0.textContent = rx.toFixed(5);
  if (r1) r1.textContent = ry.toFixed(5);
}

htSlots.addEventListener("click", (event) => {
  const slot = event.target.closest("[data-slot]");
  if (!slot) return;
  chosenIndex = Number(slot.dataset.slot);
  uiKey = "";
  slotKey = "";
});

window.addEventListener("gamepadconnected", () => {
  uiKey = "";
  slotKey = "";
});
window.addEventListener("gamepaddisconnected", () => {
  uiKey = "";
  slotKey = "";
});

requestAnimationFrame(function loop() {
  renderTester();
  requestAnimationFrame(loop);
});
