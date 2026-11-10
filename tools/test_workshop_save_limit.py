#!/usr/bin/env python3
"""Source checks; optional HTTP boundary tests against isolated PHP fixtures.
Set WORKSHOP_LIMIT_TEST_URL=http://127.0.0.1:8094 to test server/legacy/live
fixtures. NEVER point these tests at a production site: they create uploads.
"""
import os
from pathlib import Path
import requests

ROOT = Path(__file__).resolve().parents[1]
LIMIT = 30 * 1024 * 1024


def test_all_distributions_enforce_thirty_mib():
    for folder in ('server', 'Hosting/workshop'):
        base = ROOT / folder
        api = (base / 'api.php').read_text()
        helper = (base / 'upload_limits.php').read_text()
        assert "define('MAX_BYTES', 31457280)" in api
        assert "require_once __DIR__ . '/upload_limits.php'" in api
        assert 'workshop_validate_save_upload(true)' in api
        assert 'workshop_validate_save_upload(false)' in api
        assert 'workshop_validate_request_size()' in api
        assert "filesize($file['tmp_name'])" in helper
        assert 'min(MAX_BYTES, WORKSHOP_SAVE_LIMIT_BYTES)' in helper
        assert 'UPLOAD_ERR_INI_SIZE' in helper
        assert 'UPLOAD_ERR_FORM_SIZE' in helper
        assert 'WORKSHOP_SAVE_LIMIT_BYTES\', 30 * 1024 * 1024' in helper
        assert '], 413)' in helper
        assert 'upload_max_filesize = 30M' in (base / '.user.ini').read_text()
        assert 'post_max_size = 40M' in (base / '.user.ini').read_text()
        assert 'upload_max_filesize 30M' in (base / '.htaccess').read_text()
        assert 'post_max_size 40M' in (base / '.htaccess').read_text()
    assert (ROOT / 'server/upload_limits.php').read_bytes() == (ROOT / 'Hosting/workshop/upload_limits.php').read_bytes()
    client = (ROOT / 'Assets/Scripts/Assembly-CSharp/WorkshopClient.cs').read_text()
    assert 'MaxSaveBytes = 30 * 1024 * 1024' in client
    assert 'new FileInfo(localPath).Length > MaxSaveBytes' in client
    assert 'file > 10MB' not in client


def test_filament_labels_restored_but_watts_still_hidden():
    base = ROOT / 'Assets/Scripts/Assembly-CSharp/PC/Component'
    app = (base / 'Software/ThreeDPrinterApp.cs').read_text()
    screen = (base / 'ThreeDPrinterScreen.cs').read_text()
    assert 'EstimateFilamentMeters(model).ToString' in app
    assert 'spool.RemainingMeters.ToString' in app
    assert 'spool.CapacityMeters.ToString' in app
    assert 'CurrentWatts.ToString' not in app
    assert 'filamentText.text = filament;' in screen
    assert 'filamentText.gameObject.SetActive(true);' in screen


def http_boundary_tests(base_url):
    assert base_url.startswith(('http://127.0.0.1:', 'http://localhost:')), 'isolated local fixtures only'
    for variant in ('server', 'legacy', 'live'):
        url = base_url + '/' + variant + '/api.php'
        data = {'token': 'local-limit-test', 'title': 'Boundary test', 'author': 'Test'}
        # The envelope includes a cover; an exact-limit save must still fit.
        r = requests.post(url + '?action=upload', data=data,
                          files={'file': ('boundary.opc', b'x' * LIMIT), 'cover': ('cover.jpg', b'x' * 1000)}, timeout=30)
        assert r.status_code == 200 and r.json()['ok'], (variant, r.status_code, r.text)
        item = r.json()
        edit = dict(data, id=item['id'], owner_key=item['owner_key'])
        r = requests.post(url + '?action=update', data=edit, files={'file': ('ok.opc', b'y' * LIMIT)}, timeout=30)
        assert r.status_code == 200 and r.json()['ok'], r.text
        before = requests.get(url + '?action=list', timeout=10).json()['items']
        # One byte over the file limit is a 413, on both upload and replacement.
        for action, fields in [('upload', data), ('update', edit)]:
            r = requests.post(url + '?action=' + action, data=fields,
                              files={'file': ('too-big.opc', b'z' * (LIMIT + 1))}, timeout=30)
            assert r.status_code == 413 and not r.json()['ok'], (variant, action, r.status_code, r.text)
        after = requests.get(url + '?action=list', timeout=10).json()['items']
        assert before == after, 'Rejected updates must not mutate the index'
        # Metadata-only edits are still legal, with no replacement file.
        r = requests.post(url + '?action=update', data=edit, timeout=10)
        assert r.status_code == 200 and r.json()['ok'], r.text
        # PHP discards POST/FILES at post_max_size; never return fake success.
        r = requests.post(url + '?action=update', data=edit,
                          files={'file': ('request-too-big.opc', b'x' * (40 * 1024 * 1024 + 1))}, timeout=30)
        assert r.status_code == 413 and not r.json()['ok'], (r.status_code, r.text)
        r = requests.post(url + '?action=delete', data=edit, timeout=10)
        assert r.status_code == 200 and r.json()['ok'], r.text
        print('PASS HTTP', variant, '30 MiB upload/update accepted, +1 rejected, 40 MiB envelope rejected')


if __name__ == '__main__':
    test_all_distributions_enforce_thirty_mib()
    print('PASS server/client/config source contracts')
    test_filament_labels_restored_but_watts_still_hidden()
    print('PASS filament UI restored; watts hidden')
    if os.environ.get('WORKSHOP_LIMIT_TEST_URL'):
        http_boundary_tests(os.environ['WORKSHOP_LIMIT_TEST_URL'].rstrip('/'))
