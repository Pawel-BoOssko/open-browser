param([Parameter(Mandatory = $true)][string]$ManifestPath)
$ErrorActionPreference = 'Stop'
$file = (Resolve-Path -LiteralPath $ManifestPath).Path
$m = Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json
if ($m.schema -ne 1 -or $m.channel -notin @('staged','live','test')) {
    throw 'Unsupported release manifest schema/channel'
}
if (-not $m.executable -or
    $m.executable -ne [IO.Path]::GetFileName([string]$m.executable)) {
    throw 'Manifest executable must be a basename'
}
$exe = Join-Path (Split-Path -Parent $file) $m.executable
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'Manifest executable is missing'
}
$embedded = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
$pattern = '^(?<ver>\d+\.\d+\.\d+)\+build\.(?<stamp>\d{8}T\d{6}Z)(?:\.(?<sha>[0-9a-fA-F]{7,40}))?$'
if ($embedded -notmatch $pattern) { throw 'Invalid embedded build identity' }
$version = $Matches['ver']
$stamp = $Matches['stamp']
$source = $Matches['sha']
$utc = [DateTime]::ParseExact($stamp, "yyyyMMdd'T'HHmmss'Z'",
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
$tz = [TimeZoneInfo]::FindSystemTimeZoneById('Central European Standard Time')
$warsaw = [TimeZoneInfo]::ConvertTimeFromUtc($utc, $tz)
if ($m.version -ne $version -or
    $m.buildUtc -ne $utc.ToString('yyyy-MM-ddTHH:mm:ssZ') -or
    $m.buildWarsaw -ne $warsaw.ToString('yyyy-MM-ddTHH:mm:ss') -or
    $m.buildWarsawOffset -ne $tz.GetUtcOffset($utc).ToString() -or
    $m.sourceCommit -ne $source) {
    throw 'Manifest identity does not match embedded EXE identity'
}
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if ([string]$m.executableSha256 -ne $hash.ToLowerInvariant()) {
    throw 'Manifest SHA256 mismatch'
}
Write-Output ('RELEASE_IDENTITY_OK v' + $version + ' sha256=' + $hash.ToLowerInvariant())
