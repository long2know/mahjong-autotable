#requires -Version 5.1
[CmdletBinding(PositionalBinding = $false)]
param(
    [Alias('Tag')]
    [ValidateNotNullOrEmpty()]
    [string] $ImageTag = $(if ($env:MAHJONG_IMAGE) { $env:MAHJONG_IMAGE } else { 'mahjong-autotable:local' }),
    [ValidateNotNullOrEmpty()]
    [string] $Platform = $(if ($env:MAHJONG_PLATFORM) { $env:MAHJONG_PLATFORM } else { 'linux/amd64' }),
    [string] $Archive,
    [switch] $NoCache,
    [switch] $Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if ($Help) {
    Write-Output 'Usage: ./build.ps1 [-Tag IMAGE] [-Platform linux/ARCH] [-Archive FILE] [-NoCache]'
    Write-Output 'Builds and loads a Linux image locally. Does not deploy or push.'
    Write-Output 'Defaults: MAHJONG_IMAGE=mahjong-autotable:local, MAHJONG_PLATFORM=linux/amd64.'
    Write-Output 'Build identity defaults to Git commit[-dirty]-UTC timestamp (or local-source-unavailable-timestamp).'
    Write-Output 'BUILD_SHA overrides that public identity exactly; no runtime secrets are needed.'
    Write-Output '-Archive writes raw tar; use gzip separately for a .tar.gz archive.'
    exit 0
}
if ($Platform -notmatch '^linux/[^,/]+(/[^,/]+)?$') {
    Write-Error 'Specify one Linux platform, for example linux/amd64 or linux/arm64.' -ErrorAction Continue
    exit 2
}
if ($Archive -and (Test-Path -LiteralPath $Archive)) {
    Write-Error "Archive already exists; refusing to overwrite: $Archive" -ErrorAction Continue
    exit 2
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error 'Docker with the Buildx plugin is required.' -ErrorAction Continue
    exit 127
}
& docker buildx version > $null
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$buildSha = $env:BUILD_SHA
if ($null -eq $buildSha) {
    $buildTime = [DateTime]::UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", [Globalization.CultureInfo]::InvariantCulture)
    $buildSha = "local-source-unavailable-$buildTime"
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $commit = $null
        try {
            $candidate = & git -C $PSScriptRoot rev-parse --verify HEAD 2>$null
            if ($LASTEXITCODE -eq 0) { $commit = $candidate }
        } catch {
            # Older PowerShell can throw on native stderr for a source export without .git.
        }
        if ($null -ne $commit) {
            $changes = & git --no-optional-locks -C $PSScriptRoot status --porcelain --untracked-files=normal
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
            $dirty = if ($changes) { '-dirty' } else { '' }
            $buildSha = "$commit$dirty-$buildTime"
        }
    }
}
Write-Output "Build identity: $buildSha"
$buildArguments = @(
    'buildx', 'build', '--load', '--platform', $Platform, '--tag', $ImageTag,
    '--file', (Join-Path $PSScriptRoot 'Dockerfile'), '--build-arg', "BUILD_SHA=$buildSha"
)
if ($NoCache) { $buildArguments += '--no-cache' }
$buildArguments += $PSScriptRoot
& docker @buildArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$imageOs = & docker image inspect $ImageTag --format '{{.Os}}'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($imageOs -ne 'linux') {
    Write-Error "Expected a locally loaded Linux image; got OS=$imageOs" -ErrorAction Continue
    exit 1
}
& docker image inspect $ImageTag --format 'Built {{.Id}} ({{.Os}}/{{.Architecture}})'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Archive) {
    & docker image save --output $Archive $ImageTag
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output "Saved image archive: $Archive"
}
