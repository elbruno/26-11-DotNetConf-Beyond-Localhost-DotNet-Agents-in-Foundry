# Demo 10: Foundry-Hosted Kimi Agent

This example hosts a .NET agent through the Foundry Responses protocol and adds the three deterministic, read-only tools shared with Demo 03: `GetDotNetFact`, `AddNumbers`, and `CountWords`.

The agent uses a model deployment configured by the sample. Use an existing Foundry project and deployment that you are authorized to access. This repository does not provision projects, models, telemetry, or permissions.

## Requirements

- .NET SDK 11
- Azure Developer CLI
- PowerShell 7
- An existing Foundry project and a compatible model deployment

Run the shared setup from the bundle root to configure non-secret endpoints and deployment names:

```powershell
& .\demos\Set-DemoEnvironment.ps1
```

From the `demos` directory, build the local project and run its focused tests:

```powershell
dotnet build .\10-foundry-hosted-kimi-agent\KimiAgent.csproj -c Release -p:RestoreLockedMode=true
dotnet test .\tests\KimiAgent.Tests\KimiAgent.Tests.csproj -c Release -p:RestoreLockedMode=true
```

## Run locally

From the demo folder, start the Responses host:

```powershell
azd ai agent run --no-client
```

In another terminal, invoke it locally:

```powershell
azd ai agent invoke --local --new-session --timeout 120 "Call GetDotNetFact, AddNumbers with a=19 and b=23, and CountWords on this exact text: Foundry traces show real tool calls. Report all three tool results briefly."
```

The host listens on port 8088. Use a protected network and do not expose the listener through a tunnel.

## Hosted use

For hosted use, select an existing Foundry project and configure the required environment values with the Azure Developer CLI. Verify the endpoint, project, tenant, and model deployment before publishing. Do not provision resources or broaden permissions as a workaround for access failures. Local success does not establish hosted model access.

The hosted agent can use a different identity from the local caller. Confirm that identity and its existing access with the resource owner. Traces require an already configured telemetry destination and reader access; this sample does not change content capture or permissions.

See the [hosted agent tracing guide](https://learn.microsoft.com/azure/foundry/observability/quickstarts/quickstart-tracing-hosted-agent) and [hosted-agent permissions guidance](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-permissions#account-level-access).
