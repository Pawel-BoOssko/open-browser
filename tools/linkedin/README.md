# tools/linkedin/

**Purpose:** Python scripts for posting to LinkedIn via OAuth 2.0 and LinkedIn REST API. Uses the official LinkedIn Python client for reliable token exchange. Callback via local HTTP server on port 8787.

**Created by:** Claude Code, 2026-05-18.

**Prerequisite:** `pip install linkedin-api-client`

**Rules:**
1. First-time setup requires human interaction (browser authorization). After that, the model can post autonomously.
2. Secrets are in `config/local/linkedin/` (git-ignored).
3. Export files go to `tools/linkedin/exports/`.

**Scripts:**
| Script | Purpose | Usage |
|--------|---------|-------|
| `linkedin_auth.py` | One-time OAuth authorization | `python linkedin_auth.py` |
| `linkedin_post.py` | Post text or article to LinkedIn | `python linkedin_post.py "text"` or `--article "Title" "Body"` |

**Setup checklist:**
1. Register an app at https://www.linkedin.com/developers/apps
2. Add `http://localhost:8787/callback` to redirect URLs
3. Request scopes: `openid`, `profile`, `email`, `w_member_social`
4. Create `config/local/linkedin/linkedin_client.json`:
   ```json
   {"client_id": "your_client_id", "client_secret": "your_client_secret"}
   ```
5. Run `python linkedin_auth.py` — opens browser, you authorize, tokens saved
6. Test: `python linkedin_post.py "Test post from Open Bridge"`

**LinkedIn API notes:**
- Uses OAuth 2.0 with PKCE (S256)
- Tokens auto-refresh when expired
- Posts API: `POST /rest/posts` with `LinkedIn-Version: 202411`
- Article mode uses `content.article` for longer posts with title
