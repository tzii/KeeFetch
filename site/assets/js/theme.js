/* Run before the stylesheet paints. Storage is optional, never a requirement. */
"use strict";
(() => {
  const root = document.documentElement;
  const matches = query => typeof window.matchMedia === "function" && window.matchMedia(query).matches;
  let theme = null;
  let motion = null;
  try {
    theme = localStorage.getItem("keefetch-theme");
    motion = localStorage.getItem("keefetch-motion");
  } catch (_) { /* Private/blocked storage: follow the system in this session. */ }
  root.dataset.theme = theme === "dark" || theme === "light" ? theme : (matches("(prefers-color-scheme: dark)") ? "dark" : "light");
  root.dataset.motion = matches("(prefers-reduced-motion: reduce)") || motion === "off" ? "off" : "on";
  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta) meta.content = root.dataset.theme === "dark" ? "#191520" : "#faf8f4";
})();
