# Shared orange-site backend

Deploy the runtime files in this directory to `/htdocs/sites/` on the same host
as the existing `/htdocs/workshop/`. Do **not** replace workshop files, account
files, configs, uploads, or any existing `sites/data` contents.

Runtime files: `api.php`, `lib.php`, `reserved.json`, `.htaccess`, `.user.ini`,
`data/.htaccess`. No credentials belong in this directory or client source.
`api.php` reads existing verified workshop users and their session tokens from
`../workshop/users.json`. It does not write that file. PHP 7.3+ is required
(the production host should use a supported PHP 8.x release).

The writable data directory contains PHP-guarded base64 JSON state/package
files, an exclusive flock lock, and ephemeral salted abuse-limit keys. Site
packages are data only; never write them as unguarded PHP/HTML. All changes are
serialized through the lock and files use temporary-write + rename. A visit
requires a signed five-minute ticket; online sessions expire after 90 seconds.
The service is not a general file execution service or real DNS registrar.

Tariff (v3, wallet model): publishing is free. Rent is $100 per 1800 real
seconds of hosting ($1100 with a custom domain, +$1000 once for the domain) and
is paid **only** from the hosting wallet, never from the game balance: the
wallet is filled exclusively by `action=topup`. On top of the base rent there is
a traffic surcharge of $20 per 1800 seconds of real serving — the seconds a
browser session or a download was open. An empty wallet pauses the site without
creating debt; served time is counted per second, idempotently, and a repeated
or lost request never bills the same second twice.

Health request: POST `action=health` to `/sites/api.php?i=1`. Expected JSON:
`ok=true, service=orange-sites, version=1, available=true`. Byethost's browser
challenge must be solved before calling PHP; the game reuses the workshop
cookie handshake. Challenge HTML must never be treated as an empty success.
Verify an unauthenticated `stats` request is rejected, `list` returns JSON,
and `data/state.php` and `lib.php` do not expose storage/source content.
Do not use a real account to seed public test publications automatically.

Run `python tools/test_site_backend.py` from the repository root. It executes
real PHP against temporary data and synthetic account fixtures; no network.
Never upload its generated fixture data (it is automatically removed).

Followup protocol: `info` supplies public metadata and a bounded thumbnail;
`transfer` requires the current signed visit ticket plus a root filename and
returns host telemetry after checking publication/ban/payment state. Nothing
executes uploaded HTML/Lua on the web host. GD thumbnails are optional; safe
small originals work without it. All icon data stays under the normal package
validation and image limits.

Authenticated stats/publish requests can include a nonnegative `balance` and
`settled` (or the save's `ledgers` for initial account resolution). The verified
owner/world key stores one shared expenditure ceiling, not one balance per
site. Exhaustion suspends service at the next unpaid period, without growing
unpaid debt. A sufficient new snapshot resumes with a new prepaid period.
Existing pre-snapshot bills remain payable. This trusts the local game save;
it is not an anti-cheat bank or visibility into offline edits to that file.

Deploy `server/sites_admin.php` beside the existing workshop `admin.php`, and
merge the new Sites tab/CSRF-gated actions into that existing page. Never
replace admin_config.json, users.json, account.php or account sessions/config.
Moderation uses the same storage lock as the API. Banned publications are
stopped, their tickets revoked; unbanning does not auto-start rent. Direct
requests to sites_admin.php have no independent authority and return 404.


Current additions: authenticated `host_stop` stops every publication on the
matching owner/world/host, including leases and access epochs. `stopped_hosts`
on owner stats retries failed reports. Exiting the save is not a host stop.
Transfer phases `start/pulse/end/cancel` track active downloads (30s TTL), renew
5-minute tickets, and expose `transferring` + cumulative `downloads` for CPU/RAM
simulation. Stats are polled every 5s; short transfers are accounted by deltas.
Custom domain rent is now recurring: $1100 total per period versus $100 for the
generated domain. Existing elapsed boundaries are not retroactively increased.

Deploy `sites_preview.php` and `sites_preview.js` beside `sites_admin.php`.
The authenticated admin renderer embeds validated packages into a sandboxed
srcdoc with a restrictive CSP; JSON/text is escaped, images use validated data
URIs, only trusted renderer code runs. No eval, user Lua, external requests or
arbitrary HTML from packages. Full Lua behavior remains an in-game feature.
