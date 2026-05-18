"""Post content to LinkedIn using saved OAuth tokens.

Automatically refreshes the access token if expired using the official
LinkedIn Python client.

Usage:
  python linkedin_post.py "Post text here"
  python linkedin_post.py --article "Title" "Article body text..."

Config: config/local/linkedin/linkedin_client.json
        config/local/linkedin/linkedin_tokens.json
"""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

from linkedin_api.clients.auth.client import AuthClient

CLIENT_CONFIG = Path("config/local/linkedin/linkedin_client.json")
TOKEN_PATH = Path("config/local/linkedin/linkedin_tokens.json")

if not CLIENT_CONFIG.exists():
    print("CLIENT_CONFIG_MISSING — run linkedin_auth.py first")
    raise SystemExit(1)
if not TOKEN_PATH.exists():
    print("TOKENS_MISSING — run linkedin_auth.py first")
    raise SystemExit(1)

cfg = json.loads(CLIENT_CONFIG.read_text(encoding="utf-8"))
tokens = json.loads(TOKEN_PATH.read_text(encoding="utf-8"))

auth = AuthClient(
    client_id=cfg["client_id"],
    client_secret=cfg["client_secret"],
    redirect_url="http://127.0.0.1:8787/callback",
)

access_token = tokens.get("access_token")

# --- Refresh token if needed ---
def refresh():
    resp = auth.exchange_refresh_token_for_access_token(
        refresh_token=tokens["refresh_token"]
    )
    tokens["access_token"] = resp.access_token
    tokens["refresh_token"] = resp.refresh_token
    tokens["expires_in"] = resp.expires_in
    tokens["scope"] = resp.scope
    TOKEN_PATH.write_text(json.dumps(tokens, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return resp.access_token


if not access_token:
    refresh_token = tokens.get("refresh_token")
    if not refresh_token:
        print("NO_TOKENS — run linkedin_auth.py again")
        raise SystemExit(1)
    print("Refreshing access token...")
    access_token = refresh()

# --- Get user info ---
user_req = urllib.request.Request(
    "https://api.linkedin.com/v2/userinfo",
    headers={"Authorization": f"Bearer {access_token}"},
)
try:
    with urllib.request.urlopen(user_req, timeout=20) as resp:
        user_info = json.loads(resp.read().decode("utf-8"))
        person_urn = f"urn:li:person:{user_info['sub']}"
        print("AUTHOR", user_info.get("name", user_info.get("sub")))
except urllib.error.HTTPError as e:
    if e.code == 401:
        print("Token expired, refreshing...")
        access_token = refresh()
        user_req = urllib.request.Request(
            "https://api.linkedin.com/v2/userinfo",
            headers={"Authorization": f"Bearer {access_token}"},
        )
        with urllib.request.urlopen(user_req, timeout=20) as resp:
            user_info = json.loads(resp.read().decode("utf-8"))
            person_urn = f"urn:li:person:{user_info['sub']}"
    else:
        print("USERINFO_ERROR", e.code, e.read().decode("utf-8", errors="replace")[:1000])
        raise SystemExit(1)

# --- Build post ---
is_article = "--article" in sys.argv
if is_article:
    args = [a for a in sys.argv[1:] if a != "--article"]
    if len(args) < 2:
        print("USAGE: python linkedin_post.py --article 'Title' 'Body'")
        raise SystemExit(2)
    post_body = {
        "author": person_urn,
        "commentary": args[0],
        "visibility": "PUBLIC",
        "distribution": {"feedDistribution": "MAIN_FEED", "targetEntities": [], "thirdPartyDistributionChannels": []},
        "content": {"article": {"title": args[0], "description": " ".join(args[1:])}},
        "lifecycleState": "PUBLISHED",
        "isReshareDisabledByAuthor": False,
    }
else:
    if len(sys.argv) < 2:
        print("USAGE: python linkedin_post.py 'Your post text'")
        raise SystemExit(2)
    post_body = {
        "author": person_urn,
        "commentary": " ".join(sys.argv[1:]),
        "visibility": "PUBLIC",
        "distribution": {"feedDistribution": "MAIN_FEED", "targetEntities": [], "thirdPartyDistributionChannels": []},
        "lifecycleState": "PUBLISHED",
        "isReshareDisabledByAuthor": False,
    }

# --- Post ---
body_bytes = json.dumps(post_body).encode("utf-8")
post_req = urllib.request.Request(
    "https://api.linkedin.com/rest/posts",
    data=body_bytes,
    headers={
        "Authorization": f"Bearer {access_token}",
        "Content-Type": "application/json",
        "LinkedIn-Version": "202411",
        "X-RestLi-Protocol-Version": "2.0.0",
    },
    method="POST",
)

try:
    with urllib.request.urlopen(post_req, timeout=30) as resp:
        result = json.loads(resp.read().decode("utf-8"))
except urllib.error.HTTPError as e:
    error_body = e.read().decode("utf-8", errors="replace")
    print("POST_ERROR", e.code, error_body[:1000])
    raise SystemExit(1)

post_urn = f"urn:li:post:{result.get('id', 'unknown')}"
print("POST_OK", post_urn)

out = Path(f"tools/linkedin/exports/post_{result.get('id', 'unknown')}.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("saved", str(out))
