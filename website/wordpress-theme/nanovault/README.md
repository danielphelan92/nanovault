# NanoVault WordPress theme

A clean, animated one-page landing theme for the NanoVault app. Dark/light
aware, responsive, accessible, with scroll-reveal animations, a floating app
mockup, a features grid, FAQ, and a Buy-Me-a-Coffee donation section.

## Install

1. Zip this `nanovault` folder (so the zip contains `nanovault/style.css`, etc.).
   - On the command line: `cd website/wordpress-theme && zip -r nanovault-theme.zip nanovault`
2. In WordPress: **Appearance → Themes → Add New → Upload Theme**, choose the
   zip, install, and **Activate**.
3. Make sure your front page uses the theme's homepage: **Settings → Reading →
   Your homepage displays → “A static page”** (any page) — or leave it on
   “Your latest posts”; the theme renders the landing page either way.

## Configure (no code needed)

Go to **Appearance → Customize → NanoVault** and fill in:

| Field | What it is |
| --- | --- |
| **Download URL (.exe)** | Direct link to `NanoVault-Setup-1.0.0.exe`. **Host it on [GitHub Releases](https://docs.github.com/repositories/releasing-projects-on-github) — the 49 MB installer is too big for WordPress's media library** (typical 2–8 MB upload cap). Paste the release asset link here. |
| **Buy Me a Coffee / donation URL** | e.g. `https://www.buymeacoffee.com/yourname`, a Ko-fi, or a PayPal.me link. |
| **GitHub repository URL** | Powers the “Source code”, “Report a problem” and “Star on GitHub” links. |
| **App version label** | Shown on the download button (e.g. `1.0.0`). |
| **Ad code (optional)** | Paste a Google AdSense (or other) ad unit to show one unobtrusive slot below the safety section. Leave blank for none. |

That's everything — no template editing required.

## Earning from it

- **Donations** are wired in already (the coffee button). This is the least
  intrusive option and fits a free tool.
- **Ads:** the theme has a single, optional ad slot you enable by pasting your
  AdSense unit into the Customizer field above. It sits *below* the content,
  never in the download flow, so the app stays trustworthy. Google AdSense
  needs its own account and site approval — the theme does not include any
  publisher ID.

## Hosting the download for free

You don't need paid file hosting. Good free options for the `.exe`:

- **GitHub Releases** (recommended) — unlimited, fast, versioned.
- Cloudflare R2 / Backblaze B2 free tiers.
- Any static host you already use.

Then paste the direct file URL into the Customizer **Download URL** field.

## Notes

- Styles live in `assets/css/main.css`; interactions in `assets/js/main.js`.
  Both are shared verbatim with the standalone static site in `../static`.
- Respects `prefers-reduced-motion` and `prefers-color-scheme`.
- MIT licensed, same as the app.

## SEO (WordPress)

The theme sets a proper `<title>` and clean markup, but for full SEO on
WordPress the easiest route is a free plugin — **Yoast SEO** or **Rank Math** —
which handles meta descriptions, Open Graph, sitemaps and schema for you. If you
instead use the free static site (`website/static`), all of that is already
built in — see [`docs/SEO.md`](../../../docs/SEO.md).
