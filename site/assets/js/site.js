/* Progressive enhancement only. The full guide remains readable without JS. */
(function () {
  'use strict';
  var root = document.documentElement;
  var key = 'keefetch-theme';
  var themeButton = document.getElementById('theme-toggle');
  var resetButton = document.getElementById('theme-reset');
  var menuButton = document.getElementById('menu-toggle');
  var navigation = document.getElementById('primary-nav');
  var media = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null;
  var compact = typeof window.matchMedia === 'function' ? window.matchMedia('(max-width: 1152px)') : null;
  var preference = null;
  try { preference = localStorage.getItem(key); } catch (_) { /* Private/denied storage: keep state in memory. */ }
  if (preference !== 'light' && preference !== 'dark') preference = null;

  function listen(query, handler) {
    if (!query) return;
    if (query.addEventListener) query.addEventListener('change', handler);
    else if (query.addListener) query.addListener(handler);
  }
  function applyTheme() {
    var theme = preference || (media && media.matches ? 'dark' : 'light');
    root.setAttribute('data-theme', theme);
    themeButton.setAttribute('aria-pressed', String(theme === 'dark'));
    themeButton.setAttribute('aria-label', 'Switch to ' + (theme === 'dark' ? 'light' : 'dark') + ' theme');
    document.querySelector('meta[name="theme-color"]').setAttribute('content', theme === 'dark' ? '#18151d' : '#f4f1e9');
    resetButton.hidden = !preference;
  }
  themeButton.addEventListener('click', function () {
    preference = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    try { localStorage.setItem(key, preference); } catch (_) { /* The visible control still works. */ }
    applyTheme();
  });
  resetButton.addEventListener('click', function () {
    preference = null;
    try { localStorage.removeItem(key); } catch (_) { /* Fall back to the system in this tab. */ }
    applyTheme();
    themeButton.focus();
  });
  window.addEventListener('storage', function (event) {
    if (event.key !== key && event.key !== null) return;
    preference = event.newValue === 'dark' || event.newValue === 'light' ? event.newValue : null;
    applyTheme();
  });
  listen(media, function () { if (!preference) applyTheme(); });
  applyTheme();
  themeButton.hidden = false;

  function setMenu(open, restoreFocus) {
    navigation.classList.toggle('is-open', open);
    menuButton.setAttribute('aria-expanded', String(open));
    menuButton.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
    if (restoreFocus) menuButton.focus();
  }
  menuButton.addEventListener('click', function () { setMenu(menuButton.getAttribute('aria-expanded') !== 'true', false); });
  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && menuButton.getAttribute('aria-expanded') === 'true') setMenu(false, true);
  });
  navigation.addEventListener('click', function (event) {
    var link = event.target.closest('a');
    if (!link) return;
    setMenu(false, false);
    var href = link.getAttribute('href');
    if (href && href.charAt(0) === '#') {
      var target = document.getElementById(href.slice(1));
      if (target) {
        target.setAttribute('tabindex', '-1');
        target.focus({preventScroll: true});
      }
    }
  });
  listen(compact, function () {
    var restore = compact.matches && navigation.contains(document.activeElement);
    setMenu(false, restore);
  });
  menuButton.hidden = false;

  var workshop = document.getElementById('demo');
  if (workshop) {
  var before = document.getElementById('demo-before');
  var after = document.getElementById('demo-after');
  function showIcons(found) {
    // Atomic state change: rapid clicks cannot leave old timers running.
    workshop.classList.toggle('resolved', found);
    before.setAttribute('aria-pressed', String(!found));
    after.setAttribute('aria-pressed', String(found));
    document.getElementById('demo-state').textContent = found ? '4 familiar faces' : '4 generic icons';
    document.getElementById('demo-announcement').textContent = found ? 'With KeeFetch: four example entries show their website icons. This is an illustration, not a live fetch.' : 'Before: all four example entries use generic key icons.';
  }
  before.addEventListener('click', function () { showIcons(false); workshop.dispatchEvent(new CustomEvent('keefetch:comparison', {detail: {found: false}})); });
  after.addEventListener('click', function () { showIcons(true); workshop.dispatchEvent(new CustomEvent('keefetch:comparison', {detail: {found: true}})); });
  document.getElementById('demo-controls').hidden = false;

  var controls = document.getElementById('preset-controls');
  var presetButtons = controls.querySelectorAll('button');
  var panels = document.querySelectorAll('.preset-panel');
  function selectPreset(name) {
    for (var i = 0; i < presetButtons.length; i++) presetButtons[i].setAttribute('aria-pressed', String(presetButtons[i].getAttribute('data-preset') === name));
    for (var j = 0; j < panels.length; j++) panels[j].hidden = panels[j].id !== 'preset-' + name;
  }
  controls.addEventListener('click', function (event) {
    var button = event.target.closest('button[data-preset]');
    if (button) selectPreset(button.getAttribute('data-preset'));
  });
  selectPreset('everyday');
  controls.hidden = false;

  }
  var copy = document.getElementById('copy-checksum');
  if (copy) {
  copy.addEventListener('click', function () {
    var text = document.getElementById('checksum').textContent.trim();
    var status = document.getElementById('copy-status');
    function manualCopy() {
      var selection = window.getSelection();
      if (selection) {
        var range = document.createRange();
        range.selectNodeContents(document.getElementById('checksum'));
        selection.removeAllRanges();
        selection.addRange(range);
      }
      status.textContent = 'Automatic copying is unavailable. The checksum is selected; use your browser’s Copy action.';
    }
    if (!navigator.clipboard || !navigator.clipboard.writeText) { manualCopy(); return; }
    copy.disabled = true;
    navigator.clipboard.writeText(text).then(function () {
      status.textContent = 'SHA-256 copied.';
      copy.disabled = false;
    }, function () {
      copy.disabled = false;
      manualCopy();
    });
  });
  copy.hidden = false;
  }
  root.classList.add('ready');
}());
