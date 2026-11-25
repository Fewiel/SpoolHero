# SpoolHero design system

The UI is built on Bootstrap 5.3 (CDN) + Bootstrap Icons, re-themed in `src/SpoolManager.Client/wwwroot/css/app.css`.
Neutral surfaces let the filament colours stand out; there is a single accent colour. Every page must look right in
**light and dark** mode and at **phone width (390 px)**.

## Principles

- **The spool is the hero.** Show spools as `SpoolRing` (ring in the filament colour, filled arc = remaining amount)
  instead of colour dots + progress bars.
- **Self-explanatory.** Every state that needs attention says what is going on *and* offers the next action as a button.
  Prefer plain words over jargon, a short `page-subtitle` under each page title, and helpful empty states (`sh-empty`)
  with a primary action.
- **Calm surfaces.** Cards are white/`--sh-surface` with a 1px border, no heavy shadows, generous spacing. One primary
  button per area; secondary actions use `btn-outline-secondary`, destructive ones `btn-outline-danger`.
- **Touch friendly.** Interactive elements are at least 44 px high (`.btn` default height, `.btn-sm` only in dense
  tables/toolbars). No hover-only functionality.
- **Accessible.** Real `<button>`/`<a>`/`<label>`; icon-only buttons need `aria-label` (or `title` + `visually-hidden`
  text); decorative icons `aria-hidden="true"`; colours come from tokens (contrast is checked for both themes).

## Tokens (CSS variables)

| Token | Use |
|---|---|
| `--sh-bg` | page background |
| `--sh-surface`, `--sh-surface-2`, `--sh-surface-3` | cards, inputs; subtle fills; stronger fills |
| `--sh-border`, `--sh-border-strong` | borders, input borders |
| `--sh-text`, `--sh-muted` | text, secondary text |
| `--sh-accent`, `--sh-accent-soft`, `--sh-accent-text`, `--sh-on-accent` | primary actions, selected states |
| `--sh-ok*`, `--sh-warn*`, `--sh-danger*` (`-soft`, `-text`) | status colours |
| `--sh-track` | empty part of rings/progress bars |
| `--sh-radius-sm/–/-lg` | 10 / 14 / 20 px |
| `--sh-font`, `--sh-font-display` | Inter (text), Space Grotesk (headings) |

Never hard-code colours in pages (except filament colours from data). Bootstrap utility classes (`text-muted`,
`bg-body-tertiary`, `badge bg-success`, `text-bg-*`, …) are mapped to the tokens and are fine to use.

## Layout

- `MainLayout`: sidebar (desktop ≥ 992 px), sticky top bar with global search (Ctrl K / `/`), "Scan tag" and
  "Add spool"; on phones a bottom bar (Start, Spools, **Scan**, Locations, More) and an off-canvas menu.
- Main content is limited to 1440 px. Do not use `container`/`container-fluid` inside pages.
- Navigation entries live in `Components/AppNav.razor`.

## Page skeleton

```razor
<PageTitle>@L["x.title"] – @L["app.name"]</PageTitle>

<div class="page-header">
    <div>
        <h1>@L["x.title"]</h1>
        <p class="page-subtitle">@L["x.subtitle"]</p>
    </div>
    <div class="page-actions">
        <a class="btn btn-primary" href="..."><i class="bi bi-plus-lg" aria-hidden="true"></i>@L["x.add"]</a>
    </div>
</div>
```

Headings: one `h1` per page (via `page-header`), sections use `h2.sh-section-title` (or `.sh-card-title` inside cards).
Do not put icons in front of the page `h1` any more.

## Components and classes

| Class / component | Purpose |
|---|---|
| `<SpoolRing ColorHex Percent Size AriaLabel>` + optional child content | spool visual; child content is centred (e.g. `78 %`) |
| `.sh-card`, `.sh-card-title`, `.sh-card-sub` | content card (or Bootstrap `.card`, already themed) |
| `.sh-section-title` | section heading |
| `.sh-chip` (+ `-accent`, `-ok`, `-warn`, `-danger`, `-sm`) | small labels; as `<button aria-pressed>` they are filter chips |
| `.sh-segmented` with `<button aria-pressed>` | 2–4 option toggles (view switch, etc.) |
| `.sh-toolbar` | filter/search row above lists |
| `.sh-tile-grid`, `.sh-spool-tile` (`.sh-low`, `.sh-consumed`), `.sh-add-tile` | tile grids |
| `.sh-stat`, `.sh-stat-value`, `.sh-stat-label` | key figures |
| `.sh-attention` | "needs attention" card (ring/icon, chip, text, action button) |
| `.sh-empty` | empty state: icon, `h2`, text, primary action |
| `.sh-progress > span(.sh-warn/.sh-ok/.sh-danger)` | thin progress bar |
| `.sh-kv` (`<dl>`) | key/value details |
| `.sh-color-swatch` | small filament colour dot (e.g. in tables/selects) |
| `.modal-backdrop-custom` + `.modal-box` | existing custom dialogs (themed) |

Legacy classes (`spool-card`, `stat-card`, `remaining-bar`, `color-dot`, `location-*`, `drying-*`, `login-card`,
`landing-*`) are restyled and keep working, but new UI should use the `sh-*` building blocks.

## Texts

All UI strings live in `wwwroot/i18n/en.json` and `de.json` (same keys, CI checks it). Write German with proper
umlauts. Keep labels short and concrete ("Spule wiegen", not "Gewichtsaktualisierung durchführen").
