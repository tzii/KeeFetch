"use strict";
(() => {
  const root = document.documentElement;
  const themeToggle = document.getElementById("theme-toggle");
  const motionToggle = document.getElementById("motion-toggle");
  const menuToggle = document.getElementById("menu-toggle");
  const nav = document.getElementById("primary-nav");
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const save = (key, value) => { try { localStorage.setItem(key, value); } catch (_) { /* Optional. */ } };

  const setTheme = (dark) => {
    root.dataset.theme = dark ? "dark" : "light";
    themeToggle?.setAttribute("aria-pressed", String(dark));
  };
  if (themeToggle) {
    themeToggle.hidden = false;
    setTheme(root.dataset.theme !== "light");
    themeToggle.addEventListener("click", () => {
      setTheme(root.dataset.theme !== "dark");
      save("keefetch-theme", root.dataset.theme);
    });
  }

  const closeMenu = (restoreFocus = false) => {
    nav?.classList.remove("is-open");
    menuToggle?.setAttribute("aria-expanded", "false");
    menuToggle?.setAttribute("aria-label", "Open navigation");
    if (restoreFocus) menuToggle?.focus();
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
      if (!event.target.closest(".site-header")) closeMenu();
    });
    nav.addEventListener("click", event => {
      if (event.target.closest("a")) closeMenu();
    });
    window.matchMedia("(min-width: 1001px)").addEventListener("change", () => closeMenu());
    // Navigation remains fully visible when JavaScript is unavailable.
    root.dataset.enhanced = "true";
  }

  const entries = [...document.querySelectorAll(".vault-entry")];
  const replay = document.getElementById("replay-demo");
  const status = document.getElementById("demo-status");
  const count = document.getElementById("demo-count");
  let timers = [];
  let running = false;
  const clearDemo = () => { timers.forEach(clearTimeout); timers = []; };
  const finishDemo = () => {
    clearDemo();
    entries.forEach(entry => entry.classList.add("is-resolved"));
    if (status) status.textContent = "A familiar face for every entry";
    if (count) count.textContent = entries.length + " / " + entries.length;
    running = false;
    if (replay) replay.disabled = root.dataset.motion === "off";
  };
  const playDemo = () => {
    if (!entries.length || root.dataset.motion === "off" || document.hidden) return finishDemo();
    clearDemo();
    running = true;
    if (replay) replay.disabled = true;
    entries.forEach(entry => entry.classList.remove("is-resolved"));
    status.textContent = "Finding something familiar…";
    count.textContent = "0 / " + entries.length;
    entries.forEach((entry, i) => {
      timers.push(setTimeout(() => {
        entry.classList.add("is-resolved");
        count.textContent = (i + 1) + " / " + entries.length;
      }, 650 + i * 430));
    });
    timers.push(setTimeout(finishDemo, 3700));
  };
  if (replay) {
    replay.hidden = false;
    replay.addEventListener("click", playDemo);
  }

  const setMotion = (off) => {
    root.dataset.motion = off ? "off" : "on";
    motionToggle?.setAttribute("aria-pressed", String(off));
    motionToggle?.setAttribute("title", reduceMotion.matches ? "Animations paused by your system preference" : (off ? "Enable animations" : "Pause animations"));
    if (motionToggle) motionToggle.disabled = reduceMotion.matches;
    motionToggle?.setAttribute("aria-label", reduceMotion.matches ? "Animations paused by your system preference" : "Pause animations");
    if (off) finishDemo();
    else if (replay) replay.disabled = false;
  };
  if (motionToggle) {
    motionToggle.hidden = false;
    setMotion(root.dataset.motion === "off");
    motionToggle.addEventListener("click", () => {
      // The system reduced-motion setting takes precedence.
      const off = reduceMotion.matches || root.dataset.motion !== "off";
      setMotion(off);
      save("keefetch-motion", off ? "off" : "on");
    });
  }
  reduceMotion.addEventListener("change", event => {
    let savedOff = false;
    try { savedOff = localStorage.getItem("keefetch-motion") === "off"; } catch (_) { /* Optional. */ }
    setMotion(event.matches || savedOff);
  });
  document.addEventListener("visibilitychange", () => { if (document.hidden && running) finishDemo(); });

  if ("IntersectionObserver" in window) {
    const reveal = new IntersectionObserver(observations => {
      observations.forEach(observation => {
        if (!observation.isIntersecting) return;
        observation.target.classList.add("is-visible");
        reveal.unobserve(observation.target);
      });
    }, { threshold: 0.06 });
    document.querySelectorAll("[data-reveal]").forEach(target => {
      target.classList.add("reveal-ready");
      reveal.observe(target);
    });
    const stage = document.querySelector(".vault-stage");
    if (stage) {
      const demoObserver = new IntersectionObserver(observations => {
        if (observations.some(observation => observation.isIntersecting)) {
          playDemo();
          demoObserver.disconnect();
        }
      }, { threshold: 0.4 });
      demoObserver.observe(stage);
    }
  } else finishDemo();

  // The generated comparison table is complete without JavaScript or fetch.
  const targets = document.querySelectorAll("[data-profile-list]");
  if (!targets.length) return;
  fetch("data/profiles.json").then(response => {
    if (!response.ok) throw new Error("Profile data unavailable");
    return response.json();
  }).then(data => {
    if (!Array.isArray(data.profiles)) throw new Error("Invalid profile data");
    const visible = data.profiles.filter(profile => profile.isVisible);
    if (!visible.length) throw new Error("No visible profiles");
    targets.forEach(target => {
      const cards = visible.map(profile => {
        const card = document.createElement("article");
        card.className = "profile-card";
        const name = document.createElement("h3");
        name.textContent = profile.displayName;
        const use = document.createElement("p");
        use.textContent = profile.intendedUse;
        const budget = document.createElement("p");
        budget.className = "budget";
        budget.textContent = (profile.cumulativeTimeoutMs / 1000) + " s budget";
        const description = document.createElement("p");
        description.textContent = profile.description;
        card.append(name, use, budget, description);
        return card;
      });
      target.replaceChildren(...cards);
    });
  }).catch(() => { /* Checked comparison table remains available. */ });
})();
