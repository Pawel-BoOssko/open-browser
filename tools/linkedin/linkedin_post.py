"""Post content to LinkedIn using saved OAuth tokens.

Automatically refreshes the access token if expired.

Usage:
  python linkedin_post.py "Post text here"
  python linkedin_post.py --article "Title" "Article body text..."

Config: config/local/linkedin/linkedin_client.json (client_id, client_secret)
        config/local/linkedin/linkedin_tokens.json (auto-managed)
"""
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

CLIENT_CONFIG = Path("config/local/linkedin/linkedin_client.json")
TOKEN_PATH = Path("config/local/linkedin/linkedin_tokens.json")

if not CLIENT_CONFIG.exists():
    print("CLIENT_CONFIG_MISSING — run linkedin_auth.py first")
    raise SystemExit(1)
if not TOKEN_PATH.exists():
    print("TOKENS_MISSING — run linkedin_auth.py first")
    raise SystemExit(1)

cfg = json.loads(CLIENT_CONFIG.read_text(encoding="utf-8"))
CLIENT_ID = cfg["client_id"]
CLIENT_SECRET = cfg.get("client_secret", "")

tokens = json.loads(TOKEN_PATH.read_text(encoding="utf-8"))


# --- Refresh token if needed ---
def refresh_access_token(refresh_token):
    body = urllib.parse.urlencode(
        {
            "grant_type": "refresh_token",
            "refresh_token": refresh_token,
            "client_id": CLIENT_ID,
            "client_secret": CLIENT_SECRET,
        }
    ).encode("ascii")

    req = urllib.request.Request(
        "https://www.linkedin.com/oauth/v2/accessToken",
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            new_tokens = json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        print("REFRESH_ERROR", e.code, e.read().decode("utf-8", errors="replace")[:1000])
        raise SystemExit(1)

    if "refresh_token" not in new_tokens:
        new_tokens["refresh_token"] = refresh_token
    TOKEN_PATH.write_text(json.dumps(new_tokens, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return new_tokens


access_token = tokens.get("access_token")
if not access_token:
    refresh_token = tokens.get("refresh_token")
    if not refresh_token:
        print("NO_TOKENS — run linkedin_auth.py again")
        raise SystemExit(1)
    print("Refreshing access token...")
    tokens = refresh_access_token(refresh_token)
    access_token = tokens["access_token"]

# --- Get user info (URN) ---
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
    refresh_token = tokens.get("refresh_token")
    if refresh_token and e.code == 401:
        print("Token expired, refreshing...")
        tokens = refresh_access_token(refresh_token)
        access_token = tokens["access_token"]
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
    title = args[0]
    body_text = " ".join(args[1:])
    post_body = {
        "author": person_urn,
        "commentary": title,
        "visibility": "PUBLIC",
        "distribution": {"feedDistribution": "MAIN_FEED", "targetEntities": [], "thirdPartyDistributionChannels": []},
        "content": {"article": {"title": title, "description": body_text}},
        "lifecycleState": "PUBLISHED",
        "isReshareDisabledByAuthor": False,
    }
else:
    if len(sys.argv) < 2:
        print("USAGE: python linkedin_post.py 'Your post text'")
        print("USAGE: python linkedin_post.py --article 'Title' 'Body'")
        raise SystemExit(2)
    body_text = " ".join(sys.argv[1:])
    post_body = {
        "author": person_urn,
        "commentary": body_text,
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

post_id = result.get("id", "unknown")
post_urn = f"urn:li:post:{post_id}"
print("POST_OK", post_urn)

out = Path(f"tools/linkedin/exports/post_{post_id}.json")
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("saved", str(out))
