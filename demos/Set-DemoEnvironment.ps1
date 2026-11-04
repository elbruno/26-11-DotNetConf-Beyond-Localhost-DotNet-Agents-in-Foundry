#requires -Version 7.0
<#
Purpose: Configure chat and Decision-1 endpoints and deployments in this terminal.
Run with & .\demos\Set-DemoEnvironment.ps1 in PowerShell 7, not pwsh -File.
Expected result: four visible, validated process settings; no files, keys or sign-in.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Setting([string]$Label, [string]$Default) {
    $prompt = if ($Default) { "$Label [$Default]" } else { $Label }
    $value = Read-Host -Prompt $prompt
    if ($value -eq '') { return $Default }
    return $value
}

$chatDefault = if ($env:AZURE_AI_MODEL_DEPLOYMENT_NAME) { $env:AZURE_AI_MODEL_DEPLOYMENT_NAME } else { 'gpt-6-luna' }
$projectEndpoint = Read-Setting 'Foundry chat project endpoint' $env:FOUNDRY_PROJECT_ENDPOINT
$modelDeployment = Read-Setting 'Existing chat model deployment' $chatDefault
$decisionEndpoint = Read-Setting 'Foundry Decision-1 resource endpoint' $env:Decision__Endpoint
$decisionDeployment = Read-Setting 'Existing Microsoft-Decision-1 deployment' $env:Decision__Deployment

# Validate all four inputs before changing any process setting.
$endpoint = $null
if ($projectEndpoint -match '[\s\p{Cc}]' -or
    -not [Uri]::TryCreate($projectEndpoint, [UriKind]::Absolute, [ref]$endpoint) -or
    $endpoint.Scheme -ne 'https' -or -not $endpoint.Host -or
    $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or
    $endpoint.AbsolutePath -notmatch '^/api/projects/[^/]+/?\z') {
    throw 'Invalid Foundry project endpoint. Use an HTTPS /api/projects/<project> URL without credentials, query or fragment. Nothing was changed.'
}
$endpoint = $null
if ($decisionEndpoint -match '[\s\p{Cc}]' -or
    -not [Uri]::TryCreate($decisionEndpoint, [UriKind]::Absolute, [ref]$endpoint) -or
    $endpoint.Scheme -ne 'https' -or -not $endpoint.Host -or
    $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or
    $endpoint.AbsolutePath -ne '/') {
    throw 'Invalid Decision-1 endpoint. Use an HTTPS native resource root without a project path, credentials, query or fragment. Nothing was changed.'
}
foreach ($deployment in @($modelDeployment, $decisionDeployment)) {
    if ($deployment -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z') {
        throw 'Invalid model deployment name. Use 1 to 128 letters, digits, dots, underscores or hyphens, starting with a letter or digit. Nothing was changed.'
    }
}

$env:FOUNDRY_PROJECT_ENDPOINT = $projectEndpoint
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = $modelDeployment
$env:Decision__Endpoint = $decisionEndpoint
$env:Decision__Deployment = $decisionDeployment
Write-Host "FOUNDRY_PROJECT_ENDPOINT = $projectEndpoint"
Write-Host "AZURE_AI_MODEL_DEPLOYMENT_NAME = $modelDeployment"
Write-Host "Decision__Endpoint = $decisionEndpoint"
Write-Host "Decision__Deployment = $decisionDeployment"
Write-Host 'Configured for this terminal and future child processes only. No keys, files, sign-in or model calls.'
