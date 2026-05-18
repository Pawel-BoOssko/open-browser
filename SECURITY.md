# Security

## Design

Open Browser executes local PowerShell commands. This is intentional — it is the core function of the project.

**Safety boundaries are at the operating system level:**
- Commands run under the same user account as the application
- The working directory is configurable and scoped
- No sandboxing, containerization, or process isolation (single process, single user)

## What you should know

- **Do not run Open Browser as Administrator.** Run it under your normal user account.
- **Do not expose Open Browser to the Internet.** It is designed for local use. There is no authentication, no network security layer.
- **Review what the LLM asks to execute.** While the loop is autonomous, the human operator can always close the window or kill the process.
- **Secrets** (API keys, tokens, credentials) are stored in `config/local/` which is git-ignored. Never commit files from this directory.

## Reporting

This is an alpha project. If you find a security issue, open a GitHub issue or contact the maintainer directly. Do not expect a formal security response process at this stage.

## Third-party components

- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) — browser control
- PowerShell (`System.Diagnostics.Process`) — command execution
