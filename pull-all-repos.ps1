param(
    [switch]$IncludeSubmodules = $true
)

# Repositories live alongside Scrubbler-2 in the script directory's parent.
$root = Split-Path -Parent $PSScriptRoot
$repos = Get-ChildItem -Path $root -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName ".git") } |
    Sort-Object Name

if ($repos.Count -eq 0) {
    Write-Host "No git repositories found in $root"
    exit 0
}

$summary = [ordered]@{
    Updated = 0
    Current = 0
    Skipped = 0
    Failed  = 0
}

foreach ($repo in $repos) {
    Push-Location $repo.FullName
    try {
        $branch = git rev-parse --abbrev-ref HEAD 2>$null
        $upstream = git rev-parse --abbrev-ref --symbolic-full-name "@{u}" 2>$null

        Write-Host "[$($repo.Name)] $branch" -ForegroundColor Cyan

        git fetch --prune
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  fetch failed" -ForegroundColor Red
            $summary.Failed++
            continue
        }

        if ([string]::IsNullOrWhiteSpace($upstream)) {
            Write-Host "  skipped: no upstream configured" -ForegroundColor Yellow
            $summary.Skipped++
            continue
        }

        $before = [int](git rev-list --count "HEAD..@{u}" 2>$null)

        if ($before -eq 0) {
            Write-Host "  already up to date" -ForegroundColor Green
            $summary.Current++
        }
        else {
            git pull --ff-only
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  updated: pulled $before commit(s)" -ForegroundColor Green
                $summary.Updated++
            }
            else {
                Write-Host "  pull failed: manual attention needed" -ForegroundColor Red
                $summary.Failed++
            }
        }

        if ($IncludeSubmodules) {
            git submodule update --init --recursive
            if ($LASTEXITCODE -ne 0) {
                Write-Host "  submodule update failed" -ForegroundColor Yellow
            }
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Done: $($summary.Updated) updated, $($summary.Current) current, $($summary.Skipped) skipped, $($summary.Failed) failed."

if ($summary.Failed -gt 0) {
    exit 1
}
