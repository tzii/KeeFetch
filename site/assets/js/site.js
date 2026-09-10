'use strict';
(() => {
  const root = document.documentElement;
  const find = id => document.getElementById(id);
  const read = key => { try { return localStorage.getItem(key); } catch (_) { return null; } };
  const save = (key, value) => { try { localStorage.setItem(key, value); } catch (_) { /* Optional preference. */ } };
  const media = query => typeof window.matchMedia === 'function' ? window.matchMedia(query) : { matches: false };
  const watch = (query, callback) => {
    if (query.addEventListener) query.addEventListener('change', callback);
    else if (query.addListener) query.addListener(callback);
  };
  const darkSystem = media('(prefers-color-scheme: dark)');
  const reduced = media('(prefers-reduced-motion: reduce)');
  const themeButton = find('theme-toggle');
  const motionButton = find('motion-toggle');
  let chosenTheme = read('keefetch-theme');
  const applyTheme = theme => {
    root.dataset.theme = theme;
    if (themeButton) {
      themeButton.setAttribute('aria-pressed', String(theme === 'dark'));
      themeButton.title = theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme';
    }
    const chrome = document.querySelector('meta[name="theme-color"]');
    if (chrome) chrome.content = theme === 'dark' ? '#191721' : '#faf7f0';
  };
  if (themeButton) {
    themeButton.hidden = false;
    themeButton.setAttribute('aria-label', 'Dark theme');
    themeButton.addEventListener('click', () => {
      chosenTheme = root.dataset.theme === 'dark' ? 'light' : 'dark';
      applyTheme(chosenTheme);
      save('keefetch-theme', chosenTheme);
    });
  }
  applyTheme(root.dataset.theme === 'light' ? 'light' : 'dark');
  watch(darkSystem, event => {
    if (chosenTheme !== 'dark' && chosenTheme !== 'light') applyTheme(event.matches ? 'dark' : 'light');
  });
  const applyMotion = () => {
    const off = reduced.matches || read('keefetch-motion') === 'off';
    root.dataset.motion = off ? 'off' : 'on';
    if (motionButton) {
      motionButton.setAttribute('aria-pressed', String(off));
      motionButton.setAttribute('aria-label', 'Pause animations');
      motionButton.title = reduced.matches ? 'Your system has reduced motion enabled' : (off ? 'Enable animations' : 'Pause animations');
      motionButton.disabled = reduced.matches;
    }
  };
  if (motionButton) {
    motionButton.hidden = false;
    motionButton.addEventListener('click', () => {
      const off = root.dataset.motion !== 'off';
      save('keefetch-motion', off ? 'off' : 'on');
      // Apply the choice even when storage is denied.
      applyMotion();
      if (!reduced.matches) {
        root.dataset.motion = off ? 'off' : 'on';
        motionButton.setAttribute('aria-pressed', String(off));
        motionButton.title = off ? 'Enable animations' : 'Pause animations';
      }
    });
  }
  applyMotion();
  watch(reduced, applyMotion);
  window.addEventListener('storage', event => {
    if (event.key === 'keefetch-theme' || event.key === null) {
      chosenTheme = read('keefetch-theme');
      applyTheme(chosenTheme === 'dark' || chosenTheme === 'light' ? chosenTheme : (darkSystem.matches ? 'dark' : 'light'));
    }
    if (event.key === 'keefetch-motion' || event.key === null) applyMotion();
  });

  const menuButton = find('menu-toggle');
  const nav = find('primary-nav');
  const closeMenu = restoreFocus => {
    if (!nav || !menuButton) return;
    nav.classList.remove('is-open');
    menuButton.setAttribute('aria-expanded', 'false');
    menuButton.setAttribute('aria-label', 'Open navigation');
    if (restoreFocus) menuButton.focus();
  };
  if (menuButton && nav) {
    menuButton.hidden = false;
    menuButton.addEventListener('click', () => {
      const open = nav.classList.toggle('is-open');
      menuButton.setAttribute('aria-expanded', String(open));
      menuButton.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
    });
    document.addEventListener('keydown', event => {
      if (event.key === 'Escape' && nav.classList.contains('is-open')) closeMenu(true);
    });
    document.addEventListener('click', event => {
      if (!event.target.closest('.site-header')) closeMenu(false);
    });
    nav.addEventListener('click', event => { if (event.target.closest('a')) closeMenu(false); });
    watch(media('(min-width: 1001px)'), () => closeMenu(false));
    root.dataset.enhanced = 'true';
  }

  // This is a local illustration, not a network request or a plugin benchmark.
  const stage = document.querySelector('.vault-stage');
  const entries = Array.from(document.querySelectorAll('.vault-entry'));
  const controls = find('demo-controls');
  const status = find('demo-status');
  const count = find('demo-count');
  const buttons = Array.from(document.querySelectorAll('[data-demo-state]'));
  let demoFrame = null;
  if (stage && controls && entries.length && status && count) {
    controls.hidden = false;
    const show = after => {
      if (demoFrame !== null) cancelAnimationFrame(demoFrame);
      stage.classList.remove('demo-animate');
      stage.dataset.demo = after ? 'after' : 'before';
      entries.forEach(entry => entry.classList.toggle('is-resolved', after));
      buttons.forEach(button => button.setAttribute('aria-pressed', String(button.dataset.demoState === stage.dataset.demo)));
      status.textContent = after ? 'Same entries. Easier to recognize.' : 'Same entries. All wearing the same icon.';
      count.textContent = after ? entries.length + ' / ' + entries.length : '0 / ' + entries.length;
      if (after && root.dataset.motion !== 'off') {
        demoFrame = requestAnimationFrame(() => { stage.classList.add('demo-animate'); demoFrame = null; });
      }
    };
    buttons.forEach(button => button.addEventListener('click', () => show(button.dataset.demoState === 'after')));
  }

  // Optional reading index. The complete guide remains visible without scripts.
  const docs = document.querySelector('.docs-main');
  if (docs) {
    const sections = Array.from(docs.children).filter(child => child.tagName === 'SECTION');
    let index = docs.querySelector('.doc-index');
    if (!index && sections.length > 1) {
      index = document.createElement('nav');
      index.className = 'doc-index';
      index.setAttribute('aria-label', 'On this page');
      sections.forEach((section, number) => {
        const heading = section.querySelector('h2');
        if (!heading) return;
        if (!section.id) section.id = 'section-' + (number + 1);
        const link = document.createElement('a');
        link.href = '#' + section.id;
        link.textContent = heading.textContent;
        index.appendChild(link);
      });
      docs.insertBefore(index, sections[0]);
    }
    if (index) docs.dataset.index = 'true';
  }

  // Static generated comparison is authoritative. These summaries are optional.
  const targets = document.querySelectorAll('[data-profile-list]');
  if (!targets.length || typeof fetch !== 'function') return;
  fetch('data/profiles.json').then(response => {
    if (!response.ok) throw new Error('Profile data unavailable');
    return response.json();
  }).then(data => {
    if (!Array.isArray(data.profiles)) throw new Error('Invalid profile data');
    const visible = data.profiles.filter(profile => profile.isVisible);
    if (!visible.length || visible.some(profile => typeof profile.displayName !== 'string' || typeof profile.description !== 'string' || !Number.isFinite(profile.cumulativeTimeoutMs))) throw new Error('Incomplete profile data');
    targets.forEach(target => {
      const cards = visible.map(profile => {
        const card = document.createElement('article');
        card.className = 'profile-card';
        [['h3', profile.displayName], ['p', profile.intendedUse], ['p', (profile.cumulativeTimeoutMs / 1000) + ' s budget'], ['p', profile.description]].forEach(([tag, text], index) => {
          const node = document.createElement(tag);
          node.textContent = text;
          if (index === 2) node.className = 'budget';
          card.appendChild(node);
        });
        return card;
      });
      target.replaceChildren(...cards);
    });
  }).catch(() => { /* The checked comparison table is already in the HTML. */ });
})();
