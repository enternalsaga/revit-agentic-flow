param(
    [string]$TraceDirectory = "src/RevitHarness/fixtures/traces",
    [string]$OutputDirectory = ".revit-harness/lesson-candidates",
    [int]$MinimumFrequency = 2
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$groups = @{}
Get-ChildItem -Path $TraceDirectory -Filter "*.json" -File | ForEach-Object {
    $trace = Get-Content -Path $_.FullName -Raw | ConvertFrom-Json
    $failures = $trace.classifiedFailures
    if (-not $failures) { return }
    foreach ($failure in $failures) {
        if ([string]::IsNullOrWhiteSpace($failure.category)) {
            continue
        }
        if (-not $groups.ContainsKey($failure.category)) {
            $groups[$failure.category] = @()
        }
        $groups[$failure.category] += [ordered]@{
            tracePath = $_.FullName
            failure = $failure
        }
    }
}

$created = @()
foreach ($category in $groups.Keys) {
    $items = @($groups[$category])
    if ($items.Count -lt $MinimumFrequency) {
        continue
    }

    $firstFailure = $items[0].failure
    $candidate = [ordered]@{
        candidateId = "{0}_{1}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $category
        createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        category = $category
        frequency = $items.Count
        supportingTracePaths = @($items | ForEach-Object { $_.tracePath })
        summary = "Repeated $category failure across $($items.Count) traces."
        generalizedLesson = $firstFailure.suggestedNextAction
        recommendedPatchTargets = @($firstFailure.patchTargets)
        risk = "Review before applying because generated candidates may overfit fixture or session-specific failures."
        requiresHumanApproval = $true
    }

    $path = Join-Path $OutputDirectory ($candidate.candidateId + ".json")
    $candidate | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding UTF8
    $created += $path
}

[ordered]@{
    success = $true
    createdCount = $created.Count
    createdPaths = $created
} | ConvertTo-Json -Depth 20
