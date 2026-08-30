#Requires -Version 5.1
<#
    Quota status line probe — THROWAWAY SPIKE CODE.

    Prints one line for the Claude Code status line, e.g.

        5h 12% resets in 53m · wk 37% resets Sat

    Percentages are what has been USED, matching what /usage reports. Note that this differs
    from spec.md AC-2, which calls for remaining. The divergence is deliberate and is recorded
    in the spike document.

    This exists to answer one question: does seeing remaining quota continuously change
    what I do? It is deliberately not tested, not structured for reuse, and must not
    become production code. See specs/quota-hud/spikes/statusline-probe.md.

    It does honour the one rule that is not negotiable even in a spike: it never shows a
    number it cannot back with a response it actually received and parsed.
#>

[CmdletBinding()]
param(
    # How old the cached reading may get before we go back to the network.
    # Never below 2 minutes — this is someone else's undocumented endpoint.
    [int]$RefreshMinutes = 5,

    # Past this age we refuse to show numbers at all, rather than show a stale one.
    [int]$MaxAgeMinutes = 30
)

# 'Stop' turns non-fatal errors into catchable exceptions, so a failure can't slip past a
# try/catch and leave us rendering garbage.
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 draws a progress bar during Invoke-WebRequest that costs real
# time. The status line is rendered often, so turn it off.
$ProgressPreference = 'SilentlyContinue'

# 5.1 does not always negotiate TLS 1.2 by default, and the API requires it. Without this
# the request fails with a confusing "connection closed" error.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Make sure a non-ASCII character survives the trip to whatever is reading our stdout.
# Wrapped because setting this throws in some hosts where the console is redirected.
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch { }

# The separator is built from a character code rather than typed literally. Windows
# PowerShell 5.1 reads a .ps1 file as ANSI unless it carries a byte-order mark, which turns
# a literal middle dot into "Â·". Building it from a code point sidesteps the whole issue,
# because the source file then contains nothing but ASCII.
$Separator = ' ' + [char]0x00B7 + ' '

$Endpoint  = 'https://api.anthropic.com/api/oauth/usage'
$UserAgent = 'claude-code/2.1.251'
$CacheDir  = Join-Path $env:LOCALAPPDATA 'bingo-probe'
$CacheFile = Join-Path $CacheDir 'quota-cache.json'

# Local severity thresholds, expressed in percent REMAINING even though the display shows
# percent used. Keeping the thresholds in "remaining" terms means they still read the way
# AC-4 states them, and the comparison converts once, in Format-Window.
$WarningAt  = 25
$CriticalAt = 10

# Bumped whenever the cached record's shape changes, so a cache written by an older version
# of this script is ignored instead of being misread.
$CacheVersion = 2


function Get-AccessToken {
    # Returns the OAuth token string, or $null if there isn't one to be had.
    # The token is returned to the caller and used for the request. It is never written
    # to the cache, never printed, and never included in any error text.
    $credPath = Join-Path $env:USERPROFILE '.claude\.credentials.json'
    if (-not (Test-Path -LiteralPath $credPath)) { return $null }

    try {
        $creds = Get-Content -LiteralPath $credPath -Raw | ConvertFrom-Json
    } catch {
        return $null
    }

    if ($creds.claudeAiOauth -and $creds.claudeAiOauth.accessToken) {
        return $creds.claudeAiOauth.accessToken
    }
    if ($creds.accessToken) {
        return $creds.accessToken
    }
    return $null
}


function Get-QuotaPayload {
    param([string]$Token)

    # Returns a hashtable: Ok (bool), Payload (object), Status (int).
    # Status 401/403 means "sign in"; anything else failed means "try again later".
    $headers = @{
        'Authorization'  = "Bearer $Token"
        'anthropic-beta' = 'oauth-2025-04-20'
        'Accept'         = 'application/json'
    }

    try {
        $response = Invoke-WebRequest -Uri $Endpoint -Headers $headers -UserAgent $UserAgent `
            -TimeoutSec 5 -UseBasicParsing
        return @{ Ok = $true; Payload = ($response.Content | ConvertFrom-Json); Status = 200 }
    } catch {
        $status = 0
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        return @{ Ok = $false; Payload = $null; Status = $status }
    }
}


function ConvertTo-QuotaWindows {
    param($Payload)

    # Returns a list of windows, or an empty list if nothing recognizable was found.
    # An empty list is the caller's signal to show an error, never a zero.
    $windows = @()

    # Primary form: the self-describing `limits` array.
    if ($Payload.PSObject.Properties.Name -contains 'limits' -and $Payload.limits) {
        foreach ($limit in $Payload.limits) {
            $label = $null
            if ($limit.kind -eq 'session')    { $label = '5h' }
            if ($limit.kind -eq 'weekly_all') { $label = 'wk' }

            # An unrecognized kind is ignored, not guessed at. The live payload carries
            # several unreleased window kinds and more will appear.
            if (-not $label) { continue }
            if ($null -eq $limit.percent) { continue }

            # Stored exactly as the server reports it: percent consumed, no inversion.
            $windows += [pscustomobject]@{
                Label    = $label
                Used     = [double]$limit.percent
                ResetsAt = $limit.resets_at
            }
        }
    }

    # Fallback form: the older flat keys, read only if `limits` gave us nothing.
    if ($windows.Count -eq 0) {
        $flat = @(
            @{ Key = 'five_hour'; Label = '5h' },
            @{ Key = 'seven_day'; Label = 'wk' }
        )
        foreach ($entry in $flat) {
            $node = $Payload.($entry.Key)
            if ($null -eq $node) { continue }
            if ($null -eq $node.utilization) { continue }

            $windows += [pscustomobject]@{
                Label    = $entry.Label
                Used     = [double]$node.utilization
                ResetsAt = $node.resets_at
            }
        }
    }

    return $windows
}


function Format-Reset {
    param([string]$ResetsAt)

    # Absolute when distant, relative when near. Returns $null when the server gave us no
    # reset time — a window can report a utilization with resets_at null, and in that case
    # we show the percentage and simply say nothing about the reset.
    if ([string]::IsNullOrWhiteSpace($ResetsAt)) { return $null }

    try {
        $reset = [datetimeoffset]::Parse($ResetsAt, [Globalization.CultureInfo]::InvariantCulture)
    } catch {
        return $null
    }

    $local   = $reset.ToLocalTime()
    $minutes = [int][math]::Round(($local - [datetimeoffset]::Now).TotalMinutes)

    if ($minutes -lt 0)  { return 'resetting' }
    if ($minutes -lt 60) { return "resets in ${minutes}m" }
    if ($local.Date -eq (Get-Date).Date) { return 'resets ' + $local.ToString('h:mm tt') }
    if ($minutes -lt (60 * 24 * 6))      { return 'resets ' + $local.ToString('ddd') }
    return 'resets ' + $local.ToString('MMM d')
}


function Format-Window {
    param($Window)

    $used = [int][math]::Round($Window.Used)
    $text = "$($Window.Label) $used%"

    $reset = Format-Reset -ResetsAt $Window.ResetsAt
    if ($reset) { $text = "$text $reset" }

    # The thresholds are stated in percent remaining, so convert once here rather than
    # restating them backwards and inviting an off-by-one later.
    $remaining = 100 - $used

    # ANSI colour. Salience is part of what this probe is testing — a number you never
    # notice is the failure mode we are trying to detect.
    $esc = [char]27
    if ($remaining -le $CriticalAt) { return "$esc[31m$text$esc[0m" }  # red
    if ($remaining -le $WarningAt)  { return "$esc[33m$text$esc[0m" }  # yellow
    return $text
}


function Read-Cache {
    if (-not (Test-Path -LiteralPath $CacheFile)) { return $null }
    try {
        return Get-Content -LiteralPath $CacheFile -Raw | ConvertFrom-Json
    } catch {
        return $null
    }
}


function Write-Cache {
    param($Windows)

    # Only the parsed windows and a timestamp are persisted. The raw body is not written
    # to disk: it is an authenticated response.
    if (-not (Test-Path -LiteralPath $CacheDir)) {
        New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null
    }
    $record = [pscustomobject]@{
        version   = $CacheVersion
        fetchedAt = ([datetimeoffset]::Now).ToString('o')
        windows   = $Windows
    }
    $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $CacheFile -Encoding UTF8
}


# --- main ---------------------------------------------------------------------------------

$cache    = Read-Cache
$ageMins  = [double]::PositiveInfinity

# A cache written by an older version of this script holds a different shape. Discard it
# rather than risk rendering a field that means something else now.
if ($cache -and $cache.version -ne $CacheVersion) {
    $cache = $null
}

if ($cache -and $cache.fetchedAt) {
    try {
        $ageMins = ([datetimeoffset]::Now - [datetimeoffset]::Parse($cache.fetchedAt)).TotalMinutes
    } catch {
        $ageMins = [double]::PositiveInfinity
    }
}

# Floor the refresh interval at 2 minutes no matter what was passed in.
$effectiveRefresh = [math]::Max(2, $RefreshMinutes)

$windows = $null

if ($ageMins -lt $effectiveRefresh) {
    # Cache is fresh enough. This is the common path and costs no network call.
    $windows = $cache.windows
} else {
    $token = Get-AccessToken
    if (-not $token) {
        Write-Output 'quota: sign in'
        exit 0
    }

    $result = Get-QuotaPayload -Token $token

    if ($result.Ok) {
        $parsed = ConvertTo-QuotaWindows -Payload $result.Payload
        if ($parsed.Count -eq 0) {
            # We got a response and could not recognize a single window in it. Say so.
            Write-Output 'quota: unreadable'
            exit 0
        }
        Write-Cache -Windows $parsed
        $windows = $parsed
        $ageMins = 0
    } elseif ($result.Status -eq 401 -or $result.Status -eq 403) {
        Write-Output 'quota: sign in'
        exit 0
    } else {
        # Transient. Fall back to the cache if it is still young enough to be honest about.
        if ($cache -and $cache.windows -and $ageMins -le $MaxAgeMinutes) {
            $windows = $cache.windows
        } else {
            Write-Output 'quota: unavailable'
            exit 0
        }
    }
}

if (-not $windows -or @($windows).Count -eq 0) {
    Write-Output 'quota: unavailable'
    exit 0
}

if ($ageMins -gt $MaxAgeMinutes) {
    Write-Output ('quota: stale ({0}m)' -f [int]$ageMins)
    exit 0
}

$parts = @()
foreach ($window in @($windows)) {
    $parts += Format-Window -Window $window
}

$line = $parts -join $Separator

# Show the age once the reading is older than one refresh interval, so a number that has
# quietly stopped updating cannot pass itself off as current.
if ($ageMins -ge $effectiveRefresh) {
    $line = '{0}  ({1}m old)' -f $line, [int]$ageMins
}

Write-Output $line
