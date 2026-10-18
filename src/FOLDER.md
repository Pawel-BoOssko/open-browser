# Folder Policy

Purpose: active C# and JavaScript application source undergoing relocation from the historical releases/BridgeBrowserAlpha0/src path.

Parent policy: docs/agent/folder-policy_new.md and repository FOLDER.md.

Allowed:
- application source code, project files and required source assets;
- source-only configuration templates that contain no credentials.

Forbidden:
- downloaded user documents, browser profiles, credentials, logs, generated build outputs and test artifacts;
- unrelated integrations or new architecture without owner approval.

Child folder rule:
- subfolders inherit this policy; define a local FOLDER.md when responsibility boundaries change.

Update rule:
- update this policy when the source root or its responsibility changes.
