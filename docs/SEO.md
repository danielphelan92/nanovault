# Getting NanoVault found on Google

Realistic expectations first: SEO is not instant. A brand-new site usually
takes **1–4 weeks to get indexed** and longer to rank. The steps below are in
priority order — #1 and #2 are what actually get you into Google; the rest
compounds over time.

---

## ✅ Already done on the site (technical SEO)

These shipped with the live site at https://danielphelan92.github.io/nanovault/:

- Keyword-aware `<title>` and meta description ("free iPod music backup for PC, no iTunes").
- Canonical URL, `robots` meta (index, follow), mobile-friendly + fast (static, no bloat).
- Open Graph + Twitter cards, so links unfurl with the app screenshot on social/Discord/WhatsApp.
- **Structured data** (`SoftwareApplication` + `FAQPage` JSON-LD) — lets Google show it as a free app and can surface your FAQ in results.
- `sitemap.xml` and `robots.txt`.

You don't need to touch any of that. The rest is manual because it needs your accounts.

---

## 1. Google Search Console — do this first (~10 min)

This is how Google discovers the site and how you ask it to index you *now*
instead of waiting to be found.

1. Go to **https://search.google.com/search-console** and sign in.
2. Click **Add property → URL prefix**, and enter exactly:
   `https://danielphelan92.github.io/nanovault/`
3. Choose the **"HTML tag"** verification method. Google shows a line like
   `<meta name="google-site-verification" content="ABC123..." />`.
   **Copy the `content` value** and send it to me — I'll paste it into the page
   (there's already a commented-out slot for it) and redeploy, then you click
   **Verify**. *(Or self-serve: uncomment the `google-site-verification` meta near
   the top of `website/static/index.html`, drop your code in, and run
   `./website/deploy-pages.sh`.)*
4. Once verified: open **Sitemaps** (left menu) and submit `sitemap.xml`.
5. Open **URL Inspection** (top search bar), paste your URL, and click
   **Request indexing**. This is the single biggest lever — it can get you
   indexed in hours/days instead of weeks.

## 2. Bing Webmaster Tools (~5 min, easy extra traffic)

Bing also powers DuckDuckGo and some ChatGPT search. Go to
**https://www.bing.com/webmasters**, add the same URL, and you can **import
directly from Google Search Console** in one click. Submit the sitemap there too.

---

## 3. Backlinks & mentions — where your actual visitors will come from

For a niche free tool, links/mentions matter more than keywords. Post honestly
(you built a free thing people want — that's welcome in these places):

- **Reddit**: r/ipod, r/DigitalMusic, r/software, r/opensource. Share it as "I made
  a free, no-paywall iPod backup tool." These threads often rank in Google themselves.
- **AlternativeTo.net**: list NanoVault as a free alternative to iMazing / CopyTrans /
  Senuti. High-intent traffic — people literally searching for this.
- **GitHub**: the repo already has topics (`ipod`, `music-backup`, …). Add it to
  relevant "awesome" lists via pull request. Star-worthy repos get crawled well.
- **Hacker News** (Show HN), **Lobsters**, and old-Apple/iPod forums.
- **Product directories**: Slant, SaaSHub, and similar.

Each real mention is a backlink; a handful from these moves the needle far more
than any on-page tweak.

## 4. Content that matches what people type

People don't search "NanoVault" (yet) — they search the *problem*. The page
already targets several of these, but you can add a short blog/README section or
even a second page answering them verbatim:

- "how to get music off an iPod onto a computer"
- "copy songs from iPod to PC without iTunes"
- "transfer iPod nano music to computer free"
- "iPod to PC music transfer"

Answering these plainly (which the FAQ already starts to do) is what wins
long-tail search.

---

## Optional: a custom domain (£/€/$ ~10–12 a year)

Not required — a `github.io` page can and will rank. But a domain like
`nanovault.app` looks more trustworthy, is easier to share, and lets you verify
the **whole domain** in Search Console (including a real root `robots.txt`).
GitHub Pages supports custom domains for free (you only pay the registrar).
If you ever want it, buy the domain, add a `CNAME` file, and point DNS — I can
set that up in a few minutes.

## How to check progress

- In Google, search `site:danielphelan92.github.io/nanovault` — once results
  appear, you're indexed.
- Search Console's **Performance** tab shows which queries you're showing up for
  and your position, usually within a few days of indexing.
- Test your structured data at https://search.google.com/test/rich-results
  (paste the live URL).
