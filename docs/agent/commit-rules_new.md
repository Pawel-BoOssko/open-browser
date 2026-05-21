# Commit Rules

Purpose:
Defines the conditions a developer agent must verify before recommending or performing a Git commit.

Depends on:
- docs/templates/document-header-template_new.md
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/agent/coding-invariants_new.md
- docs/agent/development-rules_new.md
- docs/feedback/field-notes_new.md
- docs/development/rules.md
- docs/development/decisions.md
- docs/development/journal.md
- docs/development/README.md

Impacts:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/agent/documentation-update-checklist_new.md
- docs/templates/document-header-template_new.md
- future Git hook / pre-commit documentation gate
- all developer-agent final reports that mention COMMIT or DO NOT COMMIT

Update rule:
Update this file when commit gating, commit recommendation rules, Git hook behavior, or documentation checks before commit change.

Latest:
- Source migration from docs/development/rules.md, decisions.md, journal.md, README.md is reflected here.
- A developer agent must not recommend COMMIT unless documentation impact has been checked.
- A developer agent must not recommend COMMIT if changed behavior requires documentation updates that were not made.
- The commit decision must be based on current repository state, not memory from the conversation.
- The owner/architect does not need to understand Git internals; the agent must provide a clear COMMIT or DO NOT COMMIT recommendation.
- Never use --no-verify.
- Never run git push unless explicitly instructed.

## Required checks before recommending COMMIT

Before recommending COMMIT, verify:

1. git status --short was run and understood.
2. Existing dirty state was identified before adding new changes.
3. Git diff was inspected for the intended commit scope.
4. Build or relevant validation was run, or a reason is given why it was not run.
5. Documentation impact was checked using docs/agent/documentation-update-checklist_new.md.
6. Coding invariants were checked using docs/agent/coding-invariants_new.md.
7. Field notes were checked for relevant open observations.
8. No unrelated files were changed without explanation.
9. No stale or rejected architecture was reintroduced.
10. Removed features have no remaining active-file ghosts.
11. config/local files are not included.
12. The commit is one logical package.

## Commit scope rules

Commits should be small and scoped to one logical package.

Do not mix:
- runtime code changes;
- documentation migration;
- unrelated test artifacts;
- local config;
- experimental files;
- generated downloads;
- unrelated cleanup.

## Required final recommendation format

Use exactly one of:

- COMMIT
- DO NOT COMMIT

Also state:

- reason;
- changed files intended for commit;
- files excluded from commit, if any;
- documentation impact: yes/no;
- documentation files updated: list or none;
- invariants checked: yes/no;
- field notes checked: yes/no;
- validation result;
- recommended commit message, if COMMIT.

## Commit must be rejected when

Recommend DO NOT COMMIT if:

- build or required validation fails;
- dirty repo state contains unexplained changes;
- unrelated files are mixed into the commit;
- documentation is stale after behavior changes;
- coding invariants are violated;
- field notes reveal unresolved blocking issues;
- config/local files would be committed;
- removed-feature ghosts remain in active files;
- the agent is unsure whether the change is safe.

## Git prohibitions

Never:
- run git push unless explicitly instructed;
- use --no-verify;
- amend other people's commits;
- commit config/local files;
- commit secrets;
- commit unrelated generated downloads;
- expose Open Browser to the Internet or document it as Internet-safe.

## Hook rule

The local pre-commit hook may remind the agent to apply docs/agent/READ_BEFORE_WORK_new.md.
The hook does not replace agent responsibility for checking documentation, invariants, field notes, and commit scope.