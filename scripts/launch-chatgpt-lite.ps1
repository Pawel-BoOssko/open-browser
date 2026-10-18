# Open Browser ChatGPT Lite launcher (Windows PowerShell 5.1 compatible).
# Does not rebuild the app, change ChatGPT state, or launch the OpenBridge executor.
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$binary = Join-Path $repository 'src\BridgeBrowserAlpha0\bin\Release\net8.0-windows\BridgeBrowserAlpha0.exe'
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) {
    throw "ChatGPT Lite binary is missing: $binary. Build in Release mode first."
}
Start-Process -FilePath $binary -ArgumentList '--chatgpt-lite' -WorkingDirectory (Split-Path -Parent $binary)
