# NanoVault website

The marketing/download site for NanoVault, in two interchangeable forms that
share the exact same design, CSS and JavaScript:

```
website/
├─ wordpress-theme/nanovault/   ← upload to WordPress (what you asked for)
└─ static/                      ← plain HTML; host free on GitHub Pages / Netlify
```

Both render an identical clean, animated one-page landing site: gradient hero
with a floating app mockup, an anti-paywall message, features grid, "how it
works" steps, animated stats, iPod compatibility, a read-only safety section,
a **Buy Me a Coffee** donation block, an FAQ, and an optional ad slot.

## Which one should I use?

- **WordPress** — use `wordpress-theme/nanovault`. See its
  [README](wordpress-theme/nanovault/README.md) for upload + setup. All links
  are set in **Appearance → Customize → NanoVault** (no code editing).
- **Free static hosting** — if you'd rather not pay for WordPress hosting, the
  `static/` folder is a complete site you can drop on GitHub Pages, Netlify,
  Cloudflare Pages or Vercel for **£0**. Edit the three tokens in
  `static/index.html` (`DOWNLOAD_URL`, `BUYMEACOFFEE`, `GITHUB_URL`) and deploy
  the folder.

## Three things to fill in (either version)

1. **Download link** — upload `NanoVault-Setup-1.0.0.exe` to **GitHub Releases**
   (free; the 49 MB installer is too big for a WordPress media upload) and point
   the download link at it.
2. **Buy Me a Coffee link** — create an account at buymeacoffee.com (or Ko-fi /
   PayPal.me) and paste your link.
3. **GitHub link** — your repository, for the source and "report a problem"
   links.

## Making money, honestly

- The **donation button** is already built in — the lowest-friction way to earn
  from a free tool.
- One **optional ad slot** exists (Customizer field in WordPress, or uncomment
  the `.ad-slot` block in `static/index.html`). It sits below the content and
  never in the download path. Google AdSense requires its own approval; no
  publisher ID is bundled.

## Preview the static site locally

```bash
cd website/static
node server.js         # then open http://localhost:8099
```

## A note on honesty (keeps you out of trouble)

The site already states plainly that the installer is **unsigned** (so Windows
SmartScreen may warn) and community-tested, and that NanoVault doesn't remove
DRM. Keep those lines — being upfront is exactly what sets this apart from the
"pay to unlock your own songs" apps you're reacting to, and it avoids
misleading-advertising problems.
