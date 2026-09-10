# Icon studio

## Direction

A small utility deserves care without pretending to be a larger product. The homepage is an icon specimen tray on warm paper: recognizable local website marks, a reversible before/after and a violet accent borrowed from the existing KeeFetch artwork. Documentation is quieter, with clear section boundaries and a reading index instead of decorative panels around every paragraph.

## Decisions

- Warm paper `#faf7f0`, ink `#282132`, violet `#6939b9`; dark paper `#191721`, pale ink `#f5f0e9`, lavender `#c4a6ff`. Component colors live in shared tokens. Mint distinguishes the privacy section.
- Keep the existing local, licensed Space Grotesk font deliberately. Continuity and avoiding another font request matter more than changing a typeface to pass a generic overused-font rule. Body text remains ordinary mixed case.
- Large, tightly set display text contrasts with compact specimen labels. Guides use a smaller heading scale and a comfortable line height. Long text must reflow at 200% enlargement.
- Hard-edged button shadows and slightly rotated icon specimens provide playfulness. Do not add looping confetti, scroll-jacking, gradients or delayed content reveals.
- The comparison has two real buttons with pressed state and a polite status announcement. It changes instantly; a short icon-arrival animation is decoration only. Reduced motion must never remove functionality.
- A static header avoids obscuring headings at zoom. Theme, motion, menu and demo controls are at least 44 by 44 CSS pixels. Toolbar symbols stay inside their buttons when text is enlarged. The reading index becomes ordinary wrapped links on a narrow screen.
- Tables scroll within their own labeled, keyboard-focusable region. The document must not scroll sideways. Code examples wrap without concealing their text.

## Impeccable passes

Guidance read and applied from impeccable.style and pbakaus/impeccable: craft floor, audit, delight, adapt, harden, clarify and polish. The pinned Impeccable 4.1.0 source detector runs separately in Website quality. Source findings are design review evidence, not proof of accessibility or a substitute for rendered checks.

Audit priorities: contrast and focus, mobile reading and enlarged text, optional storage and data failures, truthful preview/stable copy, no third-party demo requests. Runtime browser tests and screenshots determine whether the implementation passes those cases.

## Review boundary

Automated Chromium, Firefox and WebKit runs are not physical iOS/Android testing, screen-reader testing or native KeePass validation. Do not convert a passing browser suite into those claims. Record exact executed results in the review note.
