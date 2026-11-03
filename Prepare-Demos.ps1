<#
Purpose: Restore and compile four demo stages without invoking a model or deploying.
Requirements: PowerShell 7 and the .NET SDK pinned in demos\global.json.
Expected result: all agent/caller/test projects build, or an explicit tool error.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot 'demos')
try {
    foreach ($project in @(
        '01-console-agent\ConsoleAgent.csproj',
        '02-aspire-local-agent\Agent\LocalAgent.csproj',
        '03-foundry-hosted-agent\HostedAgent.csproj',
        '04-decision-agent\tests\SupportRoutingAgent.Tests\SupportRoutingAgent.Tests.csproj'
    )) {
        & dotnet restore $project --locked-mode --nologo
        if ($LASTEXITCODE -ne 0) { throw "Restore failed for $project (exit $LASTEXITCODE)" }
        & dotnet build $project --no-restore --nologo --configuration Release
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $project (exit $LASTEXITCODE)" }
    }
}
finally {
    Pop-Location
}
