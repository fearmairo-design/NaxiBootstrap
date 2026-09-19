# Issue a Naxi Pro wave code without opening the dashboard.
# The service key is deliberately NOT stored in this repo — pass it via env var.
#
# Usage:
#   $env:SUPABASE_SERVICE_KEY = "<service_role key>"   # Dashboard -> Project Settings -> API keys
#   .\scripts\new-wave-code.ps1 -Code NBTW50 -MaxUses 50                    # lifetime Pro
#   .\scripts\new-wave-code.ps1 -Code NBTM30 -MaxUses 10 -DurationDays 30   # Pro for 30 days per activation
#
# Requires the 0001/0002 migrations to be applied (see supabase/README.md).
param(
    [Parameter(Mandatory = $true)][string]$Code,
    [Parameter(Mandatory = $true)][int]$MaxUses,
    [int]$DurationDays = 0
)

$ErrorActionPreference = 'Stop'
$Code = $Code.Trim().ToUpperInvariant()
if ($Code -notmatch '^[A-Z0-9]{6}$') { Write-Error "Code must be exactly 6 characters A-Z/0-9 (the client UI enforces 6)."; exit 1 }
if ($MaxUses -lt 1) { Write-Error "MaxUses must be 1 or more."; exit 1 }

$key = $env:SUPABASE_SERVICE_KEY
if (-not $key) { Write-Error "Set the SUPABASE_SERVICE_KEY env var first (service_role key)."; exit 1 }

$base = 'https://kjvxulkfsyvmedanbsxg.supabase.co/rest/v1/pro_invites'
$headers = @{ apikey = $key; Authorization = "Bearer $key"; Prefer = 'resolution=merge-duplicates,return=representation' }
$body = @{ code = $Code; max_uses = $MaxUses; duration_days = $DurationDays } | ConvertTo-Json -Compress

try {
    $r = Invoke-WebRequest -Uri $base -Method Post -Headers $headers -ContentType 'application/json' -Body $body -UseBasicParsing -TimeoutSec 20
    $durText = if ($DurationDays -gt 0) { "Pro for $DurationDays days per activation" } else { "lifetime Pro" }
    Write-Output "OK: wave code $Code is live with $MaxUses activation(s) - $durText."
    Write-Output $r.Content
}
catch {
    $msg = $_.Exception.Message
    if ($msg -match 'max_uses|PGRST204') {
        Write-Error "The max_uses column is missing - run supabase/migrations/0001_pro_waves.sql in Dashboard -> SQL Editor first."
    }
    elseif ($msg -match '42P01|relation|pro_devices') {
        Write-Error "Schema is not migrated - run supabase/migrations/0001_pro_waves.sql first."
    }
    else {
        Write-Error "Failed: $msg"
    }
}
