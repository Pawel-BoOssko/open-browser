"""OAuth 2.0 PKCE authorization for LinkedIn API.

Opens a browser for the user to authorize the app, then catches the
callback via a temporary local HTTP server on port 8787. Exchanges the
authorization code for tokens and saves them locally.

Prerequisites:
- Register an app at https://www.linkedin.com/developers/apps
- Add http://localhost:8787/callback to the app's redirect URLs
- Request scopes: openid, profile, email, w_member_social
- Save client_id and client_secret to config/local/linkedin/linkedin_client.json

Usage: python linkedin_auth.py

Output: config/local/linkedin/linkedin_tokens.json
"""
import base64
import hashlib
import json
import secrets
import sys
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

CLIENT_CONFIG = Path("config/local/linkedin/linkedin_client.json")
TOKEN_PATH = Path("config/local/linkedin/linkedin_tokens.json")
REDIRECT_URI = "http://localhost:8787/callback"
AUTH_ENDPOINT = "https://www.linkedin.com/oauth/v2/authorization"
TOKEN_ENDPOINT = "https://www.linkedin.com/oauth/v2/accessToken"
SCOPE = "openid profile email w_member_social"
PORT = 8787

if not CLIENT_CONFIG.exists():
    print("CLIENT_CONFIG_MISSING", str(CLIENT_CONFIG))
    print("Create: config/local/linkedin/linkedin_client.json with client_id and client_secret")
    raise SystemExit(1)

cfg = json.loads(CLIENT_CONFIG.read_text(encoding="utf-8"))
CLIENT_ID = cfg["client_id"]
CLIENT_SECRET = cfg.get("client_secret", "")

# --- PKCE ---
code_verifier = secrets.token_urlsafe(64)
code_challenge = (
    base64.urlsafe_b64encode(hashlib.sha256(code_verifier.encode("ascii")).digest())
    .rstrip(b"=")
    .decode("ascii")
)
state = secrets.token_urlsafe(16)

params = urllib.parse.urlencode(
    {
        "response_type": "code",
        "client_id": CLIENT_ID,
        "redirect_uri": REDIRECT_URI,
        "state": state,
        "code_challenge": code_challenge,
        "code_challenge_method": "S256",
    }
)
# LinkedIn requires %20 for scope spaces, not +
scope_encoded = urllib.parse.quote(SCOPE, safe="")
auth_url = f"{AUTH_ENDPOINT}?{params}&scope={scope_encoded}"

print("Opening browser for LinkedIn authorization...")
print("If the browser doesn't open, visit:")
print(auth_url)
webbrowser.open(auth_url)

# --- Temporary callback server ---
_callback_result = {"auth_code": None, "received_state": None}


class CallbackHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        qs = urllib.parse.parse_qs(parsed.query)

        if parsed.path == "/callback":
            callback_state = qs.get("state", [None])[0]
            if callback_state != state:
                body = "<h1>Invalid request</h1>"
            else:
                _callback_result["auth_code"] = qs.get("code", [None])[0]
                _callback_result["received_state"] = callback_state
                error = qs.get("error", [None])[0]

                if error:
                    body = f"<h1>Authorization failed</h1><p>{error}</p><p>You can close this window.</p>"
                    print("AUTH_ERROR", error, qs.get("error_description", [""])[0])
                elif _callback_result["auth_code"]:
                    body = "<h1>Authorization successful!</h1><p>You can close this window.</p>"
                    print("AUTH_CODE_RECEIVED")
                else:
                    body = "<h1>No authorization code</h1><p>Something went wrong.</p>"

            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.end_headers()
            self.wfile.write(f"<html><body>{body}</body></html>".encode("utf-8"))
        else:
            self.send_response(404)
            self.end_headers()

    def log_message(self, fmt, *args):
        pass  # silence server logs


server = HTTPServer(("localhost", PORT), CallbackHandler)
server.timeout = 5  # per-request timeout
deadline = 180  # total 3 minutes
elapsed = 0
print(f"Waiting for authorization on http://localhost:{PORT}/callback ...")
while not _callback_result["auth_code"] and elapsed < deadline:
    try:
        server.handle_request()
        elapsed += 5
    except KeyboardInterrupt:
        break
server.server_close()

if not _callback_result["auth_code"]:
    print("NO_AUTH_CODE")
    raise SystemExit(1)

auth_code = _callback_result["auth_code"]

# --- Exchange code for tokens ---
import subprocess

token_body = (
    f"grant_type=authorization_code"
    f"&code={urllib.parse.quote(auth_code, safe='')}"
    f"&redirect_uri={urllib.parse.quote(REDIRECT_URI, safe='')}"
    f"&client_id={urllib.parse.quote(CLIENT_ID, safe='')}"
    f"&client_secret={urllib.parse.quote(CLIENT_SECRET, safe='')}"
    f"&code_verifier={urllib.parse.quote(code_verifier, safe='')}"
)

print("Exchanging code for tokens...")
result = subprocess.run(
    ["curl", "-s", "-w", "\n%{http_code}", "-X", "POST",
     TOKEN_ENDPOINT,
     "-H", "Content-Type: application/x-www-form-urlencoded",
     "-d", token_body],
    capture_output=True, text=True, timeout=30
)

output = result.stdout.strip()
lines = output.rsplit("\n", 1)
if len(lines) == 2:
    response_body, http_code = lines
else:
    response_body, http_code = output, "?"

if http_code == "200":
    tokens = json.loads(response_body)
else:
    print("TOKEN_ERROR", http_code, response_body[:1000])
    raise SystemExit(1)

TOKEN_PATH.parent.mkdir(parents=True, exist_ok=True)
TOKEN_PATH.write_text(json.dumps(tokens, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("TOKENS_SAVED", str(TOKEN_PATH))
print("expires_in", tokens.get("expires_in"))
print("scope", tokens.get("scope"))
