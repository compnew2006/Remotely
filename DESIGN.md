---
name: Remotely
description: A self-hosted remote-support console and browser viewer for technicians who need to reach a machine and act on it.
colors:
  platform-blue: "oklch(0.6723 0.1606 244.9955)"
  platform-blue-deep: "oklch(0.5500 0.1500 245.0000)"
  platform-blue-strong: "oklch(0.5000 0.1300 245.0000)"
  platform-blue-bright: "oklch(0.7500 0.1200 245.0000)"
  platform-blue-line: "oklch(0.6818 0.1584 243.3540)"
  platform-blue-soft-light: "oklch(0.9392 0.0166 250.8453)"
  platform-blue-soft-dark: "oklch(0.1928 0.0331 242.5459)"
  canvas-dark: "oklch(0 0 0)"
  card-dark: "oklch(0.2097 0.0080 274.5332)"
  border-dark: "oklch(0.2674 0.0047 248.0045)"
  input-dark: "oklch(0.3020 0.0288 244.8244)"
  ink-dark: "oklch(0.9328 0.0025 228.7857)"
  canvas-light: "oklch(1.0000 0 0)"
  card-light: "oklch(0.9784 0.0011 197.1387)"
  border-light: "oklch(0.9317 0.0118 231.6594)"
  input-light: "oklch(0.9809 0.0025 228.7836)"
  ink-light: "oklch(0.1884 0.0128 248.5103)"
  muted-light: "oklch(0.5000 0.0200 248.0000)"
  muted-dark: "oklch(0.6500 0.0100 248.0000)"
  destructive: "oklch(0.6188 0.2376 25.7658)"
  destructive-ink: "oklch(0.5600 0.2200 25.0000)"
  status-ok: "oklch(0.6907 0.1554 160.3454)"
  status-ok-ink: "oklch(0.5200 0.1400 160.0000)"
  status-warn: "oklch(0.8214 0.1600 82.5337)"
typography:
  display:
    fontFamily: "\"Open Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "32px"
    fontWeight: 500
    lineHeight: "38.4px"
  headline:
    fontFamily: "\"Open Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "20px"
    fontWeight: 500
    lineHeight: "30px"
  title:
    fontFamily: "\"Open Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "16px"
    fontWeight: 500
    lineHeight: "24px"
  body:
    fontFamily: "\"Open Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: "24px"
  label:
    fontFamily: "\"Open Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "14px"
    fontWeight: 500
    lineHeight: "21px"
  micro-label:
    fontFamily: "\"Open Sans\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "12px"
    fontWeight: 500
    lineHeight: "16px"
    letterSpacing: "0.06em"
rounded:
  control: "1.3rem"
  viewer-panel: "5px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  xxl: "48px"
components:
  button-primary:
    backgroundColor: "{colors.platform-blue-deep}"
    textColor: "{colors.canvas-light}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "8px 16px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.muted-light}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: "8px 16px"
  panel:
    backgroundColor: "{colors.card-light}"
    textColor: "{colors.ink-light}"
    rounded: "{rounded.control}"
    padding: "20px"
  input:
    backgroundColor: "{colors.canvas-light}"
    textColor: "{colors.ink-light}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: "38px"
---

# Design System: Remotely

## Overview

**Creative North Star: "Platform Liveliness"**

Remotely is an operations console first and a brand surface second: an IT technician opens it to reach a machine and act on it, not to browse. The visual world therefore borrows the energy of a social platform — one vivid blue voice on a black-or-white stage, pill-shaped controls, generous rounding — while keeping the density and plainness of a working tool. The liveliness is in the accent, not in the chrome: surfaces stay quiet so the single blue can carry action, selection, and status.

The system is built on one accent family (Twitter blue in OKLCH) plus semantic status hues that are signals, never decoration. Dark and light themes are two composed palettes sharing the same token names — every component rule is written once against `--rm-*` tokens declared in `Server/wwwroot/css/Themes/*.custom.css`, so a theme switch never leaves a surface behind. Depth is tonal, not shadowed: flat surfaces separated by lightness and 1px borders, with blue-tinted shadows reserved for hover elevation.

**Key Characteristics:**
- One chromatic voice: Platform Blue carries every action, link, and live state.
- Pill controls: `1.3rem` radius on buttons, inputs, panels, and cards.
- Flat by default: tonal layers and borders, shadows only on hover.
- Open Sans everywhere; Menlo for code and data.
- Status colors are signals (green = online, yellow = outdated, red = destructive), never decoration.

## Colors

One accent, two stages. Platform Blue (`oklch(0.6723 0.1606 244.9955)`, ≈ `#1DA1F2`) is the only chromatic family in both the console and the viewer. The raw value is 2.94:1 with white text, so text-bearing roles are derived at the same hue: deepened to L 0.55 in the light theme (4.76:1 with white) and brightened to L 0.75+ in the dark theme (8:1 on the card). The tweakcn/shadcn aliases (`--background`, `--primary`, `--card`, …) are kept verbatim in the theme files for reference; the `--rm-*` tokens are the normative painting layer.

### Primary

- **Platform Blue** (`oklch(0.6723 0.1606 244.9955)`): the single accent. Primary buttons, links, the focus ring, the viewer's connect button, the menu toggle, and the "on" state of toggled controls. Raw value is reserved for large non-text surfaces (chart fills, soft backgrounds).
- **Platform Blue Deep** (`oklch(0.5500 0.1500 245.0000)`): the light-theme primary fill and accent text — the same hue deepened until white text passes WCAG AA (4.76:1).
- **Platform Blue Bright** (`oklch(0.7500 0.1200 245.0000)`): the dark-theme accent text — the same hue brightened until it passes on the raised card (8:1).

### Secondary

- **Platform Line** (`oklch(0.6818 0.1584 243.3540)`): focus rings, primary button edges, and hairline emphasis.
- **Platform Soft Light / Dark** (`oklch(0.9392 0.0166 250.8453)` / `oklch(0.1928 0.0331 242.5459)`): the accent at background strength — hover fills, selected pills, striped table rows.

### Tertiary

- **Destructive** (`oklch(0.6188 0.2376 25.7658)`): disconnect, delete, stop. Text-bearing fills deepen to `oklch(0.5600 0.2200 25.0000)` so white text passes AA (5.21:1). The console keeps destructive as a bordered treatment; the viewer's disconnect is filled.
- **Status OK** (`oklch(0.6907 0.1554 160.3454)`): online/healthy dots and borders. Text uses the deepened `oklch(0.5200 0.1400 160.0000)` ink (5:1).
- **Status Warn** (`oklch(0.8214 0.1600 82.5337)`): outdated-agent and warning states; text uses a deepened amber in the light theme.

### Neutral

- **Canvas Dark / Light** (`oklch(0 0 0)` / `oklch(1.0000 0 0)`): page backgrounds.
- **Card Dark / Light** (`oklch(0.2097 0.0080 274.5332)` / `oklch(0.9784 0.0011 197.1387)`): raised panels, device cards, sidebar.
- **Border Dark / Light** (`oklch(0.2674 0.0047 248.0045)` / `oklch(0.9317 0.0118 231.6594)`): 1px dividers and card edges. A stronger step (`oklch(0.3795 0.0220 240.5943)` dark) is used where a border must read against a field.
- **Ink Dark / Light** (`oklch(0.9328 0.0025 228.7857)` / `oklch(0.1884 0.0128 248.5103)`): primary text.
- **Muted Dark / Light** (`oklch(0.6500 0.0100 248.0000)` / `oklch(0.5000 0.0200 248.0000)`): secondary text at AA (5.5:1 / 6:1). Faint labels sit one step lighter.

**The One Voice Rule.** Platform Blue is the only decorative hue. Green, amber, and red appear only as status signals with text or shape reinforcement; a green link would read as "online" and is never used.

## Typography

**Display Font:** Open Sans (with `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`)
**Body Font:** Open Sans (same stack)
**Label/Mono Font:** Menlo, monospace (code, logs, terminal, copy rows)

**Character:** A neutral humanist sans at a restrained scale — weight does the structuring (500 for titles and labels, 400 for body), never size inflation. Tabular numerals carry all data so columns of metrics stay aligned.

### Hierarchy

- **Display** (500, 32px, 38.4px): page titles (`.rm-page-title`). One per page.
- **Headline** (500, 20px, 30px): section and modal titles.
- **Title** (500, 16px, 24px): panel titles, card headers.
- **Body** (400, 16px, 24px): prose and lead text, capped at ~70ch.
- **Label** (500, 14px, 21px): field labels, form groups, tab text.
- **Micro-label** (500, 12px, 16px, 0.06em uppercase): stat labels, table headers, eyebrows.

**The One Sans Rule.** Open Sans is the only text face. Menlo appears only for code, logs, and tabular data — never as a "technical" costume for UI copy.

## Layout

The console is a single centered column: pages cap at `1180px` (`.rm-page`), auth at `480px`, manage flows at `760px`; panels use a 16px grid with 20px internal padding (32px for auth/landing panels). Sections breathe with 48px rhythm; toolbars put the title left and fields right with a 1px underline rule. On narrow screens (≤760px) grids collapse to one column and form actions stretch full-width. The viewer is a fixed full-viewport surface: a centered connection box before connect, then a full-bleed canvas with a top button bar and bottom metrics strip after.

## Elevation & Depth

Flat by default, tonal by design. Surfaces are separated by lightness steps (canvas → card → sunken) and 1px `--rm-border` edges; the resting state carries almost no shadow. Shadows are a blue-tinted hover response, not a resting treatment.

### Shadow Vocabulary

- **Resting** (`0 1px 2px rgba(29,161,242,0.15–0.25)`): panels, cards, toasts at rest.
- **Hover** (`0 4px 16px rgba(29,161,242,0.18–0.28)`): hoverable panels, device cards, dropdowns.
- **Raised** (`0 12px 32px rgba(29,161,242,0.22–0.32)`): modals, auth and landing panels.

**The Flat-By-Default Rule.** Depth appears as a response to state (hover, modal, focus). A resting panel with a soft shadow is drift; a resting panel with a border is the system.

## Shapes

Everything interactive is a pill: `--rm-radius-control` is `1.3rem` on buttons, inputs, panels, cards, dropdowns, and badges — at 38px control height that reads as a full pill, which is the Twitter silhouette the theme intends. The viewer keeps its older, tighter 5px panel radius (it is a separate surface with its own CSS). Borders are 1px and always tokened; there are no hard offset shadows and no colored left/right accent bars on cards or alerts.

**The Pill Rule.** If it is a control or a container in the console, it is `1.3rem`. Small radii survive only in the viewer's legacy chrome.

## Components

### Buttons

- **Shape:** full pill (`1.3rem` radius), 38px default height, 500 weight.
- **Primary:** Platform Blue fill. Light theme: deepened fill with white text (4.76:1). Dark theme: raw blue fill with dark ink (7.15:1 — white on raw blue is 2.94:1 and never ships). Hover darkens or lifts; active presses 1px down.
- **Ghost / Secondary:** transparent fill, muted text, 1px border; hover fills with Platform Soft and shifts the edge to Platform Line.
- **Danger:** bordered destructive in the console; filled (deepened red, white text) in the viewer for disconnect and in `btn-danger`.

### Cards / Panels

- **Corner Style:** pill (`1.3rem`), overflow hidden for headers.
- **Background:** Card token on Canvas; Sunken for wells (logs, code, terminal).
- **Shadow Strategy:** resting shadow at rest, hover shadow + 1px lift on interactive cards.
- **Border:** 1px `--rm-border`; hover shifts to `--rm-accent-line`.
- **Internal Padding:** 20px panels, 32px auth/landing, 12–16px card headers.

### Inputs / Fields

- **Style:** pill radius, 38px height, 12px horizontal padding. Bootstrap paints `.form-control` fields white in both themes — tokens describe that white well (`--rm-field-bg` = white, dark ink, dark placeholder).
- **Focus:** border shifts to Platform Line with a soft blue ring (`0 0 0 3px` focus-ring token).
- **Floating labels:** the label IS the placeholder — placeholder text stays transparent until focus (`.form-floating > .form-control::placeholder`), otherwise label and placeholder overlap. This rule is `!important` because the generic placeholder rule is.
- **Error / Disabled:** error = destructive outline + message in danger ink; disabled = sunken fill with faint ink.

### Navigation

- **Style:** ghost rows on the sidebar; text links in the console body.
- **Default / Hover / Active:** muted text at rest; hover fills Platform Soft; active is a raised pill with accent-strong text and a border. Tabs use the same language with an underline rule.

### Badges / Chips

- **Style:** pill, 500 weight, tabular numerals. Primary/info badges use Platform Soft fill with accent-strong text and a Platform Line border — never a solid theme-blue wash.
- **Status badges:** success/warning use transparent fills with status-ink text and a matching status border; the dot or glyph carries meaning with the text, never color alone.

### Device Cards

- **Style:** raised card, pill radius, name + online dot in the header, two-column field grid in the body with field names in accent ink.
- **State:** hover lifts with Platform Line edge; expanded raises the shadow. Online = green dot + title text, not color alone.

## Do's and Don'ts

### Do:

- **Do** paint every console color from a `--rm-*` token declared in `Server/wwwroot/css/Themes/*.custom.css` — a raw hex in a component rule will not survive a theme switch.
- **Do** keep both themes declaring the same token names; a missing token silently keeps old behavior in one theme.
- **Do** derive text colors at AA: body ≥ 4.5:1, controls and focus ≥ 3:1 — verify computed pairs, not eyeballs.
- **Do** let Platform Blue own action, selection, focus, and the connect affordance — and nothing decorative.
- **Do** pair every status color with text, a dot, or a glyph.
- **Do** use pill radius (`1.3rem`) for console controls and containers; keep tabular numerals for data.

### Don't:

- **Don't** introduce a second accent hue (no cyan, no slate blue, no generic Bootstrap palette escapes) — Bootstrap classes must route through the `--bs-*` bridges in the custom theme files.
- **Don't** put white text on raw Platform Blue (`#1DA1F2` is 2.94:1) — deepen it in light, use dark ink in dark.
- **Don't** use green for links or interactive text — green means online.
- **Don't** add resting drop shadows for depth — use tonal steps and borders; shadows are hover/modal only.
- **Don't** show a placeholder and a floating label at the same time — the placeholder stays transparent until focus.
- **Don't** regenerate the enforced brand assets (logos, banners, icons) — they are authoritative inputs, not design output.
