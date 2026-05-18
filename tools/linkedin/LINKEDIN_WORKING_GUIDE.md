# Working with LinkedIn from Open Browser

This guide covers posting to LinkedIn via the REST API using the scripts in `tools/linkedin/`.

## First-time setup (one-time, requires human)

### 1. Register a LinkedIn app

1. Go to https://www.linkedin.com/developers/apps
2. Create an app — choose a name, associate it with a LinkedIn Company Page (required, even for individual devs — create a free Company Page if needed)
3. Under **Products**, add:
   - **Sign In with LinkedIn using OpenID Connect**
   - **Share on LinkedIn**
4. Under **Auth**, add redirect URL: `http://127.0.0.1:8787/callback`
5. Copy **Client ID** and **Client Secret**

### 2. Verify the app with your Company Page

1. Go to app **Settings**
2. Click **Verify** — generates a verification URL
3. Open the verification URL while logged in as the Company Page admin
4. Approve the association
5. Wait a few minutes for the verification to propagate

### 3. Install the official LinkedIn Python client

The token exchange MUST use the official LinkedIn client. Manual HTTP requests to the token endpoint consistently fail with "invalid_client" even when all parameters are correct. The official client works immediately.

```powershell
pip install linkedin-api-client
```

### 4. Save credentials

Create `config\local\linkedin\linkedin_client.json`:
```json
{"client_id": "...", "client_secret": "..."}
```

### 5. Authorize

```powershell
python tools\linkedin\linkedin_auth.py
```

Opens a browser. Log in, click Allow. Tokens are saved automatically to `config/local/linkedin/linkedin_tokens.json`.

## Posting (autonomous — no human needed after setup)

```powershell
python tools\linkedin\linkedin_post.py "Your post text"
python tools\linkedin\linkedin_post.py --article "Title" "Body"
```

## Technical notes (lessons learned — do not repeat our mistakes)

### Token exchange

- **Our manual token exchange never worked.** urllib, curl, Basic Auth, form-encoded body — all returned 401 "invalid_client".
- **The official `linkedin-api-client` works immediately.** Use `AuthClient.exchange_auth_code_for_access_token()`.
- Authorization step always worked — proving client_id was correct. Token exchange is stricter.

### API version for posting

- Posts API requires header `LinkedIn-Version: 202604` (YYYYMM format).
- Also requires `X-RestLi-Protocol-Version: 2.0.0`.
- Test before use — not all documented versions are active. `202605` returned 426 even though docs list it.

### Redirect URI

- Use `http://127.0.0.1:8787/callback` — not `localhost`.
- Must be exactly the same in auth URL, token exchange, and LinkedIn Developer Portal.
- Local HTTP server on port 8787 catches the OAuth callback.

### App verification

- Adding a product is NOT enough. The app must be verified with a Company Page.
- "Development mode" apps may not be able to exchange tokens.
- Multiple active client secrets cause confusion — keep only ONE.
- After verification, wait a few minutes before testing.

### Troubleshooting checklist

1. `invalid_client` at token exchange → use official client, verify app with Company Page, check redirect URI matches exactly
2. `invalid_scope_error` → check Products are active, try with `openid profile email w_member_social`
3. `NONEXISTENT_VERSION` → try different API version (e.g., `202604` instead of `202605`)
4. App verification → Settings → Verify button → generate URL → approve as Page admin
