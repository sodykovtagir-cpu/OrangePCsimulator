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

Tariff: $100 per 1800 real seconds, offline included; first period upfront;
custom game URL +$1000 once. Backend time controls the cumulative bill. Stopping
ends future charges; restart starts a new period. Publishing updates is
idempotent for owner/save/publication key. Domains can be changed on the same
publication without changing that key or restarting the active rent period.
The custom-domain setup fee is charged only once per publication; existing
custom-domain records are treated as paid. Stops and domain changes revoke
old visit tickets, clear online sessions and invalidate the old address.
Stopped sites report next_bill=0, stay out of public search and cannot be viewed
or visited. A restart still charges one new first period. Up to 16 publications/account and
200 on this shared installation. These safety limits are not hardware quotas.
The money balance lives in the game save, not the backend: this is not an
anti-cheat payment system. See `docs/SITES_EDITOR_RU.md` for settlement details.

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
