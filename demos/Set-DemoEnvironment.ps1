#requires -Version 7.0
<#
Purpose: Configure the Foundry project and model for the demos in this terminal.
Run with & .\demos\Set-DemoEnvironment.ps1 in PowerShell 7, not pwsh -File.
Expected result: two validated process variables; no files, credentials or sign-in.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectEndpoint = Read-Host -Prompt 'Foundry project endpoint'
$modelDeployment = Read-Host -Prompt 'Existing model deployment name [gpt-6-luna]'
if ($modelDeployment -eq '') { $modelDeployment = 'gpt-6-luna' }

# Validate both inputs before changing either process variable.
$endpoint = $null
if ($projectEndpoint -match '[\s\p{Cc}]' -or
    -not [Uri]::TryCreate($projectEndpoint, [UriKind]::Absolute, [ref]$endpoint) -or
    $endpoint.Scheme -ne 'https' -or -not $endpoint.Host -or
    $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or
    $endpoint.AbsolutePath -notmatch '^/api/projects/[^/]+/?\z') {
    throw 'Invalid Foundry project endpoint. Use an HTTPS /api/projects/<project> URL without credentials, query or fragment. Nothing was changed.'
}
if ($modelDeployment -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z') {
    throw 'Invalid model deployment name. Use 1 to 128 letters, digits, dots, underscores or hyphens, starting with a letter or digit. Nothing was changed.'
}

$env:FOUNDRY_PROJECT_ENDPOINT = $projectEndpoint
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = $modelDeployment
Write-Host 'Project and model configured for this terminal and future child processes only. No sign-in or model call was performed.'
