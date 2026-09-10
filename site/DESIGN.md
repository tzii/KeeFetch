# KeeFetch website design

## Direction

Familiar icons on warm paper, with violet ink. The playful part is the small
recognition moment when blank entries acquire the icons people already know.
Do not add mascots, confetti, drifting decorations or fake product chrome to make
an otherwise ordinary section appear playful.

The homepage persuades through an immediate visual explanation and a real stable
download. The six documentation pages prioritize reading and task completion.
They share the same tokens and controls, with a quieter heading scale.

## Tokens and typography

`assets/css/site.css` is the authority. Light surfaces are `#faf8f4`, `#ffffff`
and `#ece5fb`; ink is `#261d36`, secondary text `#665d72`, and accent `#6034bb`.
The illustration stage is `#ded2f7` and uses full-contrast ink for its disclaimer.
Dark surfaces are `#191520`, `#241e2e` and `#322344`; ink is `#f5efff`, secondary
text `#c0b5ce`, accent `#c4a6ff`, and the illustration stage `#3c2a54`.

Use the existing self-hosted Space Grotesk with system fallbacks. Base text is
16px with 1.65 line height. Headings use weight 600, a maximum 5.65rem display size,
and tracking no tighter than -0.04em. Code alone uses a monospace family.
Keep reading measures near 72 characters. Do not use gradient text or a second
serif display face. Selection, focus, text underlines and scrollbars use the tokens.

## Composition

The hero pairs two short heading lines with an illustrative set of six entries.
Its grid minimums are font-relative so enlarged text can reflow instead of
squeezing the illustration. Narrow layouts stack rather than hide content.

Installation uses an actual ordered sequence, not three interchangeable feature
cards. Profile summaries form a connected comparison, with the default marked
in text and color. The full table is a native disclosure on the homepage and is
always present on the profile page. Scrollable tables and code are keyboard
focusable. Never truncate policy or privacy information to fit a tile.

Buttons use 8px corners; the illustration stage uses 16px. The inner sample-entry
panel has one soft offset shadow. Avoid decorative glass, grid textures and
repeated elevation on every content section.

## Interaction and motion

Nothing enters from an invisible state. The illustration shows its finished
result initially. Before / With KeeFetch are ordinary pressed-state buttons,
not a drag-only comparison or an automatic carousel.

The only authored sequence is the user-triggered icon change, under one second.
Repeated actions cancel earlier timers. Reduced motion shows the selected result
immediately; it must not disable the comparison. System reduction takes precedence
and backgrounding the page completes an in-progress change without repeated work.
One polite live region announces completion, outside the illustrative image role.

Theme follows the system until explicitly overridden. Storage is optional and
other tabs can update stored preferences. Mobile navigation opens inline, Escape
restores the trigger, and the no-JavaScript navigation stays available. Controls
are at least 44px tall. The collapsed-navigation breakpoint is shared with the
JavaScript media query at 1152px; update both together.

## Quality checks

Use `eng/test-site-browser.py` on the built site, not a screenshot mock. It tests
the real `/KeeFetch/` subpath in Chromium, Firefox and WebKit. Inspect light/dark,
mobile/desktop, enlarged text, no-JavaScript, keyboard and forced-color captures.
Keep failure artifacts, axe incomplete findings and observed browser versions.
A green axe run is not a declaration of complete WCAG conformance.

Impeccable's new-work, craft-floor, harden, adapt, audit, polish and delight
references informed this work. Sources: `https://impeccable.style/docs/` and
`https://github.com/pbakaus/impeccable/tree/main/skill/reference`, consulted
2026-09-10. This was reference-guided work; the Impeccable CLI/detector was not run.
