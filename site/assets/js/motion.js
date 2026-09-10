/* KeeFetch's little icon machine.
 * One finite clock owns the whole scene. SVG coordinates keep every hinge and
 * roller on its authored pivot; no layout reads or random timers in the loop.
 * The comparison in site.js remains the authoritative, immediate result.
 */
(function () {
  'use strict';
  var root = document.documentElement;
  var workshop = document.getElementById('demo');
  if (!workshop) return;
  var machine = workshop.querySelector('.machine');
  var controls = document.getElementById('motion-controls');
  var toggle = document.getElementById('motion-toggle');
  var replay = document.getElementById('replay-motion');
  var pauseButton = document.getElementById('pause-machine');
  if (!machine || !controls || !toggle || !replay || !pauseButton) return;

  var DURATION = 4900;
  var KEY = 'keefetch-motion'; // Shared website motion preference.
  var reduce = typeof matchMedia === 'function' ? matchMedia('(prefers-reduced-motion: reduce)') : null;
  var printMedia = typeof matchMedia === 'function' ? matchMedia('print') : null;
  var supported = typeof Element.prototype.animate === 'function' &&
    typeof requestAnimationFrame === 'function' && typeof cancelAnimationFrame === 'function';
  var preference = null;
  var enabled = false;
  var mode = 'idle'; // idle | playing | paused
  var frame = null;
  var startTime = 0;
  var elapsed = 0;
  var generation = 0;
  var autoPlayed = false;
  var printing = false;
  var suspended = false;
  var phase = '';
  var count = -1;
  var renderedFrames = 0;
  var effects = new Set();
  var presetEffects = new Set();
  var caption = document.getElementById('machine-caption');
  var counter = document.getElementById('machine-count');
  var demoState = document.getElementById('demo-state');
  var announcement = document.getElementById('demo-announcement');
  try { preference = localStorage.getItem(KEY); } catch (_) { /* Memory-only preferences. */ }
  if (preference !== 'on' && preference !== 'off') preference = null;

  function one(selector) { return machine.querySelector(selector); }
  function many(selector) { return Array.from(machine.querySelectorAll(selector)); }
  var art = {
    core: one('.machine-core'), lever: one('.machine-lever'), antenna: one('.machine-antenna'),
    lamp: one('.antenna-lamp'), eyes: one('.machine-eyes'), left: one('.eye-left'), right: one('.eye-right'),
    rotor: one('.window-rotor'), door: one('.vault-door'), scan: one('.scan-beam'),
    upper: one('.scanner-upper'), forearm: one('.scanner-forearm'), head: one('.scanner-head'),
    beam: one('.scanner-light'), needle: one('.gauge-needle'), piston: one('.match-piston'),
    inlet: one('.intake-gate'), outlet: one('.output-gate'), sparks: one('.machine-sparkles'),
    wheels: many('.belt-wheel'), treads: many('.belt-tread'), inputs: many('.feed-tile'),
    outputs: many('.output-tile'), checks: many('.tile-check')
  };
  var entries = Array.from(workshop.querySelectorAll('.vault-entry')).map(function (entry) {
    return {picture: entry.querySelector('.entry-picture'), found: entry.querySelector('.found-icon'),
      empty: entry.querySelector('.empty-icon'), tick: entry.querySelector('.entry-tick')};
  });
  // If this optional component is partially removed, leave the original page usable.
  if (Object.keys(art).some(function (key) { return !art[key]; }) ||
      art.inputs.length !== 4 || art.outputs.length !== 4 || entries.length !== 4) return;

  var inputX = [204, 158, 112, 66];
  var outputX = [594, 548, 502, 456];
  var arrivals = inputX.map(function (x) { return 450 + (276 - x) / 0.085; });
  var emissions = arrivals.map(function (t) { return t + 390; });
  var docking = outputX.map(function (x, i) { return emissions[i] + (x - 404) / 0.14 + 120; });
  function clamp(n, min, max) { return Math.min(max, Math.max(min, n)); }
  function progress(t, start, duration) { return clamp((t - start) / duration, 0, 1); }
  function ease(p) { return p * p * (3 - 2 * p); }
  function bump(t, start, duration) { var p = progress(t, start, duration); return Math.sin(p * Math.PI); }
  function number(n) { return Number(n.toFixed(3)); }
  function attr(node, name, value) { if (node) node.setAttribute(name, String(value)); }
  function translate(node, x, y) { attr(node, 'transform', 'translate(' + number(x) + ' ' + number(y) + ')'); }
  function rotate(node, degrees) { attr(node, 'transform', 'rotate(' + number(degrees) + ')'); }
  function opacity(node, value) { attr(node, 'opacity', number(value)); }
  function listen(query, handler) {
    if (!query) return;
    if (query.addEventListener) query.addEventListener('change', handler);
    else if (query.addListener) query.addListener(handler);
  }
  function blocked() { return !enabled || document.hidden || printing || suspended || !!(printMedia && printMedia.matches); }
  function visibleScene() {
    // A single read at a user action, never in the animation loop.
    var rect = machine.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0 && rect.bottom > 0 && rect.top < innerHeight;
  }
  function suspendMotion() {
    suspended = true;
    root.setAttribute('data-motion-suspended', 'true');
    stopAll();
  }
  function releaseSuspension() {
    if (!document.hidden && !printing && !(printMedia && printMedia.matches)) {
      suspended = false;
      root.removeAttribute('data-motion-suspended');
    }
  }
  function setPhase(next, text) {
    if (phase === next) return;
    phase = next;
    workshop.setAttribute('data-machine-phase', next);
    caption.textContent = text;
  }
  function setCount(value) {
    if (count === value) return;
    count = value;
    counter.textContent = ('0' + value).slice(-2);
  }
  function restoreEntries() {
    entries.forEach(function (entry) {
      [entry.picture, entry.found, entry.empty, entry.tick].forEach(function (node) {
        node.style.removeProperty('transform');
        node.style.removeProperty('opacity');
      });
    });
  }
  function buttons() {
    var active = mode === 'playing' || mode === 'paused';
    var wasPauseFocused = document.activeElement === pauseButton;
    pauseButton.disabled = !active || !enabled;
    pauseButton.setAttribute('data-paused', String(mode === 'paused'));
    pauseButton.setAttribute('aria-label', mode === 'paused' ? 'Resume machine animation' : 'Pause machine animation');
    pauseButton.querySelector('span').textContent = mode === 'paused' ? 'Resume' : 'Pause';
    replay.querySelector('span').textContent = active ? 'Run again' : 'Run machine';
    // Finishing a sequence must not strand keyboard focus on a disabled control.
    if (wasPauseFocused && pauseButton.disabled) {
      (replay.disabled ? document.getElementById('demo-after') : replay).focus({preventScroll: true});
    }
  }
  function cancelFrame() {
    if (frame !== null) cancelAnimationFrame(frame);
    frame = null;
    generation++; // Even an already queued callback cannot revive an old run.
  }
  function restingPose(completed) {
    var found = workshop.classList.contains('resolved');
    translate(art.core, 0, 0); rotate(art.lever, 0); rotate(art.antenna, 0);
    translate(art.eyes, 0, 0); attr(art.left, 'transform', 'scale(1 1)'); attr(art.right, 'transform', 'scale(1 1)');
    rotate(art.rotor, 0); attr(art.door, 'transform', 'scale(1 1)'); opacity(art.scan, 0);
    rotate(art.upper, 0); rotate(art.forearm, 0); rotate(art.head, 0); opacity(art.beam, 0);
    rotate(art.needle, found ? 55 : -55); translate(art.piston, 0, 0);
    translate(art.inlet, 0, 0); translate(art.outlet, 0, 0); opacity(art.sparks, 0);
    attr(art.lamp, 'fill', found ? '#ac81e2' : '#9c8ca6');
    art.wheels.forEach(function (wheel) { rotate(wheel, 0); });
    art.treads.forEach(function (tread) { attr(tread, 'stroke-dashoffset', 0); });
    art.inputs.forEach(function (tile) { translate(tile, 0, 0); opacity(tile, found ? 0.48 : 1); });
    art.outputs.forEach(function (tile, i) { translate(tile, outputX[i], 184); opacity(tile, found ? 1 : 0); });
    art.checks.forEach(function (check) { opacity(check, found ? 1 : 0); });
    restoreEntries();
    setCount(found ? 4 : 0);
    setPhase(found ? 'done' : 'ready', found ? (completed ? 'Four familiar faces. Job done.' : 'A little machine. Four familiar faces.') : 'Generic icons in. Familiar faces out.');
    demoState.textContent = found ? '4 familiar faces' : '4 generic icons';
  }
  function stop(completed) {
    cancelFrame();
    mode = 'idle';
    elapsed = completed ? DURATION : 0;
    workshop.setAttribute('data-sequence', mode);
    restingPose(!!completed);
    buttons();
  }
  function cancelEffects(group) {
    Array.from(group).forEach(function (effect) { effect.cancel(); effects.delete(effect); });
    group.clear();
  }
  function stopAll() { stop(false); cancelEffects(effects); presetEffects.clear(); }

  // A deterministic pose for an elapsed time: pause/resume is exact, and every
  // replay starts from the same physical positions. No animation promises race.
  function render(t) {
    renderedFrames++;
    var drive = clamp(t - 450, 0, 3350);
    var motor = t >= 450 && t < 3800;
    var scan = 0, inlet = 0, outlet = 0, piston = 0, delivered = 0;
    for (var i = 0; i < 4; i++) {
      var inputTravel = clamp((t - 450) * 0.085, 0, 320 - inputX[i]);
      translate(art.inputs[i], inputTravel, -1.5 * bump(t, 450, 300));
      opacity(art.inputs[i], 1 - progress(inputX[i] + inputTravel, 248, 27));
      scan = Math.max(scan, bump(t, arrivals[i] - 845, 440));
      inlet = Math.max(inlet, bump(t, arrivals[i] - 380, 580));
      outlet = Math.max(outlet, bump(t, emissions[i] - 110, 350));
      piston = Math.max(piston, bump(t, arrivals[i] + 90, 265));

      var travel = progress(t, emissions[i], docking[i] - emissions[i] - 120);
      var settle = bump(t, docking[i] - 120, 240);
      var x = 404 + (outputX[i] - 404) * travel;
      var y = 184 - 4 * Math.sin(travel * Math.PI);
      attr(art.outputs[i], 'transform', 'translate(' + number(x) + ' ' + number(y) + ') rotate(' + number(-5 * Math.sin(travel * Math.PI)) + ') scale(' + number(1 + .055 * settle) + ' ' + number(1 - .045 * settle) + ')');
      opacity(art.outputs[i], progress(t, emissions[i], 100));
      opacity(art.checks[i], progress(t, docking[i] - 30, 150));
      if (t >= docking[i]) delivered++;

      // The accessible comparison has already changed atomically. This is only
      // its decorative reveal; cancelling removes every inline visual override.
      var reveal = ease(progress(t, docking[i] - 100, 220));
      entries[i].found.style.opacity = String(reveal);
      entries[i].found.style.transform = 'scale(' + number(.72 + .28 * reveal) + ')';
      entries[i].empty.style.opacity = String(1 - reveal);
      entries[i].tick.style.opacity = String(reveal);
      entries[i].picture.style.transform = 'scale(' + number(1 + .085 * bump(t, docking[i], 330)) + ')';
    }
    setCount(delivered);
    var wake = bump(t, 0, 620);
    var wink = bump(t, 4380, 190);
    var blink = bump(t, 180, 170);
    translate(art.core, 0, -1.2 * piston - 1.5 * bump(t, 4050, 520));
    rotate(art.lever, -32 * wake);
    rotate(art.antenna, 7 * Math.sin(progress(t, 0, 800) * 3 * Math.PI) * (1 - progress(t, 0, 800)) + 5 * Math.sin(progress(t, 4020, 650) * 3 * Math.PI) * bump(t, 4020, 650));
    attr(art.lamp, 'fill', motor ? '#d6b6fa' : '#ac81e2');
    translate(art.eyes, t < 2600 ? -2 * ease(progress(t, 300, 600)) : 2 * bump(t, 2600, 1700), 0);
    attr(art.left, 'transform', 'scale(1 ' + number(1 - .9 * blink) + ')');
    attr(art.right, 'transform', 'scale(1 ' + number(1 - .9 * Math.max(blink, wink)) + ')');
    art.wheels.forEach(function (wheel) { rotate(wheel, drive * .38); });
    art.treads.forEach(function (tread) { attr(tread, 'stroke-dashoffset', number(-drive * .07)); });
    rotate(art.rotor, drive * .22);
    attr(art.door, 'transform', 'scale(' + number(1 - .78 * piston) + ' 1)');
    translate(art.scan, 0, 5 + 48 * progress(t % 600, 0, 600)); opacity(art.scan, .7 * piston);
    rotate(art.upper, -4 * scan); rotate(art.forearm, 7 * scan); rotate(art.head, -3 * scan);
    opacity(art.beam, .24 * scan); rotate(art.needle, -55 + 110 * ease(progress(t, 700, 3200)));
    translate(art.piston, 0, 9 * piston); translate(art.inlet, 0, -25 * inlet); translate(art.outlet, 0, -23 * outlet);
    opacity(art.sparks, .9 * bump(t, 3940, 440));
    translate(art.sparks, 0, -6 * progress(t, 3940, 440));
    if (t < 450) setPhase('wake', 'All right. Let’s find those icons.');
    else if (t < 1700) setPhase('feed', 'Blank icons, meet the scanner.');
    else if (t < 3000) setPhase('match', 'A match. And another.');
    else if (t < 3950) setPhase('deliver', 'Fresh icons, coming through.');
    else setPhase('done', 'Four familiar faces. Job done.');
  }
  function schedule() {
    var token = generation;
    frame = requestAnimationFrame(function (now) {
      frame = null;
      if (token !== generation || mode !== 'playing') return;
      if (blocked()) { stopAll(); return; }
      elapsed = clamp(now - startTime, 0, DURATION);
      render(elapsed);
      if (elapsed >= DURATION) { stop(true); return; }
      schedule();
    });
  }
  function play() {
    stop(false);
    autoPlayed = true;
    if (blocked()) return;
    // The public API uses the same selected result as the native buttons.
    if (!workshop.classList.contains('resolved')) { document.getElementById('demo-after').click(); return; }
    if (!visibleScene()) return;
    mode = 'playing';
    elapsed = 0;
    phase = '';
    workshop.setAttribute('data-sequence', mode);
    demoState.textContent = 'Illustration in motion';
    render(0);
    startTime = performance.now();
    buttons();
    schedule();
  }
  function pause() {
    if (mode !== 'playing') return;
    elapsed = clamp(performance.now() - startTime, 0, DURATION);
    cancelFrame();
    render(elapsed);
    mode = 'paused';
    workshop.setAttribute('data-sequence', mode);
    demoState.textContent = 'Illustration paused';
    announcement.textContent = 'Machine animation paused. Resume continues from this position.';
    buttons();
  }
  function resume() {
    if (mode !== 'paused' || blocked()) return;
    if (!visibleScene()) { stop(false); return; }
    mode = 'playing';
    workshop.setAttribute('data-sequence', mode);
    demoState.textContent = 'Illustration in motion';
    startTime = performance.now() - elapsed;
    announcement.textContent = 'Machine animation resumed.';
    buttons();
    schedule();
  }
  function applyPreference() {
    enabled = supported && !(reduce && reduce.matches) && preference !== 'off';
    root.setAttribute('data-motion', enabled ? 'on' : 'off');
    toggle.disabled = !supported || !!(reduce && reduce.matches);
    toggle.setAttribute('aria-pressed', String(enabled));
    toggle.setAttribute('aria-label', reduce && reduce.matches ? 'Animations follow your reduced-motion setting' : enabled ? 'Turn animations off' : 'Turn animations on');
    toggle.querySelector('span').textContent = reduce && reduce.matches ? 'Reduced motion' : enabled ? 'Motion on' : 'Motion off';
    replay.disabled = !enabled;
    if (!enabled) stopAll();
  }
  toggle.addEventListener('click', function () {
    autoPlayed = true;
    preference = enabled ? 'off' : 'on';
    try { localStorage.setItem(KEY, preference); } catch (_) { /* The visible control still works. */ }
    applyPreference();
  });
  pauseButton.addEventListener('click', function () { if (mode === 'paused') resume(); else pause(); });
  replay.addEventListener('click', function () { document.getElementById('demo-after').click(); });
  workshop.addEventListener('keefetch:comparison', function (event) {
    autoPlayed = true;
    if (event.detail && event.detail.found) play(); else stop(false);
  });
  // Escape is local to the workshop. It does not hijack navigation elsewhere.
  workshop.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && mode !== 'idle') {
      stop(false);
      announcement.textContent = 'Animation stopped. The selected comparison remains visible.';
    }
  });
  window.addEventListener('storage', function (event) {
    if (event.key !== KEY && event.key !== null) return;
    preference = event.newValue === 'off' || event.newValue === 'on' ? event.newValue : null;
    applyPreference();
  });
  listen(reduce, applyPreference);
  listen(printMedia, function () { if (printMedia.matches) suspendMotion(); else releaseSuspension(); });
  document.addEventListener('visibilitychange', function () { if (document.hidden) suspendMotion(); else releaseSuspension(); });
  window.addEventListener('pagehide', suspendMotion);
  window.addEventListener('pageshow', releaseSuspension);
  window.addEventListener('beforeprint', function () { printing = true; suspendMotion(); });
  window.addEventListener('afterprint', function () { printing = false; releaseSuspension(); });

  // Small, finite details elsewhere on the page. Nothing loops while idle.
  function animate(element, frames, duration, delay, group) {
    if (!element || blocked()) return;
    var effect;
    try { effect = element.animate(frames, {duration: duration, delay: delay || 0, fill: 'none', iterations: 1, easing: 'cubic-bezier(.22,.75,.25,1)'}); }
    catch (_) { return; }
    effects.add(effect);
    if (group) group.add(effect);
    function clean() { effects.delete(effect); if (group) group.delete(effect); }
    effect.addEventListener('finish', clean, {once: true});
    effect.addEventListener('cancel', clean, {once: true});
  }
  var steps = Array.from(document.querySelectorAll('.workflow li'));
  steps.forEach(function (step) {
    var line = document.createElement('span');
    line.className = 'workflow-trace'; line.setAttribute('aria-hidden', 'true'); step.appendChild(line);
  });
  function detail(element) {
    if (element.classList.contains('feature')) {
      animate(element.querySelector('.feature-top > .icon'), [{transform: 'translateY(5px) scale(.9)'}, {transform: 'none'}], 430);
    } else {
      animate(element.querySelector('.workflow-trace'), [{transform: 'scaleX(0)', opacity: 1}, {transform: 'scaleX(1)', opacity: 1, offset: .65}, {transform: 'scaleX(1)', opacity: 0}], 700, steps.indexOf(element) * 100);
    }
  }
  restingPose(false);
  applyPreference();
  workshop.setAttribute('data-sequence', mode);
  controls.hidden = false;
  if (typeof IntersectionObserver === 'function') {
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (entry.target === machine) {
          if (!entry.isIntersecting) { if (mode === 'playing') stop(false); return; }
          if (!autoPlayed && entry.intersectionRatio >= .35) {
            autoPlayed = true;
            if (workshop.classList.contains('resolved')) play();
          }
        } else if (entry.isIntersecting) { detail(entry.target); observer.unobserve(entry.target); }
      });
    }, {threshold: [0, .35]});
    observer.observe(machine);
    document.querySelectorAll('.feature,.workflow li').forEach(function (element) { observer.observe(element); });
  }
  document.querySelectorAll('.wordmark > path').forEach(function (element, i) {
    animate(element, [{transform: 'translateY(7px)', opacity: .7}, {transform: 'none', opacity: 1}], 330, i * 32);
  });
  document.getElementById('preset-controls').addEventListener('click', function (event) {
    if (!event.target.closest('button[data-preset]')) return;
    cancelEffects(presetEffects);
    animate(document.querySelector('.preset-panel:not([hidden])'), [{transform: 'translateY(4px)', opacity: .7}, {transform: 'none', opacity: 1}], 180, 0, presetEffects);
  });
  // Small public control API for integration and tests. No mutable internals or
  // artificial testing clock is exposed to the page.
  window.KeeFetchMachine = Object.freeze({
    play: play, pause: pause, resume: resume, stop: function () { stop(false); },
    getState: function () { return {mode: mode, elapsed: elapsed, duration: DURATION, enabled: enabled,
      phase: phase, delivered: count, framePending: frame !== null, renderedFrames: renderedFrames,
      detailEffects: effects.size}; }
  });
}());
