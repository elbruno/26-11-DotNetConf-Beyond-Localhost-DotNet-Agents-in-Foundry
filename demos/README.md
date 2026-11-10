# Demos

These examples accompany the .NET Conf 2026 session and show local and Foundry-hosted .NET agent patterns.

| Demo | Overview |
|---|---|
| [01 Console Agent](01-console-agent/) | A local Microsoft Agent Framework console agent using an existing Foundry chat deployment. |
| [02 Host-Ready Agent](02-host-ready-agent/) | A local Responses host, ready to run with the Foundry agent tooling. |
| [03 Foundry-Hosted Agent](03-foundry-hosted-agent/) | A hosted agent with three deterministic, read-only function tools. |
| [04 Local Decision Agent](04-local-decision-agent/) | A local agent that calls the native Microsoft Decision-1 API. |
| [05 Foundry-Hosted Decision Agent](05-foundry-hosted-decision-agent/README.md) | A hosted support-routing agent using Microsoft Decision-1. |
| [10 Foundry-Hosted Kimi Agent](10-foundry-hosted-kimi-agent/README.md) | A hosted Kimi comparison using the Responses protocol and read-only tools. |

The repository uses the .NET 11 SDK. Local demos and tests target .NET 11; hosted agent projects target .NET 10 for the currently documented Foundry hosted runtime.

## Local configuration

Run `Set-DemoEnvironment.ps1` in PowerShell 7 to configure the existing endpoints and deployment names used by the local examples. The script saves these non-secret settings in an ignored local JSON file; it does not sign in, store credentials, deploy resources, or make model calls.

```powershell
& .\demos\Set-DemoEnvironment.ps1
```

Run a local console example:

```powershell
dotnet run --project .\demos\01-console-agent\ConsoleAgent.csproj
dotnet run --project .\demos\04-local-decision-agent\LocalDecisionAgent.csproj
```

To open the local Responses host for Demo 02, run `azd ai agent run` from `demos\02-host-ready-agent`. Hosted examples require separately configured Foundry resources and identities; consult their README files and current Foundry documentation before deployment.

## Windows Azure CLI lookup

With an inherited `PATH` longer than 8,191 characters, `cmd.exe` may fail to resolve Azure CLI when `AzureCliCredential` starts a child process, even when PowerShell resolves `az`. Try the normal Demo 04 command first. Use this workaround only when the failure is specifically child-process Azure CLI discovery.

From the bundle root, configure Demo 04 settings and ensure Azure CLI is already signed in. This temporarily narrows `PATH` for the current process to the detected Azure CLI and .NET directories plus Windows system directories, runs Demo 04, and restores the original value in `finally`. It does not change persistent environment settings or guarantee authentication.

**Running Demo 04 makes a request to the configured Decision-1 model and may incur Azure charges.**

```powershell
$originalPath = $env:PATH
try {
    $az = Get-Command az -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $pathDirectories = @(
        (Split-Path -Parent $az.Source)
        (Split-Path -Parent $dotnet.Source)
        (Join-Path $env:SystemRoot 'System32')
        $env:SystemRoot
    ) | Where-Object { $_ } | Select-Object -Unique

    $temporaryPath = $pathDirectories -join ';'
    if ($temporaryPath.Length -gt 8191) {
        throw 'The detected tool directories still exceed the cmd.exe PATH limit.'
    }

    $env:PATH = $temporaryPath
    Write-Host "Temporary process PATH: $($env:PATH.Length) characters"
    & $az.Source --version
    if ($LASTEXITCODE -ne 0) { throw 'Azure CLI did not run with the temporary PATH.' }
    & $dotnet.Source --version
    if ($LASTEXITCODE -ne 0) { throw '.NET SDK did not run with the temporary PATH.' }
    & $dotnet.Source run --project .\demos\04-local-decision-agent\LocalDecisionAgent.csproj
    if ($LASTEXITCODE -ne 0) { throw "Demo 04 exited with code $LASTEXITCODE." }
}
finally {
    $env:PATH = $originalPath
}
```
