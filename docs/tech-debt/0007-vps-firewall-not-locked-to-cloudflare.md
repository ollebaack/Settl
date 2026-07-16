# 0007: VPS firewall not restricted to Cloudflare's IP ranges

**What:** The production VPS (Strato, `31.70.90.93`) accepts inbound 80/443 from any
source. ADR-0013/ADR-0014 both call for locking this down to Cloudflare's published IP
ranges, so the WAF/DDoS protection Cloudflare provides can't be bypassed by hitting the
VPS's IP directly. This was never done — it needs actual VPS shell/firewall access,
which wasn't available during the Dokploy setup session.

**Why we took it:** Getting `settlapp.se` live end-to-end (domain, DNS, Dokploy, TLS,
the app itself) was the priority; the firewall rule is additive hardening on top of an
already-working setup, not a blocker to launch.

**Trigger to pay it down:** Before this matters for real (household financial data,
more than a handful of users), or opportunistically whenever someone next has VPS shell
access. Restrict inbound 80/443 to Cloudflare's current IP list
(https://www.cloudflare.com/ips/), with a note to re-sync if Cloudflare's ranges change.
