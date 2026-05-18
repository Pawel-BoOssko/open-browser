# Claude Code launcher for DeepSeek Anthropic-compatible API.
# Run from any project folder:
#   D:\projects\open-browser\tools\claude\claude-deepseek.ps1
#   D:\projects\open-browser\tools\claude\claude-deepseek.ps1 -p "short prompt"
#   D:\projects\open-browser\tools\claude\claude-deepseek.ps1 --resume <session-id>

$ErrorActionPreference = "Stop"

try {
    [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
    chcp 65001 | Out-Null
}
catch {
    Write-Warning "UTF-8 console setup failed: $($_.Exception.Message)"
}

if (-not $env:DEEPSEEK_API_KEY) {
    $userKey = [Environment]::GetEnvironmentVariable("DEEPSEEK_API_KEY", "User")
    $machineKey = [Environment]::GetEnvironmentVariable("DEEPSEEK_API_KEY", "Machine")

    if ($userKey) {
        $env:DEEPSEEK_API_KEY = $userKey
    }
    elseif ($machineKey) {
        $env:DEEPSEEK_API_KEY = $machineKey
    }
}

if (-not $env:DEEPSEEK_API_KEY) {
    throw "DEEPSEEK_API_KEY is missing. Set it as a User or Machine environment variable."
}

$env:ANTHROPIC_BASE_URL = "https://api.deepseek.com/anthropic"
$env:ANTHROPIC_API_KEY = $env:DEEPSEEK_API_KEY
$env:ANTHROPIC_AUTH_TOKEN = $env:DEEPSEEK_API_KEY
$env:ANTHROPIC_MODEL = "deepseek-v4-pro[1m]"
$env:ANTHROPIC_DEFAULT_OPUS_MODEL = "deepseek-v4-pro[1m]"
$env:ANTHROPIC_DEFAULT_SONNET_MODEL = "deepseek-v4-pro[1m]"
$env:ANTHROPIC_DEFAULT_HAIKU_MODEL = "deepseek-v4-pro[1m]"
$env:CLAUDE_CODE_SUBAGENT_MODEL = "deepseek-v4-pro[1m]"

claude @args
