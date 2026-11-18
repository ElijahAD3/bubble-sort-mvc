(() => {
  const themes = ["system", "light", "dark"];
  const accents = ["blue", "purple", "teal"];
  const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
  let theme = "system";
  let accent = "blue";
  let storageAvailable = true;

  try {
    const savedTheme = localStorage.getItem("bubble-sort-theme");
    const savedAccent = localStorage.getItem("bubble-sort-accent");
    if (themes.includes(savedTheme)) theme = savedTheme;
    if (accents.includes(savedAccent)) accent = savedAccent;
  } catch (error) {
    storageAvailable = false;
    console.warn("Appearance preferences cannot be read from browser storage.", error);
  }

  function applyAppearance() {
    document.documentElement.dataset.bsTheme = theme === "system"
      ? (systemTheme.matches ? "dark" : "light")
      : theme;
    document.documentElement.dataset.accent = accent;
  }

  function save(key, value) {
    try {
      localStorage.setItem(key, value);
    } catch (error) {
      storageAvailable = false;
      console.warn("Appearance preferences cannot be saved in browser storage.", error);
    }
    const status = document.getElementById("appearance-status");
    status.textContent = storageAvailable ? "" : "Appearance choices won't be saved in this browser.";
  }

  applyAppearance();
  systemTheme.addEventListener("change", () => {
    if (theme === "system") applyAppearance();
  });

  document.addEventListener("DOMContentLoaded", () => {
    const themeChoice = document.getElementById("theme-choice");
    const accentChoice = document.getElementById("accent-choice");
    themeChoice.value = theme;
    accentChoice.value = accent;
    if (!storageAvailable)
      document.getElementById("appearance-status").textContent = "Appearance choices won't be saved in this browser.";

    themeChoice.addEventListener("change", () => {
      if (!themes.includes(themeChoice.value)) return;
      theme = themeChoice.value;
      applyAppearance();
      save("bubble-sort-theme", theme);
    });
    accentChoice.addEventListener("change", () => {
      if (!accents.includes(accentChoice.value)) return;
      accent = accentChoice.value;
      applyAppearance();
      save("bubble-sort-accent", accent);
    });
  });
})();
