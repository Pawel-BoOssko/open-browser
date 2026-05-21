import json
import sys
import urllib.error
import urllib.request
from pathlib import Path
from linkedin_api.clients.auth.client import AuthClient

CLIENT_CONFIG = Path('config/local/linkedin/linkedin_client.json')
TOKEN_PATH = Path('config/local/linkedin/linkedin_tokens.json')
VERSION = '202604'

if len(sys.argv) != 3:
    print('USAGE: python linkedin_post_image.py post.txt image.png')
    raise SystemExit(2)

post_path = Path(sys.argv[1])
image_path = Path(sys.argv[2])
if not post_path.exists():
    print('POST_FILE_MISSING', post_path)
    raise SystemExit(1)
if not image_path.exists():
    print('IMAGE_FILE_MISSING', image_path)
    raise SystemExit(1)

cfg = json.loads(CLIENT_CONFIG.read_text(encoding='utf-8'))
tokens = json.loads(TOKEN_PATH.read_text(encoding='utf-8'))

auth = AuthClient(
    client_id=cfg['client_id'],
    client_secret=cfg['client_secret'],
    redirect_url='http://127.0.0.1:8787/callback',
)

access_token = tokens.get('access_token')

def refresh():
    resp = auth.exchange_refresh_token_for_access_token(refresh_token=tokens['refresh_token'])
    tokens['access_token'] = resp.access_token
    tokens['refresh_token'] = resp.refresh_token
    tokens['expires_in'] = resp.expires_in
    tokens['scope'] = resp.scope
    TOKEN_PATH.write_text(json.dumps(tokens, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return resp.access_token

if not access_token:
    access_token = refresh()

def request_json(url, data=None, method='GET', extra_headers=None):
    headers = {'Authorization': f'Bearer {access_token}'}
    if extra_headers:
        headers.update(extra_headers)
    body = None if data is None else json.dumps(data).encode('utf-8')
    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=40) as resp:
        raw = resp.read().decode('utf-8')
        return json.loads(raw) if raw else {}, dict(resp.headers)

try:
    user_info, _ = request_json('https://api.linkedin.com/v2/userinfo')
except urllib.error.HTTPError as e:
    if e.code == 401:
        access_token = refresh()
        user_info, _ = request_json('https://api.linkedin.com/v2/userinfo')
    else:
        print('USERINFO_ERROR', e.code, e.read().decode('utf-8', errors='replace')[:1000])
        raise SystemExit(1)

person_urn = f"urn:li:person:{user_info['sub']}"
print('AUTHOR', user_info.get('name', user_info.get('sub')))

init_body = {'initializeUploadRequest': {'owner': person_urn}}
try:
    init_result, _ = request_json(
        'https://api.linkedin.com/rest/images?action=initializeUpload',
        data=init_body,
        method='POST',
        extra_headers={
            'Content-Type': 'application/json',
            'LinkedIn-Version': VERSION,
            'X-RestLi-Protocol-Version': '2.0.0',
        },
    )
except urllib.error.HTTPError as e:
    print('IMAGE_INIT_ERROR', e.code, e.read().decode('utf-8', errors='replace')[:1000])
    raise SystemExit(1)

upload_url = init_result['value']['uploadUrl']
image_urn = init_result['value']['image']
print('IMAGE_REGISTERED', image_urn)

img_bytes = image_path.read_bytes()
upload_req = urllib.request.Request(
    upload_url,
    data=img_bytes,
    headers={
        'Authorization': f'Bearer {access_token}',
        'Content-Type': 'application/octet-stream',
    },
    method='PUT',
)
try:
    with urllib.request.urlopen(upload_req, timeout=60) as resp:
        print('IMAGE_UPLOADED', resp.status)
except urllib.error.HTTPError as e:
    print('IMAGE_UPLOAD_ERROR', e.code, e.read().decode('utf-8', errors='replace')[:1000])
    raise SystemExit(1)

commentary = post_path.read_text(encoding='utf-8-sig')
post_body = {
    'author': person_urn,
    'commentary': commentary,
    'visibility': 'PUBLIC',
    'distribution': {'feedDistribution': 'MAIN_FEED', 'targetEntities': [], 'thirdPartyDistributionChannels': []},
    'content': {'media': {'altText': 'Antigravity 2.0 project inventory result', 'id': image_urn}},
    'lifecycleState': 'PUBLISHED',
    'isReshareDisabledByAuthor': False,
}

body_bytes = json.dumps(post_body, ensure_ascii=False).encode('utf-8')
post_req = urllib.request.Request(
    'https://api.linkedin.com/rest/posts',
    data=body_bytes,
    headers={
        'Authorization': f'Bearer {access_token}',
        'Content-Type': 'application/json',
        'LinkedIn-Version': VERSION,
        'X-RestLi-Protocol-Version': '2.0.0',
    },
    method='POST',
)
try:
    with urllib.request.urlopen(post_req, timeout=40) as resp:
        raw = resp.read().decode('utf-8')
        headers = dict(resp.headers)
except urllib.error.HTTPError as e:
    print('POST_ERROR', e.code, e.read().decode('utf-8', errors='replace')[:1000])
    raise SystemExit(1)

post_id = headers.get('x-restli-id') or headers.get('X-RestLi-Id') or 'unknown'
print('POST_OK', post_id)
out = Path(f"tools/linkedin/exports/post_image_{post_id.replace(':', '_')}.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({'post_id': post_id, 'image': image_urn, 'raw': raw, 'headers': headers}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('saved', str(out))

