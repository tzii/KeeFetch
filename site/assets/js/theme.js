/* Before paint. A saved choice wins; otherwise follow the operating system. */
(() => {
  let theme = null;
  let motion = null;
  try {
    theme = localStorage.getItem('keefetch-theme');
    motion = localStorage.getItem('keefetch-motion');
  } catch (_) { /* Storage is optional, including in private browsing. */ }
  const matches = query => typeof window.matchMedia === 'function' && window.matchMedia(query).matches;
  document.documentElement.dataset.theme = theme === 'light' || theme === 'dark'
    ? theme : (matches('(prefers-color-scheme: dark)') ? 'dark' : 'light');
  document.documentElement.dataset.motion = matches('(prefers-reduced-motion: reduce)') || motion === 'off' ? 'off' : 'on';
})();
