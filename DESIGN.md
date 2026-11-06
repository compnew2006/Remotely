---
name: Remotely
description: A self-hosted remote-support console and browser viewer for technicians who need to reach a machine and act on it.
colors:
  console-canvas: "#222222"
  console-surface: "#303030"
  console-surface-raised: "#444444"
  console-sidebar: "#222222"
  console-ink: "#ffffff"
  console-ink-muted: "rgba(255, 255, 255, 0.75)"
  console-ink-faint: "rgba(255, 255, 255, 0.60)"
  console-control-ink: "#adb5bd"
  console-danger: "#e74c3c"
  console-primary: "#375a7f"
  console-primary-deep: "#162433"
  console-primary-edge: "rgba(143, 184, 224, 0.55)"
  console-accent: "#8fb8e0"
  console-accent-strong: "#b3d2f0"
  console-accent-soft: "rgba(143, 184, 224, 0.12)"
  console-accent-line: "rgba(143, 184, 224, 0.55)"
  console-focus-ring: "rgba(143, 184, 224, 0.45)"
  console-status-ok: "#00bc8c"
  console-nav-active: "rgba(255, 255, 255, 0.25)"
  console-navbar-scrim: "rgba(0, 0, 0, 0.3)"
  console-field-bg: "#ffffff"
  console-field-ink: "#303030"
  console-field-border: "#222222"
  light-canvas: "#ffffff"
  light-ink: "#222222"
  light-primary: "#008cba"
  light-control: "#017ea7"
  light-control-hover: "#7dc7e0"
  light-accent: "#0b6a94"
  light-accent-strong: "#08506f"
  light-accent-soft: "rgba(11, 106, 148, 0.10)"
  light-accent-line: "rgba(11, 106, 148, 0.45)"
  light-sidebar: "#f8f9fa"
  light-status-ok: "#00bc8c"
  viewer-canvas: "#151718"
  viewer-panel: "#212526"
  viewer-panel-toggled: "#16191a"
  viewer-field: "#0d0d0d"
  viewer-field-border: "#323334"
  viewer-ink: "#ffffff"
  viewer-ink-inverse: "#000000"
  signal-cyan: "#01e9d1"
  signal-cyan-bright: "#33ffe9"
  signal-cyan-deep: "#009888"
  signal-cyan-press: "#01ad9b"
  signal-cyan-toggled: "#07b5a4"
  viewer-hover-border: "#384346"
  viewer-overlay: "rgb(60, 60, 60)"
  viewer-scrim: "rgba(0, 0, 0, 0.5)"
  viewer-disconnect: "#a61930"
  viewer-status: "#00bfff"
  viewer-dot: "#fafafa"
  beta-pill-bg: "rgb(40, 35, 21)"
  beta-pill-ink: "rgb(245, 222, 137)"
typography:
  display:
    fontFamily: "Lato, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "32px"
    fontWeight: 500
    lineHeight: "38.4px"
  headline:
    fontFamily: "Lato, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "20px"
    fontWeight: 500
    lineHeight: "30px"
  title:
    fontFamily: "Lato, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: "24px"
  body:
    fontFamily: "Lato, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: "24px"
  label:
    fontFamily: "Lato, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "14.4px"
    fontWeight: 400
    lineHeight: "21.6px"
    letterSpacing: "normal"
  micro-label:
    fontFamily: "Lato, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "12px"
    fontWeight: 500
    lineHeight: "16px"
    letterSpacing: "0.06em"
rounded:
  console-control: "6px"
  console-control-sm: "6px"
  viewer-panel: "5px"
  viewer-control: "0px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  xxl: "48px"
components:
  console-button-primary:
    backgroundColor: "{colors.console-primary}"
    borderColor: "{colors.console-primary-edge}"
    textColor: "{colors.console-ink}"
    typography: "{typography.label}"
    rounded: "{rounded.console-control-sm}"
    padding: "4px 8px"
  console-toolbar:
    backgroundColor: "transparent"
    borderBottom: "1px solid {colors.console-surface-raised}"
    typography: "{typography.micro-label}"
    gap: "16px 32px"
    controlHeight: "38px"
  console-button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.console-control-ink}"
    typography: "{typography.body}"
    rounded: "{rounded.console-control}"
    padding: "6px 12px"
  console-card:
    backgroundColor: "{colors.console-surface}"
    textColor: "{colors.console-ink}"
    typography: "{typography.body}"
    rounded: "{rounded.console-control}"
  console-field:
    backgroundColor: "{colors.console-field-bg}"
    textColor: "{colors.console-field-ink}"
    typography: "{typography.body}"
    rounded: "{rounded.console-control}"
    padding: "6px 12px"
  console-nav-link:
    textColor: "{colors.console-ink}"
    typography: "{typography.label}"
    rounded: "{rounded.console-control-sm}"
    padding: "8px 32px"
  viewer-button:
    backgroundColor: "{colors.viewer-panel}"
    textColor: "{colors.viewer-ink}"
    rounded: "{rounded.viewer-control}"
    height: "40px"
    padding: "1px 6px"
  viewer-button-toggled:
    backgroundColor: "{colors.viewer-panel-toggled}"
    textColor: "{colors.signal-cyan-toggled}"
    rounded: "{rounded.viewer-control}"
    height: "40px"
  viewer-button-primary:
    backgroundColor: "{colors.signal-cyan}"
    textColor: "{colors.viewer-ink-inverse}"
    typography: "{typography.label}"
    rounded: "{rounded.viewer-control}"
    height: "40px"
  viewer-field:
    backgroundColor: "{colors.viewer-field}"
    textColor: "{colors.viewer-ink}"
    rounded: "{rounded.viewer-control}"
    height: "40px"
    padding: "1px 14px"
  viewer-disconnect:
    backgroundColor: "{colors.viewer-disconnect}"
    textColor: "{colors.viewer-ink}"
    rounded: "{rounded.viewer-control}"
    height: "40px"
---

# Design System: Remotely

## Overview

**Creative North Star: "The Operations Room"**

Remotely is a room with one bright thing in it. A technician opens the console, finds the machine that is in trouble, and opens it; from that moment the remote screen is the only object in the room that matters, and every piece of interface around it is instrumentation — a switch, a readout, a way out. The system is built around that one relationship, and it shows in the code: the viewer gives the remote canvas the entire viewport (`#workAreaGrid` is a fixed inset-0 grid with `grid-template-rows: auto 1fr`), while the console stays a quiet directory of devices waiting to be chosen.

Two surfaces, one product, two deliberately different manners. The **operator console** is a Bootstrap 5.3.2 application wearing a Bootswatch theme (Darkly for dark, Yeti for light) — soft 4–6px radii, tonal grey layering, `Lato`. The **remote viewer** is hand-written CSS with no framework at all — square 0px controls, hard 1px borders, `Lucida Sans`, and a single electric accent that only ever appears on the connect and toggle affordances. The console is furniture; the viewer is an instrument. The difference is not drift — it is the difference between a waiting room and the console of a machine.

The governing personality is **strictly functional: every element serves the session**. Nothing in either surface is decorative. The console has no shadows, no gradients, no illustrations — depth is carried entirely by three greys (`#222` canvas → `#303030` card → `#444` border) and by 1px borders. The viewer has exactly one shadow in the entire system, on the one element that genuinely floats (`.modal-prompt`). Color is used as signal, never as mood: the console's slate blue marks the primary action, the viewer's Signal Cyan marks the one thing you can act on, and crimson appears once, on the button that ends the session. Where the system is incomplete, it says so plainly — the viewer's light theme is a `TODO` in `Server/Pages/Viewer.cshtml.cs:42` — and where the console drifted, it was corrected rather than excused: `site.css` no longer declares a font or a link colour that the theme overrides, the sidebar's `steelblue` gradient is gone, and every console colour now comes from one token layer (`--rm-*`) shared by both themes.

**Key Characteristics:**

- The remote screen owns the viewport; all chrome floats over it as a thin, square-edged overlay.
- Two type systems with different jobs: `Lato` for the console's readability, `Lucida Sans` for the viewer's deliberate plainness.
- Zero decorative color, zero ornament, zero resting shadows.
- One accent per surface, reserved for action: Signal Cyan in the viewer, slate blue in the console.
- Radii are a vocabulary, not a gradient: a single 6px in the console, 5px on floating panels, 0px on viewer controls.

## Colors

A two-tone system: a near-black neutral scale that carries the whole product, and exactly two chromatic accents — slate blue for the console's primary action and Signal Cyan for the viewer's live affordance — plus one crimson reserved for ending a session.

### Primary

- **Signal Cyan** (`#01e9d1`): the viewer's single accent. It marks the connect button, the menu toggle, and the "on" state of a toggled control. Nothing else in the viewer is allowed to be cyan, and the console does not use it at all.
- **Slate Blue** (`#375a7f`): the console's primary action color (`.btn-primary`, Darkly's `--bs-primary`). Cool, desaturated, and low-contrast against `#222` on purpose — a primary button in this product is a quiet label, not a beacon. Its dark variant `#162433` carries pressed/emphasis text. Because the fill is deliberately quiet, the button carries a 1px `--rm-primary-edge` hairline instead of a louder colour: `#375a7f` on `#222` is 2.22:1, and the edge raises the button's boundary to **3.33:1**, which is what WCAG asks of a control edge.
- **Console Accent** (`#8fb8e0` dark, `#0b6a94` light): the console's single chromatic family, declared once per theme as `--rm-accent`. It carries links, informational text, the focus ring, the active navigation item, and the primary button's edge. Every one of those is an *action or a state*; nothing decorative may take it. The light theme darkens the same accent to `#0b6a94` because Yeti's `#008cba` on white is only 3.84:1.

### Secondary

- **Cyan Hover** (`#33ffe9`): the Signal Cyan hover state for `#menuButton`.
- **Cyan Press** (`#009888`): the Signal Cyan active/border state, reused for `:active` on buttons.
- **Toggled Cyan** (`#07b5a4`): the text color of a control in its "on" state, paired with the near-black Toggled Panel (`#16191a`) so an engaged control reads as *pressed in*, not *lit up*.

### Tertiary

- **Disconnect Crimson** (`#a61930`): the `#disconnectButton` in the viewer. The only destructive color in the product, and it appears once per screen.
- **Danger Red** (`#e74c3c`): the console's destructive action — `btn-outline-danger` in the remote task-manager modal. The light theme substitutes `#f04124`. The console's outline-danger treatment is a **bordered** destructive button, deliberately weaker than the viewer's filled crimson, because it acts on a process row rather than ending the session.

### Neutral

- **Operations Canvas** (`#151718`): the viewer's page background, with a 40×40px radial-gradient dot lattice (`#fafafa` at 1%) over it and a black gradient layer beneath.
- **Console Canvas** (`#222222`): the console's body background (Darkly `--bs-body-bg`).
- **Panel Grey** (`#212526`): the viewer's chrome surfaces — menu frame, popup menus, buttons.
- **Card Surface** (`#303030`): the console's card and device-card background (Darkly `--bs-card-bg`).
- **Divider Grey** (`#444444`): console card borders and card headers (`--bs-card-cap-bg`).
- **Field Black** (`#0d0d0d`): the viewer's input and select background — a hole punched in the panel.
- **Field Border** (`#323334`): the viewer's input border; `#384346` is its hover state.
- **Overlay Grey** (`rgb(60, 60, 60)`): the viewer's floating modal prompt.
- **Scrim Black** (`rgba(0, 0, 0, 0.5)`): the viewer's session-metrics readout, floating over the screen.
- **Pure White** (`#ffffff`): all primary ink on both surfaces; console muted ink is `rgba(255, 255, 255, 0.75)`.
- **Control Grey** (`#adb5bd`): the console's secondary control ink (`.btn` text).
- **Signal Blue** (`#00bfff`): the viewer's status-message color (deepskyblue) — reserved for connection status, never for buttons.
- **Beta Pill** (`rgb(40, 35, 21)` on `rgb(245, 222, 137)`): the only warm pair in the product, marking an experimental feature.

### Named Rules

**The Signal Cyan Rule.** Signal Cyan is reserved for the connect and toggle affordances in the viewer. It is never used for decoration, for data, for hover states on other elements, or anywhere in the console. Its rarity is the point.

**The Three-Grey Rule.** The console's depth comes from exactly three greys — `#222`, `#303030`, `#444` — plus a 1px border. Introducing a fourth surface tone is a change to the system, not a style choice.

**The Cascade Rule.** The theme stylesheet is loaded *after* `site.css`, so any declaration in `site.css` that the theme also sets is dead. The file used to declare `font-family: 'Helvetica Neue'` on `html, body` and `a { color: #0366d6 }` while the rendered console used **Lato** and theme-coloured links; both declarations are now **deleted** rather than left to be overridden. Verify against computed styles in the running app, never against the source file.

**The One-Hue Rule.** The console has one chromatic family — slate blue — plus status colours. Green means *online* and nothing else, so links are the accent, never green: a green link reads as a status. Yellow means *outdated agent*, red means *destructive*. A new colour must be a signal or it does not ship.

**The Token Rule.** Every colour the console paints comes from a `--rm-*` token declared in `Server/wwwroot/css/Themes/*.custom.css` (dark: `darkly.custom.css`, light: `yeti.custom.css`). A raw hex value in a component rule is drift waiting to happen: it will not survive a theme switch. The two themes must declare the same token names, or one of the two surfaces silently keeps the old behaviour.

## Typography

**Display Font:** `Lato` (with `-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif`) — console
**Body Font:** `Lato` — console; `Lucida Sans, 'Lucida Sans Regular', 'Lucida Grande', 'Lucida Sans Unicode', Geneva, Verdana, sans-serif` — viewer
**Label/Mono Font:** `'Segoe UI', Tahoma, Geneva, Verdana, sans-serif` for the viewer's connect button; `monospace` for menu section headers (`.menu-options-header`) and the console's console prompt/output.

**Character:** Lato is a neutral humanist sans — friendly enough for a dashboard a technician scans daily, quiet enough to never compete with status text. The viewer's `Lucida Sans` is deliberately plain and slightly old-fashioned: on a screen that shows someone else's desktop, the *only* type worth reading is the type inside the remote screen, so the surrounding interface uses the most unremarkable face available. The one exception is `#connectButton`, which is uppercase, letter-spaced `0.6px`, `Segoe UI` 700 at 12px — a machine label for the one irreversible-feeling action in the viewer.

### Hierarchy

Console (Lato, measured from the running app):

- **Display** (500, 32px/38.4px): the page-level heading — "Devices". One per page, never more.
- **Headline** (500, 20px/30px): the `h5` labels inside device cards ("User", "Platform", "Memory", "CPU", "Disk"). Weight 500, not bold: these are field names, not headings.
- **Title** (400, 16px/24px): device names, card values, body copy. The default text size of the console.
- **Body** (400, 16px/24px): everything that is not a label — values, descriptions, dropdown items.
- **Label** (400, 14.4px/21.6px, normal tracking): navigation links, muted metadata, form labels, and small buttons (`btn-sm` at 14px/21px).
- **Micro-label** (500, 12px/16px, `0.06em`, uppercase): the name of a field in the device toolbar ("Device Group", "Sort", "Search", "Page", "Per page"). It is the console's only uppercase, because a field name is a category, not a sentence.

Viewer (a separate, smaller scale — Lucida Sans):

- **Connect Header** (100, 28px/32px): `.connect-header`, the page's one heading, hairline weight.
- **Status Message** (400, 17.5px/26.25px): `.status-message` — connection state, in Signal Blue.
- **Control Label** (400, 16px/16px): `.input-label`.
- **Control Text** (400, 14px/16px, weight 300): input and select text.
- **Machine Label** (700, 12px/14px, `0.6px`, uppercase): `#connectButton`.
- **Micro** (400, 12px/14px): `.copyright-text`; the metrics readout is smaller still at 11.2px/16.8px.

### Named Rules

**The Two-Stacks Rule.** The console and the viewer keep separate type systems. Moving `Lato` into the viewer, or `Lucida Sans` into the console, merges two surfaces that are deliberately unlike each other.

**The Weight-500 Rule.** In the console, hierarchy is carried by size and by weight 500 — never by bold 700, never by color alone. Weight 300 and 400 are the default in the viewer.

## Layout

The console is a fixed-topbar application: a Bootstrap navbar pinned to the top (`background: rgba(0, 0, 0, 0.3)` over the `#222` canvas, `padding: 0 0 4px 24px`), a vertical navigation list on the left, and page content pushed down by `body { padding-top: 50px; padding-bottom: 20px }` with `padding-left/right: 15px` on `.body-content`. Devices are laid out as a wrapping grid of `.device-card` blocks (`margin: 1rem 1rem 0.5rem 0`), each a Bootstrap card that expands on click into a two-column detail grid with a 4-column span for the device metadata block. Spacing is Bootstrap's scale: 4 / 8 / 16 / 24 / 32 / 48px. The console is desktop-first; there is no mobile layout for the device grid.

The device toolbar (`#deviceControlsWrapper`) is one bar with two groups — filters on the left (Device Group, Sort, Hide offline devices, Select All, Refresh) and view controls on the right (Search, Page, Per page) — held apart by `justify-content: space-between` and closed by a 1px `#444` hairline. Every field is a micro-label above a 38px control, so the labels share one baseline and the controls share another; the gaps are 16px inside a group and 32px between groups, and the whole bar wraps rather than overflow. The device grid below it (`#deviceListDiv`) is a wrapping flex row with a 16px gap and no card margins, so the first card shares the toolbar's left edge exactly instead of sitting 8px to its right.

The viewer is a stack of fixed, full-viewport layers rather than a document flow:

| Layer | Positioning |
|---|---|
| `#workAreaGrid` | fixed, inset 0, `grid-template-rows: auto 1fr`, `row-gap: 5px`, z-index 2 |
| `#screenViewerWrapper` | relative, full width/height, `overflow: auto`, holds the scroll/pan |
| `#screenViewer` | absolute; `.fit` variant caps it at `max-width/height: 99%` and centers it with `translateX(-50%)` |
| `#menuButton` | fixed top-left, 50×30px, z-index 3, rotates its icon 180° when open |
| `.horizontal-button-bar` | fixed, `top: 10px`, horizontally centered, `border-radius: 5px`, collapses to `height: 0` until opened, z-index 3 |
| `#menuFrame` | fixed, `grid-template-columns: 1fr 2fr 1fr` (logo · actions · right), `column-gap: 10px`, expands to `height: auto` when open, z-index 3 |
| `#metricsFrame` | fixed bottom-right, `background: rgba(0,0,0,0.5)`, `pointer-events: none`, z-index 5 |
| `#toastsWrapper` | fixed, 25px from the bottom-right, z-index 4 |
| `footer` | fixed, `bottom: 40px`, full width, centered copyright |
| `#fileTransferDiv` | fixed, 5px from the bottom-right, `rgb(50,50,50)`, z-index 3 |

The viewer's single breakpoint is `@media (max-width: 667px)`, where `#menuFrame` becomes `0 2fr 1fr` — the logo column is dropped because there is no room for it.

The console overlays one additional layer: a Bootstrap modal, opened through `ModalService` and hosted by `ModalHarness.razor`. Modal contents are scrolled regions with their own internal height cap (`max-height: 55vh` in the task manager), so a dense table never pushes the page behind it out of reach.

## Elevation & Depth

**This system is flat at rest.** The console uses no shadow at all: Darkly sets `--bs-card-box-shadow` empty, and cards are separated from the `#222` canvas by a `1px solid #444` border plus a one-step tonal jump to `#303030`. The viewer is flatter still — buttons sit on `#212526` with a `1px solid #212526` border, and their only depth cue is the border brightening to `#384346` on hover. Depth is carried by **tonal layering and 1px borders**, never by a shadow.

The single exception is the one element that genuinely floats above a live screen: `.modal-prompt` uses `box-shadow: 10px 10px 5px rgba(50, 50, 50, 0.9)` — a hard, offset, almost unblurred shadow that reads as a physical card thrown onto the picture. (`#blazor-error-ui` adds `0 -1px 2px rgba(0,0,0,0.2)` as a bottom-anchored error strip.)

### Shadow Vocabulary

- **Floating Prompt** (`box-shadow: 10px 10px 5px rgba(50, 50, 50, 0.9)`): the viewer's modal prompt only.
- **Error Strip** (`box-shadow: 0 -1px 2px rgba(0, 0, 0, 0.2)`): the Blazor error bar only.

### Named Rules

**The Flat-By-Default Rule.** Surfaces are flat at rest. If a new element needs to be distinguished from its neighbors, give it a different tone or a 1px border. A shadow is a last resort and is reserved for something that physically floats over the remote screen.

## Shapes

Radii are a closed vocabulary, and each surface keeps to its own:

| Surface | Element | Radius |
|---|---|---|
| Console | card, device card, `.form-control`, `.btn`, `.btn-sm`, `.nav-link` | **6px** — one radius |
| Viewer | buttons, inputs, selects | **0px** — square |
| Viewer | `.horizontal-button-bar`, `.popup-menu`, `.modal-prompt`, `.beta-pill` | 5px |

Borders are `1px solid` everywhere, always in the surface's own border color. There are no pills, no circles, no angled or clipped geometry in either surface. The only shape that is not a rectangle is the 40×40px radial-gradient dot lattice on the viewer's background, which is a texture rather than an element.

**The One Vocabulary Rule.** Never mix radii within a surface. The console has exactly one radius, 6px, enforced by overriding `--bs-border-radius`, `--bs-border-radius-sm` and `--bs-border-radius-lg` in both themes — a 6px button next to a 4px button in the console is a bug, and so is a rounded button in the viewer.

## Components

### Buttons

**Console** — soft, quiet, Bootstrap-native.

- **Shape:** 6px radius (`4px` at `btn-sm`).
- **Primary:** `background: #375a7f`, `1px solid rgba(143, 184, 224, 0.55)` border, white text, `padding: 6px 12px` (`4px 8px` for `btn-sm`). The fill stays quiet; the accent hairline is what makes the button findable against `#222`.
- **Hover / Focus:** Bootstrap's standard darkening and focus ring; the dark theme supplies it, so do not hand-roll transitions.
- **Ghost / Secondary:** transparent background, `#adb5bd` text, 6px radius — used for nav actions such as "Account" and "Logout".

**Viewer** — square, flat, instrument-like.

- **Shape:** 0px radius, `height: 40px` (`#menuButton` is 50×30px), `1px solid #212526` border.
- **Default:** `background: #212526`, white text, `padding: 1px 6px`.
- **Hover / Focus:** the border brightens to `#384346`; nothing else changes.
- **Toggled:** `background: #16191a` with `#07b5a4` text — engaged controls read as *pressed in*.
- **Primary (`#connectButton`):** Signal Cyan `#01EAD1` with black text, `Segoe UI` 700 / 12px / `0.6px` / uppercase, full width.
- **Destructive (`#disconnectButton`):** `#a61930` with white text, 16px.

### Cards / Containers

- **Corner Style:** 6px (console), 5px (viewer floating panels), 0px (viewer controls).
- **Background:** `#303030` console card; `#212526` viewer panel.
- **Shadow Strategy:** none — see Elevation & Depth.
- **Border:** console `1px solid #444444`; viewer `1px solid #212526` (brightening to `#384346` on hover).
- **Internal Padding:** console `1rem` (Bootstrap card spacer); viewer panels `10px`; the viewer's connect box `30px` inside a 350px column.

### Inputs / Fields

- **Style:** console `.form-control` — white background, `#303030` text, 6px radius, `1px solid #222` border, `padding: 6px 12px`. Viewer inputs — `#0d0d0d` background, `1px solid #323334` border, 0px radius, `height: 40px`, `padding: 1px 14px`, `font-weight: 300`.
- **Focus:** viewer inputs turn their border white; console uses Bootstrap's ring.
- **Disabled / Read-only:** console disabled and readonly inputs are forced to `#adadad` with `!important`; the viewer's disabled connect button is `#152826` with Signal Cyan text and `cursor: no-drop`.

### Navigation

Console: a fixed Bootstrap navbar with a 24px brand (`navbar-brand` at `1.5rem` in both themes) over a `--rm-navbar-scrim`, and a vertical list of `nav-link` items at `14.4px`, `padding: 8px 32px`, 6px radius, in `--rm-ink-muted`; the active item is filled with `--rm-accent-soft` and set in `--rm-accent-strong`, so "where am I" reads as an accent state rather than a white wash. The rail itself is the flat `--rm-surface-sidebar` with a 1px right border — no gradient. Viewer: no navigation — a single menu button opens `#menuFrame`, whose three columns place the logo left, the actions center, and secondary tools right.

### Device Card (signature component)

The console's unit of meaning: a Bootstrap card whose header carries an online/offline glyph (`oi-power-standby` in `text-success` when online), the device name, and a selection checkbox; the body is a two-column grid of `label`-value pairs ("User / Administrator", "Platform / Windows", "Disk / 23 %", "Memory / 46 %", "CPU / 7 %"). The field names are painted in `--rm-accent` (`#8fb8e0`) rather than Bootstrap's `text-info`: the blue they used to be (`#3498db`) was a second hue that also failed AA on the `#303030` card at 4.19:1, and the accent measures 6.35:1 there. Clicking the card expands it into the Actions dropdown - Chat, Remote Control, View Only, Task Manager, Screenshots, Camera, Wake, Upload File, Uninstall - and the header exposes an "open in new tab" button. Task Manager, Screenshots and Camera open console modals, not new pages. The card is a status readout first and a launcher second.

### Console Data Table (signature component)

Added with the remote task manager (`11b855fb`): `table table-sm table-striped table-hover` with a `thead.table-light` header row, `small text-nowrap` for timestamps, `text-truncate` with a `max-width` for window titles, and a scroller capped at `max-height: 55vh`. Destructive per-row actions are `btn btn-sm btn-outline-danger py-0` — a 0 vertical padding button so the row height stays table-tight. The filter row above it is a `row`/`col-sm-6` pair holding a `form-control form-control-sm` and a `btn btn-sm btn-primary`. This is the console's vocabulary for dense tabular data: Bootstrap's `table-sm` scale, not a custom table.

### Inline Alert (console)

`alert alert-warning py-2` — the console's only inline message treatment, used for recoverable failures (a failed remote refresh, an unreachable device). Warning yellow, not red: the failure is recoverable and the operator can retry.

### Modal (console)

Console modals are Bootstrap modals driven by `ModalHarness.razor` + `ModalService`; contents live in `Server/Components/ModalContents/*.razor`. A modal is a Bootstrap surface - it introduces no new visual vocabulary, and nothing inside it may break the flat rule. Two console modals are built on it: the task manager (a process table with a destructive action per row) and the screenshots view (a settings block, a live capture, and a gallery of stored captures with per-item deletion). Both are read-mostly surfaces: any destructive action is a single explicit button, never a hover action, and a capture that is deliberately paused says so instead of silently doing nothing.

### Viewer Chrome (signature component)

The overlay system around the remote canvas: a square `#menuButton` that rotates 180° when open, `.horizontal-button-bar` for the top action row, `#menuFrame` for the three-column menu, `#metricsFrame` for the FPS/throughput/latency readout, `.toast-message` notifications that slide out to the right on a 2s delay with a 0.5s ease-in animation, `.beta-pill` for experimental features, and the `.modal-prompt` for typed input. Every one of these is square-edged, dark, and pointer-transparent where it must not steal input from the screen beneath.

## Do's and Don'ts

### Do:

- **Do** let the remote screen occupy the whole viewport in the viewer, and float the chrome above it.
- **Do** keep the console's depth in the three greys (`#222` / `#303030` / `#444`) and 1px borders.
- **Do** take every console colour from a `--rm-*` token, and declare the same token in both themes.
- **Do** reserve Signal Cyan for the connect and toggle affordances, and slate blue `#375a7f` for the console's primary action.
- **Do** verify a change against computed styles in the running app — the theme stylesheet loads after `site.css` and overrides it.
- **Do** keep every control in the viewer at 40px height so the target size stays constant across the toolbar.
- **Do** use Bootstrap's dense vocabulary (`table-sm`, `striped`, `hover`, `thead.table-light`, `btn-sm`) for tabular console data instead of inventing a table style.
- **Do** use `Lato` in the console and `Lucida Sans` in the viewer, and not the other way round.

### Don't:

- **Don't** add a shadow to a resting surface. The only shadow belongs to the floating prompt.
- **Don't** introduce a second accent color in either surface, and don't carry Signal Cyan into the console.
- **Don't** use green for anything but "online" — a green link or a green heading reads as a status, not an action.
- **Don't** round a viewer control, mix two radii in the console, or soften a square edge.
- **Don't** decorate, illustrate, or add an empty state that isn't about a device or a session.
- **Don't** declare a colour in `site.css` that the theme also sets — it is dead code by construction; the console's colours belong to the `--rm-*` tokens.
- **Don't** assume the viewer has a light theme; `Server/Pages/Viewer.cshtml.cs:42` hard-codes dark with a `TODO`.
