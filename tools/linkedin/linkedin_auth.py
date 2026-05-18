"""OAuth 2.0 authorization for LinkedIn API using official LinkedIn Python client.

Opens a browser for the user to authorize the app, catches the callback
via a local HTTP server, and exchanges the code for tokens.

Prerequisites:
- Register an app at https://www.linkedin.com/developers/apps
- Add redirect URL: http://127.0.0.1:8787/callback
- Save client_id and client_secret to config/local/linkedin/linkedin_client.json
- pip install linkedin-api-client

Usage: python linkedin_auth.py
Output: config/local/linkedin/linkedin_tokens.json
"""
import json
import sys
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

from linkedin_api.clients.auth.client import AuthClient

CLIENT_CONFIG = Path("config/local/linkedin/linkedin_client.json")
TOKEN_PATH = Path("config/local/linkedin/linkedin_tokens.json")
REDIRECT_URI = "http://127.0.0.1:8787/callback"
SCOPES = ["openid", "profile", "email", "w_member_social"]
PORT = 8787

if not CLIENT_CONFIG.exists():
    print("CLIENT_CONFIG_MISSING", str(CLIENT_CONFIG))
    print("Create: config/local/linkedin/linkedin_client.json with client_id and client_secret")
    raise SystemExit(1)

cfg = json.loads(CLIENT_CONFIG.read_text(encoding="utf-8"))

auth = AuthClient(
    client_id=cfg["client_id"],
    client_secret=cfg["client_secret"],
    redirect_url=REDIRECT_URI,
)

auth_url = auth.generate_member_auth_url(scopes=SCOPES)
print("Opening browser for LinkedIn authorization...")
print(auth_url)
webbrowser.open(auth_url)

# --- Callback server ---
callback = {}


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        qs = parse_qs(urlparse(self.path).query)
        callback["code"] = qs.get("code", [None])[0]
        body = b"Authorization successful! You can close this window."
        self.send_response(200)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, fmt, *args):
        pass


server = HTTPServer(("127.0.0.1", PORT), Handler)
deadline = 180
elapsed = 0
print(f"Waiting for callback on {REDIRECT_URI} ...")
while not callback.get("code") and elapsed < deadline:
    server.timeout = 5
    try:
        server.handle_request()
    except Exception:
        pass
    elapsed += 5
server.server_close()

if not callback.get("code"):
    print("NO_AUTH_CODE")
    raise SystemExit(1)

print("Code received, exchanging for tokens...")

# --- Exchange via official client ---
response = auth.exchange_auth_code_for_access_token(code=callback["code"])

tokens = {
    "access_token": response.access_token,
    "refresh_token": response.refresh_token,
    "expires_in": response.expires_in,
    "scope": response.scope,
}
TOKEN_PATH.parent.mkdir(parents=True, exist_ok=True)
TOKEN_PATH.write_text(json.dumps(tokens, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("TOKENS_SAVED", str(TOKEN_PATH))
print("expires_in", tokens["expires_in"])
print("scope", tokens["scope"])
