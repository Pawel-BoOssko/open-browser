"""Interactive LinkedIn setup — asks for Client ID and Secret, saves config, runs auth.

Usage: python linkedin_setup.py
"""
import json
import subprocess
import sys
from pathlib import Path

CLIENT_CONFIG = Path("config/local/linkedin/linkedin_client.json")

print("=" * 50)
print("LinkedIn App Setup for Open Bridge")
print("=" * 50)
print()
print("Before running this, register an app at:")
print("  https://www.linkedin.com/developers/apps")
print()
print("Required:")
print("  1. Add Products: Share on LinkedIn + Sign In with OpenID Connect")
print("  2. In Auth tab, add Redirect URL: http://localhost:8787/callback")
print("  3. Copy your Client ID and Client Secret from the Auth tab")
print()

client_id = input("Client ID: ").strip()
client_secret = input("Client Secret: ").strip()

if not client_id or not client_secret:
    print("ERROR: Both Client ID and Client Secret are required.")
    raise SystemExit(1)

CLIENT_CONFIG.parent.mkdir(parents=True, exist_ok=True)
config = {"client_id": client_id, "client_secret": client_secret}
CLIENT_CONFIG.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print()
print(f"Config saved: {CLIENT_CONFIG}")
print()

run_auth = input("Run authorization now? (opens browser) [Y/n]: ").strip().lower()
if run_auth in ("", "y", "yes"):
    print("Opening browser for LinkedIn authorization...")
    auth_script = str(Path(__file__).parent / "linkedin_auth.py")
    result = subprocess.run([sys.executable, auth_script])
    if result.returncode == 0:
        print()
        print("Setup complete! Post a test:")
        print(f'python {str(Path(__file__).parent / "linkedin_post.py")} "Hello from Open Bridge"')
    else:
        print()
        print("Authorization did not complete. Check errors above.")
        print("You can re-run: python tools\\linkedin\\linkedin_auth.py")
else:
    print()
    print("Run authorization later:")
    print("  python tools\\linkedin\\linkedin_auth.py")
