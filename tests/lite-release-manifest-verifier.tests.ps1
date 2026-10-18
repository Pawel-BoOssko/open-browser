$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo 'src\BridgeBrowserAlpha0\bin\Debug\net8.0-windows\BridgeBrowserAlpha0.exe'
$verify = Join-Path $repo 'scripts\verify-lite-release-manifest.ps1'
if (-not (Test-Path -LiteralPath $source)) { throw 'Build Debug EXE first' }
$version = (Get-Item $source).VersionInfo.ProductVersion
if ($version -notmatch '^(?<v>\d+\.\d+\.\d+)\+build\.(?<stamp>\d{8}T\d{6}Z)\.(?<sha>[0-9a-fA-F]{7,40})$') {
    throw 'Unexpected EXE version format'
}
$v = $Matches['v']
$stamp = $Matches['stamp']
$sha = $Matches['sha']
$utc = [DateTime]::ParseExact($stamp, "yyyyMMdd'T'HHmmss'Z'",
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
$tz = [TimeZoneInfo]::FindSystemTimeZoneById('Central European Standard Time')
$dir = Join-Path $env:TEMP ('ob-manifest-verifier-' + [Guid]::NewGuid().ToString('N'))
New-Item -Path $dir -ItemType Directory | Out-Null
try {
    $exe = Join-Path $dir 'BridgeBrowserAlpha0.exe'
    Copy-Item -LiteralPath $source -Destination $exe
    $hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
    $m = [ordered]@{
        schema = 1; channel = 'test'; version = $v
        buildUtc = $utc.ToString('yyyy-MM-ddTHH:mm:ssZ')
        buildWarsaw = [TimeZoneInfo]::ConvertTimeFromUtc($utc, $tz).ToString('yyyy-MM-ddTHH:mm:ss')
        buildWarsawOffset = $tz.GetUtcOffset($utc).ToString()
        sourceCommit = $sha; executable = 'BridgeBrowserAlpha0.exe'
        executableSha256 = $hash
    }
    $manifest = Join-Path $dir 'openbrowser-release-manifest.json'
    function SaveFixture {
        [IO.File]::WriteAllText($manifest, ($m | ConvertTo-Json -Depth 3),
            [Text.UTF8Encoding]::new($false))
    }
    function AssertRejected([string]$label) {
        $rejected = $false
        try { & $verify -ManifestPath $manifest | Out-Null }
        catch { $rejected = $true }
        if (-not $rejected) { throw ('Expected rejection: ' + $label) }
    }
    SaveFixture
    $result = & $verify -ManifestPath $manifest
    if ($result -notmatch '^RELEASE_IDENTITY_OK') { throw 'Valid manifest rejected' }
    Write-Output 'VALID_PASS'
    $m.executableSha256 = '0' * 64
    SaveFixture
    AssertRejected 'SHA tampering'
    Write-Output 'HASH_TAMPER_PASS'
    $m.executableSha256 = $hash
    $m.buildUtc = '2000-01-01T00:00:00Z'
    SaveFixture
    AssertRejected 'build timestamp tampering'
    Write-Output 'TIMESTAMP_TAMPER_PASS'
    $m.buildUtc = $utc.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $m.executable = '..\BridgeBrowserAlpha0.exe'
    SaveFixture
    AssertRejected 'path traversal'
    Write-Output 'PATH_TRAVERSAL_PASS'
} finally {
    Remove-Item -LiteralPath $dir -Recurse -Force
}
