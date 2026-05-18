# Working with LinkedIn from Open Browser

This guide covers posting to LinkedIn via the REST API using the scripts in `tools/linkedin/`.

## First-time setup (one-time, requires human)

The human operator must:

1. Register an app at https://www.linkedin.com/developers/apps
2. Add `http://localhost:8787/callback` to the app's Authorized Redirect URLs
3. Under Products, request "Share on LinkedIn" and "Sign In with LinkedIn using OpenID Connect"
4. Create the client config at `config/local/linkedin/linkedin_client.json`:
   ```json
   {"client_id": "...", "client_secret": "..."}
   ```
5. Run the authorization script:
   ```
   python tools\linkedin\linkedin_auth.py
   ```
   This opens a browser — the human clicks "Allow". Tokens are saved automatically.

After this step, no more human interaction is needed.

## Posting (autonomous)

### Simple text post
```
python tools\linkedin\linkedin_post.py "Your post text here"
```

### Article-style post (with title)
```
python tools\linkedin\linkedin_post.py --article "My Title" "The body of the article goes here"
```

## Checking if setup is complete

Verify the auth exists:
```
Test-Path config\local\linkedin\linkedin_tokens.json
```

If `True` — you're ready to post. If `False` — the human needs to run `linkedin_auth.py`.

## Token refresh

Access tokens expire after ~2 hours. The post script refreshes them automatically — you don't need to re-authorize. The refresh token is valid for 12 months.

## Troubleshooting

- **"CLIENT_CONFIG_MISSING"** — the human operator hasn't set up the client config yet. Remind them.
- **"TOKENS_MISSING"** — run `linkedin_auth.py` first.
- **"POST_ERROR 401"** — tokens expired, script will auto-refresh on next try.
- **"POST_ERROR 403"** — app doesn't have `w_member_social` scope or the product isn't approved.
