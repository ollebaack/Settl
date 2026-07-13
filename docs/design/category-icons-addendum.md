# Category icons addendum (2026-07-13)

Changed file: **`Settl App.dc.html`** only — replace `docs/design/Settl App.dc.html`. All other design files are unchanged since the previous export.

## What changed

Entry rows (dashboard "Senaste" + activity log) now show a **category icon** matched from the entry title, instead of the first letter:

- New logic methods `catIcon(title)` (keyword → icon, Swedish keywords) and `svgIcon(parts)` (17px, 24-viewBox, stroke 1.8, currentColor — Lucide-style).
- Keyword map, checked in order (first match wins; order matters — e.g. `städ` before `mat` so "Städmaterial" isn't matched as grocery):
  - städ/rengör/tvätt → sparkles
  - takeaway/thai/pizza/sushi/restaurang/lunch/middag/käk → utensils
  - konsert/biljett/bio/match/event → ticket
  - soffa/möbel/stol/bord/säng/fåtölj → armchair
  - mat/handl/ica/coop/willys/lidl/hemköp → shopping cart
  - taxi/buss/tåg/resa/bensin/parkering → car
  - internet/wifi/bredband → wifi
  - hyra → home
  - spotify/musik → music
  - netflix/hbo/stream/film/tv → tv
  - "el "/elräkning/ström → zap
  - blommor/present/gåva/årsdag → gift
- Fallbacks when no keyword matches: expense → receipt icon; IOU → `⇄` (unchanged); recurring → `↻` (unchanged).
- Icon tile colors unchanged (chip/sub; recurring keeps soft/accent).

## Implementation note

In the real app this should be a stored `category` enum on the entry (set at creation, keyword match only as a default suggestion), not runtime title matching. Icon set: lucide (sparkles, utensils, ticket, armchair, shopping-cart, car, wifi, home, music, tv, zap, gift, receipt-text).
