#!/usr/bin/env python3
"""HTTP chunk transport regression on local isolated PHP fixtures only.
Run with WORKSHOP_LIMIT_TEST_URL=http://127.0.0.1:8094; PHP may be capped at
20M upload / 30M POST like the production host. No production test uploads.
"""
import hashlib
import os
from pathlib import Path
import requests

ROOT = Path(__file__).resolve().parents[1]
LIMIT = 30 * 1024 * 1024
CHUNK = 8 * 1024 * 1024


def test_source_contracts():
    client = (ROOT / 'Assets/Scripts/Assembly-CSharp/WorkshopClient.cs').read_text()
    assert 'UploadSaveChunks(bytes' in client
    assert 'SaveChunkBytes = 8 * 1024 * 1024' in client
    assert 'Field("staged_upload", stagedId)' in client
    for d in ('server', 'Hosting/workshop'):
        base = ROOT / d
        api = (base / 'api.php').read_text()
        chunk = (base / 'upload_chunks.php').read_text()
        assert "$action === 'upload_chunk'" in api
        assert 'workshop_move_save_upload($dest)' in api
        assert 'move_uploaded_file($_FILES[\'file\']' not in api
        assert 'hash_equals($meta[\'key\'], $key)' in chunk
        assert 'WORKSHOP_SAVE_LIMIT_BYTES' in chunk
        assert 'time() - 3600' in chunk
        assert 'flock($lock, LOCK_EX)' in chunk
        assert '$own >= 4' in chunk
        assert '$received !== $offset' in chunk


def run_http(base):
    assert base.startswith(('http://127.0.0.1:', 'http://localhost:'))
    for variant in ('server', 'legacy', 'live'):
        url = base + '/' + variant + '/api.php'
        auth = {'token': 'local-limit-test'}
        session = {}
        digest = hashlib.sha256()
        # One byte above the aggregate limit is rejected even if each request is tiny.
        r = requests.post(url + '?action=upload_chunk', data=dict(auth, total_bytes=LIMIT+1, offset=0),
                          files={'chunk': ('chunk.bin', b'x')}, timeout=10)
        assert r.status_code == 413, r.text
        for i, offset in enumerate(range(0, LIMIT, CHUNK)):
            part = bytes([i + 1]) * min(CHUNK, LIMIT-offset)
            digest.update(part)
            fields = dict(auth, total_bytes=LIMIT, offset=offset, **session)
            r = requests.post(url + '?action=upload_chunk', data=fields,
                              files={'chunk': ('chunk.bin', part)}, timeout=30)
            answer = r.json()
            assert r.status_code == 200 and answer['ok'], (variant, r.status_code, r.text)
            assert answer['received_bytes'] == offset + len(part)
            session = {k: answer[k] for k in ('upload_id', 'upload_key')}
            if i == 0:
                final = dict(auth, staged_upload=session['upload_id'], staged_key=session['upload_key'])
                r = requests.post(url + '?action=upload', data=final, timeout=10)
                assert r.status_code == 400 and r.json()['error'] == 'incomplete upload', r.text
                wrong = dict(fields, upload_id=session['upload_id'], upload_key='0' * 48, offset=len(part))
                r = requests.post(url + '?action=upload_chunk', data=wrong, files={'chunk': ('part', b'x')}, timeout=10)
                assert r.status_code == 403, r.text
                overlap = dict(auth, total_bytes=LIMIT, offset=0, **session)
                r = requests.post(url + '?action=upload_chunk', data=overlap, files={'chunk': ('part', b'x')}, timeout=10)
                assert r.status_code == 409, r.text
        final = dict(auth, title='Chunk boundary', author='Test', staged_upload=session['upload_id'], staged_key=session['upload_key'])
        r = requests.post(url + '?action=upload', data=final, timeout=10)
        assert r.status_code == 200 and r.json()['ok'], r.text
        item = r.json()
        listed = requests.get(url+'?action=list', timeout=10).json()['items']
        match = next(it for it in listed if it['id'] == item['id'])
        assert match['size_bytes'] == LIMIT
        actual = hashlib.sha256()
        r = requests.get(url+'?action=download&id='+str(item['id']), stream=True, timeout=30)
        for block in r.iter_content(1024*1024): actual.update(block)
        assert actual.digest() == digest.digest(), 'assembled content differs'
        # Successful publish consumes the staging session; it cannot be replayed.
        r = requests.post(url + '?action=upload', data=final, timeout=10)
        assert r.status_code == 403, r.text
        # Also replace this save through the same staged-file path.
        part = b'chunk replacement'
        r = requests.post(url+'?action=upload_chunk', data=dict(auth,total_bytes=len(part),offset=0),
                          files={'chunk':('p',part)}, timeout=10)
        stage = r.json();assert stage['ok'], r.text
        edit = dict(auth,id=item['id'],owner_key=item['owner_key'],staged_upload=stage['upload_id'],staged_key=stage['upload_key'])
        r = requests.post(url+'?action=update',data=edit,timeout=10)
        assert r.status_code==200 and r.json()['ok'],r.text
        assert requests.get(url+'?action=download&id='+str(item['id']),timeout=10).content==part
        r = requests.post(url+'?action=delete',data=edit,timeout=10)
        assert r.json()['ok'],r.text
        print('PASS chunked', variant, '30 MiB roundtrip, +1 rejection, wrong secret, overlap, incomplete/replay, update')


if __name__ == '__main__':
    test_source_contracts()
    print('PASS bounded/authenticated chunk source contracts')
    if os.environ.get('WORKSHOP_LIMIT_TEST_URL'):
        run_http(os.environ['WORKSHOP_LIMIT_TEST_URL'].rstrip('/'))
