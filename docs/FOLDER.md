# Folder Policy

Purpose: project documentation, governance, module docs, executor docs, templates, and legacy references.

Parent policy: nearest parent FOLDER.md and docs/agent/folder-policy_new.md

Allowed:
- files and subfolders matching the stated purpose;
- local operational files explicitly allowed by parent policy;
- generated folders only when this policy says they are generated output.

Forbidden:
- unrelated ad-hoc files;
- secrets unless this is an ignored local configuration area;
- generated output treated as source without explicit promotion;
- new responsibility boundaries without policy update.

Child folder rule:
- child folders inherit this policy unless they define their own FOLDER.md;
- new ambiguous child folders require policy update or architect approval.

Update rule:
- update this file when the folder purpose, allowed contents, forbidden contents, or child-folder rules change.
