"use strict";
(() => {
  const root = document.documentElement;
  const find = id => document.getElementById(id);
  const media = query => typeof window.matchMedia === "function" ? window.matchMedia(query) : { matches: false };
  // Safari versions before MediaQueryList.addEventListener use addListener.
  const listen = (query, callback) => {
    if (query.addEventListener) query.addEventListener("change", callback);
    else if (query.addListener) query.addListener(callback);
  };
  const read = key => { try { return localStorage.getItem(key); } catch (_) { return null; } };
  const save = (key, value) => { try { localStorage.setItem(key, value); } catch (_) { /* Session choice still works. */ } };
  const systemDark = media("(prefers-color-scheme: dark)");
  const reduced = media("(prefers-reduced-motion: reduce)");
  let chosenTheme = read("keefetch-theme");
  let chosenMotion = read("keefetch-motion");
  const themeToggle = find("theme-toggle");
  const motionToggle = find("motion-toggle");
  const setTheme = dark => {
    root.dataset.theme = dark ? "dark" : "light";
    if (themeToggle) {
      themeToggle.setAttribute("aria-pressed", String(dark));
      themeToggle.setAttribute("aria-label", "Dark theme");
      themeToggle.title = dark ? "Switch to light theme" : "Switch to dark theme";
    }
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.content = dark ? "#191520" : "#faf8f4";
  };
  setTheme(root.dataset.theme === "dark");
  if (themeToggle) {
    themeToggle.hidden = false;
    themeToggle.addEventListener("click", () => {
      chosenTheme = root.dataset.theme === "dark" ? "light" : "dark";
      setTheme(chosenTheme === "dark");
      save("keefetch-theme", chosenTheme);
    });
  }
  listen(systemDark, () => {
    if (chosenTheme !== "light" && chosenTheme !== "dark") setTheme(systemDark.matches);
  });

  const menuToggle = find("menu-toggle");
  const nav = find("primary-nav");
  const closeMenu = (restore = false) => {
    if (!nav || !menuToggle) return;
    nav.classList.remove("is-open");
    menuToggle.setAttribute("aria-expanded", "false");
    menuToggle.setAttribute("aria-label", "Open navigation");
    if (restore) menuToggle.focus();
  };
  if (menuToggle && nav) {
    menuToggle.hidden = false;
    menuToggle.addEventListener("click", () => {
      const open = nav.classList.toggle("is-open");
      menuToggle.setAttribute("aria-expanded", String(open));
      menuToggle.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
    });
    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && nav.classList.contains("is-open")) closeMenu(true);
    });
    document.addEventListener("click", event => {
      if (event.target instanceof Element && !event.target.closest(".site-header")) closeMenu();
    });
    nav.addEventListener("click", event => {
      if (event.target instanceof Element && event.target.closest("a")) closeMenu();
    });
    listen(media("(min-width: 1153px)"), event => closeMenu(!event.matches && nav.contains(document.activeElement)));
    root.dataset.enhanced = "true";
  }

  // Illustration only: sample brand assets are local. No vault or domain requests.
  const entries = Array.from(document.querySelectorAll(".vault-entry"));
  const before = find("demo-before");
  const after = find("demo-after");
  const status = find("demo-status");
  const count = find("demo-count");
  const state = find("demo-state");
  const announcement = find("demo-announcement");
  let timers = [];
  let running = false;
  let showIcons = true;
  const clearTimers = () => { timers.forEach(clearTimeout); timers = []; running = false; };
  const complete = announce => {
    clearTimers();
    entries.forEach(entry => entry.classList.toggle("is-resolved", showIcons));
    if (count) count.textContent = (showIcons ? entries.length : 0) + " / " + entries.length;
    if (status) status.textContent = showIcons ? "Same entries. Much easier to spot." : "Same entries. Same blank icons.";
    if (state) state.textContent = showIcons ? "With KeeFetch" : "Before";
    if (announce && announcement) announcement.textContent = showIcons ? "Sample website icons shown. This is an illustration, not a live fetch." : "Sample entries shown without website icons.";
  };
  const selectDemo = withIcons => {
    clearTimers();
    showIcons = withIcons;
    if (before) before.setAttribute("aria-pressed", String(!withIcons));
    if (after) after.setAttribute("aria-pressed", String(withIcons));
    if (!withIcons || root.dataset.motion === "off" || document.hidden) return complete(true);
    entries.forEach(entry => entry.classList.remove("is-resolved"));
    if (announcement) announcement.textContent = "";
    if (state) state.textContent = "With KeeFetch";
    if (status) status.textContent = "Adding a little character…";
    if (count) count.textContent = "0 / " + entries.length;
    running = true;
    entries.forEach((entry, index) => {
      timers.push(setTimeout(() => {
        entry.classList.add("is-resolved");
        if (count) count.textContent = (index + 1) + " / " + entries.length;
      }, 100 + index * 110));
    });
    timers.push(setTimeout(() => complete(true), 200 + entries.length * 110));
  };
  if (before && after && entries.length) {
    find("demo-controls").hidden = false;
    before.addEventListener("click", () => selectDemo(false));
    after.addEventListener("click", () => selectDemo(true));
    complete(false);
  }
  const setMotion = () => {
    const off = reduced.matches || chosenMotion === "off";
    root.dataset.motion = off ? "off" : "on";
    if (motionToggle) {
      motionToggle.setAttribute("aria-pressed", String(off));
      motionToggle.setAttribute("aria-label", "Reduce motion");
      motionToggle.title = reduced.matches ? "Reduced motion follows your system setting" : (off ? "Enable icon animation" : "Reduce motion");
      motionToggle.disabled = reduced.matches;
    }
    if (off && running) complete(true);
  };
  if (motionToggle && entries.length) {
    motionToggle.hidden = false;
    motionToggle.addEventListener("click", () => {
      chosenMotion = root.dataset.motion === "off" ? "on" : "off";
      save("keefetch-motion", chosenMotion);
      setMotion();
    });
  }
  setMotion();
  listen(reduced, setMotion);
  document.addEventListener("visibilitychange", () => { if (document.hidden && running) complete(false); });
  window.addEventListener("pagehide", () => { if (running) complete(false); });
  // Reflect other tabs without requiring storage. Clearing an override returns to system.
  window.addEventListener("storage", event => {
    if (event.key === "keefetch-theme" || event.key === null) {
      chosenTheme = read("keefetch-theme");
      setTheme(chosenTheme === "dark" || (chosenTheme !== "light" && systemDark.matches));
    }
    if (event.key === "keefetch-motion" || event.key === null) {
      chosenMotion = read("keefetch-motion");
      setMotion();
    }
  });
})();
