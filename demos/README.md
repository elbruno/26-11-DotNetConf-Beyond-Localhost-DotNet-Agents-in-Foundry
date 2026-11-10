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

Some local examples use `AzureCliCredential`. If Azure CLI is installed but a child process cannot find it, prepend its directory to `PATH` in the same PowerShell 7 terminal before launching the demo:

```powershell
$azCliPath = (Get-Command az.cmd -ErrorAction Stop).Source
$env:PATH = "$(Split-Path -Parent $azCliPath);$env:PATH"
```

This affects only the current PowerShell process and its child processes. It does not change the user or machine `PATH` or sign in to Azure.
