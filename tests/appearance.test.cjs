const test = require("node:test");
const assert = require("node:assert/strict");
const vm = require("node:vm");
const fs = require("node:fs");
const path = require("node:path");

const script = fs.readFileSync(path.join(__dirname, "../wwwroot/js/appearance.js"), "utf8");

function setup(initial = {}, dark = false, blocked = false) {
  const storage = { ...initial };
  const handlers = {};
  const controls = {
    "theme-choice": { value: "", addEventListener: (_, fn) => handlers.theme = fn },
    "accent-choice": { value: "", addEventListener: (_, fn) => handlers.accent = fn },
    "appearance-status": { textContent: "" }
  };
  const media = { matches: dark, addEventListener: (_, fn) => handlers.system = fn };
  const document = {
    documentElement: { dataset: {} },
    addEventListener: (_, fn) => handlers.ready = fn,
    getElementById: id => controls[id]
  };
  const localStorage = {
    getItem: key => { if (blocked) throw new Error("blocked"); return storage[key] ?? null; },
    setItem: (key, value) => { if (blocked) throw new Error("blocked"); storage[key] = value; }
  };
  vm.runInNewContext(script, { document, localStorage, window: { matchMedia: () => media }, console: { warn() {} } });
  handlers.ready();
  return { document, controls, handlers, media, storage };
}

test("defaults to system and responds to system changes", () => {
  const app = setup();
  assert.equal(app.controls["theme-choice"].value, "system");
  assert.equal(app.document.documentElement.dataset.bsTheme, "light");
  app.media.matches = true;
  app.handlers.system();
  assert.equal(app.document.documentElement.dataset.bsTheme, "dark");
});

test("persists choices and restores them on another page", () => {
  const app = setup();
  app.controls["theme-choice"].value = "dark";
  app.handlers.theme();
  app.controls["accent-choice"].value = "teal";
  app.handlers.accent();
  const restored = setup(app.storage);
  assert.equal(restored.document.documentElement.dataset.bsTheme, "dark");
  assert.equal(restored.document.documentElement.dataset.accent, "teal");
  restored.media.matches = false;
  restored.handlers.system();
  assert.equal(restored.document.documentElement.dataset.bsTheme, "dark");
});

test("rejects unknown preferences and reports blocked storage", () => {
  const app = setup({ "bubble-sort-theme": "unexpected", "bubble-sort-accent": "unexpected" }, true);
  assert.equal(app.document.documentElement.dataset.bsTheme, "dark");
  assert.equal(app.document.documentElement.dataset.accent, "blue");
  const blocked = setup({}, false, true);
  assert.match(blocked.controls["appearance-status"].textContent, /won't be saved/);
});
