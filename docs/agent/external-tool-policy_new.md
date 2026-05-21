# External Tool Policy

Purpose: Defines how models should treat external tools and services differently from local filesystem operations.

Depends on:
- docs/agent/READ_BEFORE_WORK_new.md
- docs/runtime/hst-help_new.md
- docs/runtime/modules-index_new.md
- docs/agent/model-feedback-policy_new.md

Impacts:
- tool README files
- tool help commands
- model operator behavior
- external service automation

Update rule: Update this file when retry behavior, external service boundaries, tool execution policy, or service-specific risk changes.

Latest:
- Local filesystem retries are cheap.
- External service retries are not cheap and may affect accounts, rate limits, API quotas, moderation systems, or anti-spam systems.
- Models must not blindly repeat external actions.

## Local operations

For local filesystem and local script operations, repeated inspection or verification is usually acceptable.

## External operations

External services include social platforms, APIs, websites, form automation, publishing tools, and account-based systems.

For external services:
- avoid blind retry loops;
- after repeated failure, stop and report the observed state;
- do not keep submitting posts, forms, messages, or API calls without understanding the previous result;
- distinguish read-only checks from write/publish actions.

## Tool documentation requirement

Each external tool README should say:
- what the tool can do;
- which actions are read-only;
- which actions publish, send, modify, or call external systems;
- what the model should verify before write/publish actions;
- where outputs/logs are stored.
