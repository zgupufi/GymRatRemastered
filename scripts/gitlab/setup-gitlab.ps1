<#
.SYNOPSIS
    Configures the GitLab task-tracking project: labels, the Task issue template and the board columns.

.DESCRIPTION
    Run it once after creating the (empty) tracking project in GitLab. It is safe to re-run:
    anything that already exists is left alone.

    The token is read from the GITLAB_TOKEN environment variable so it never lands in a file
    or in shell history. It needs the "api" scope and at least the Maintainer role on the project.

.EXAMPLE
    $env:GITLAB_TOKEN = Read-Host "GitLab token"
    .\setup-gitlab.ps1 -ProjectPath "gymrat/tasks"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [string]$GitLabUrl = "https://gitlab.com",

    [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"

if (-not $env:GITLAB_TOKEN) {
    throw "Set the GITLAB_TOKEN environment variable first (see the example in this script's help)."
}

$headers = @{ "PRIVATE-TOKEN" = $env:GITLAB_TOKEN }
$projectApi = "$GitLabUrl/api/v4/projects/$([uri]::EscapeDataString($ProjectPath))"

function Invoke-GitLab {
    param([string]$Method, [string]$Url, [hashtable]$Body)

    $arguments = @{ Method = $Method; Uri = $Url; Headers = $headers }
    if ($Body) {
        $arguments.Body = ($Body | ConvertTo-Json)
        $arguments.ContentType = "application/json; charset=utf-8"
    }
    # Windows PowerShell returns a JSON array as one object; unroll it so callers get its items.
    Invoke-RestMethod @arguments | ForEach-Object { $_ }
}

function Get-StatusCode {
    param($ErrorRecord)

    if ($ErrorRecord.Exception.Response) {
        return [int]$ErrorRecord.Exception.Response.StatusCode
    }
    return 0
}

$project = Invoke-GitLab -Method Get -Url $projectApi
Write-Host "Configuring $($project.web_url)"

# --- Labels -----------------------------------------------------------------
$labels = [ordered]@{
    # Type
    "backend"         = "#1F75CB"
    "frontend"        = "#6699CC"
    "feature"         = "#108548"
    "bugfix"          = "#DD2B0E"
    "refactor"        = "#9400D3"
    # Status
    "ready-for-agent" = "#00B140"
    "in-progress"     = "#EDA100"
    "in-review"       = "#6E49CB"
    "blocked"         = "#C91C00"
}

foreach ($name in $labels.Keys) {
    try {
        Invoke-GitLab -Method Post -Url "$projectApi/labels" -Body @{ name = $name; color = $labels[$name] } | Out-Null
        Write-Host "  label created: $name"
    }
    catch {
        if ((Get-StatusCode $_) -ne 409) { throw }
        Write-Host "  label exists:  $name"
    }
}

# --- Issue template -----------------------------------------------------------
$templatePath = ".gitlab/issue_templates/Task.md"
$templateContent = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot "Task.md")
try {
    Invoke-GitLab -Method Post -Url "$projectApi/repository/files/$([uri]::EscapeDataString($templatePath))" -Body @{
        branch         = $Branch
        content        = $templateContent
        commit_message = "Add Task issue template"
    } | Out-Null
    Write-Host "  issue template created: $templatePath"
}
catch {
    if ((Get-StatusCode $_) -ne 400) { throw }
    Write-Host "  issue template exists:  $templatePath"
}

# --- Board columns ------------------------------------------------------------
# Open and Closed are built in; Closed is the "done" column.
$board = @(Invoke-GitLab -Method Get -Url "$projectApi/boards") | Select-Object -First 1
if (-not $board) {
    $board = Invoke-GitLab -Method Post -Url "$projectApi/boards" -Body @{ name = "Tasks" }
}

$projectLabels = @(Invoke-GitLab -Method Get -Url "$projectApi/labels?per_page=100")
$existingLists = @($board.lists | ForEach-Object { $_.label.name })

foreach ($name in "ready-for-agent", "in-progress", "in-review") {
    if ($existingLists -contains $name) {
        Write-Host "  board column exists:  $name"
        continue
    }
    $label = $projectLabels | Where-Object { $_.name -eq $name } | Select-Object -First 1
    Invoke-GitLab -Method Post -Url "$projectApi/boards/$($board.id)/lists" -Body @{ label_id = $label.id } | Out-Null
    Write-Host "  board column created: $name"
}

Write-Host "Done. Board: $($project.web_url)/-/boards"
