#!/usr/bin/env bash
# Redeploy the static site to GitHub Pages (the gh-pages branch).
# Run this after editing anything in website/static/ to push it live.
#
#   ./website/deploy-pages.sh
#
# The live site is https://danielphelan92.github.io/nanovault/
set -euo pipefail

REPO_URL="https://github.com/danielphelan92/nanovault.git"
SRC="$(cd "$(dirname "$0")/static" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Copy the whole static site (index.html, css, js, img, sitemap.xml,
# robots.txt, …) except the local preview server.
cp -R "$SRC/." "$TMP/"
rm -f "$TMP/server.js"
touch "$TMP/.nojekyll"   # tell Pages not to run Jekyll

cd "$TMP"
git init -q -b gh-pages
git add -A
git commit -q -m "Deploy NanoVault site $(date -u +%Y-%m-%dT%H:%M:%SZ)"
git remote add origin "$REPO_URL"
git push -f origin gh-pages

echo "Deployed. Live in ~1 minute at https://danielphelan92.github.io/nanovault/"
