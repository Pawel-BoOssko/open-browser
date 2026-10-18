param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [ValidateSet('staged','live','test')][string]$Channel = 'staged'
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$version = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
$pattern = '^(?<version>\d+\.\d+\.\d+)\+build\.(?<stamp>\d{8}T\d{6}Z)(?:\.(?<sha>[0-9a-fA-F]{7,40}))?$'
if ($version -notmatch $pattern) { throw 'Embedded build identity is missing or malformed' }
$semver = $Matches['version']
$stamp = $Matches['stamp']
$source = $Matches['sha']
$built = [DateTime]::ParseExact($stamp, "yyyyMMdd'T'HHmmss'Z'",
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
$built = $built.ToUniversalTime()
$zone = [TimeZoneInfo]::FindSystemTimeZoneById('Central European Standard Time')
$local = [TimeZoneInfo]::ConvertTimeFromUtc($built, $zone)
$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    schema = 1
    channel = $Channel
    version = $semver
    buildUtc = $built.ToString('yyyy-MM-ddTHH:mm:ssZ')
    buildWarsaw = $local.ToString('yyyy-MM-ddTHH:mm:ss')
    buildWarsawOffset = $zone.GetUtcOffset($built).ToString()
    sourceCommit = $source
    executable = [IO.Path]::GetFileName($exe)
    executableSha256 = $hash
}
$json = $manifest | ConvertTo-Json -Depth 3
$target = [IO.Path]::GetFullPath($ManifestPath)
$folder = [IO.Path]::GetDirectoryName($target)
if (-not (Test-Path -LiteralPath $folder -PathType Container)) {
    throw 'Manifest destination folder does not exist'
}
if (Test-Path -LiteralPath $target) {
    $existing = Get-Content -LiteralPath $target -Raw -Encoding UTF8
    if ($existing.Trim() -ne $json.Trim()) {
        throw 'Manifest already exists with different metadata'
    }
    Write-Output 'MANIFEST_VERIFIED'
    return
}
$tmp = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($tmp, $json + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $tmp -Destination $target -ErrorAction Stop
} finally {
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
}
Write-Output 'MANIFEST_CREATED'
