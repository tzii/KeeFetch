/* Resolve preferences before paint; storage is entirely optional. */
(() => {
  let theme = 'dark';
  let motion = 'on';
  try {
    theme = localStorage.getItem('keefetch-theme') === 'light' ? 'light' : 'dark';
    motion = localStorage.getItem('keefetch-motion') === 'off' ? 'off' : 'on';
  } catch (_) { /* Privacy modes may deny storage. */ }
  document.documentElement.dataset.theme = theme;
  document.documentElement.dataset.motion = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'off' : motion;
})();
