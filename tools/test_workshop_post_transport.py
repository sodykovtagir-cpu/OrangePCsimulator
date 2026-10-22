#!/usr/bin/env python3
"""Headless guard for account/form POST encoding on the workshop host.

The actual PHP endpoint was separately probed with harmless action=ping:
urlencoded reached PHP; multipart was rejected by the hosting proxy (403).
"""
from pathlib import Path
from urllib.parse import parse_qs, quote

root = Path(__file__).resolve().parents[1]
client = (root / 'Assets/Scripts/Assembly-CSharp/WorkshopClient.cs').read_text()
server = (root / 'server/account.php').read_text() if (root / 'server/account.php').exists() else ''

assert client.count('using (var req = PostForm(baseUrl + query, form))') == 2
post_form = client.split('private static UnityWebRequest PostForm(', 1)[1].split(
    'private IEnumerator RequestPostBase(', 1)[0]
assert 'if (hasFile) return UnityWebRequest.Post(url, form);' in post_form
assert '!string.IsNullOrEmpty(part.fileName)' in post_form
assert 'Uri.EscapeDataString(part.sectionName)' in post_form
assert 'Uri.EscapeDataString(Encoding.UTF8.GetString(part.sectionData))' in post_form
assert 'new UploadHandlerRaw(Encoding.UTF8.GetBytes(data.ToString()))' in post_form
assert 'application/x-www-form-urlencoded' in post_form
assert 'new DownloadHandlerBuffer()' in post_form
assert 'request.chunkedTransfer = false;' in post_form
assert 'url + "?token="' not in post_form
for action in ('register', 'verify', 'login', 'resend', 'me', 'logout', 'tg_link'):
    assert f'AccountUrls, "?action={action}&i=1", Form(' in client
if server:
    for action in ('register', 'verify', 'login', 'resend', 'me', 'logout', 'tg_link'):
        assert f"$action === '{action}'" in server

# PHP's application/x-www-form-urlencoded parser must receive each original
# field unchanged, even if a password contains +, &, = or non-ASCII text.
fields = {'login': 'a+b@host.test', 'password': 'x&y=да +z', 'token': 'ab/cd?'}
encoded = '&'.join(quote(name, safe='') + '=' + quote(value, safe='')
                   for name, value in fields.items())
assert {k: v[0] for k, v in parse_qs(encoded).items()} == fields
print('PASS: account/text POSTs are URL-encoded; binary file uploads stay multipart')
print('NOTE: Unity Play Mode and live login with user credentials were not run')
