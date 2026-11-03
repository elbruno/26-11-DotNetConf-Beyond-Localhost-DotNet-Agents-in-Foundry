# Beyond Localhost demos

Two concrete stages of the same HelloAgent, following [the primary article](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/):

| Demo | Entry point | What changes |
|---|---|---|
| 1 | `01-console-agent\Program.cs` | One model request, printed to the terminal |
| 2 | `02-hosted-agent\Program.cs` | The same agent exposed through the Responses protocol |

## Build without a model call

From the bundle root, run `pwsh -File .\Prepare-Demos.ps1`. This uses locked package restoration and Release builds. It does not install CLIs, authenticate, invoke a model, initialize Azure resources or deploy.

The SDK pin is .NET 10.0.403 with patch roll-forward. Package versions and dependency hashes are checked into each project. `global.json` selects the SDK and each `.csproj` describes its program; JSON files have no comment syntax.

## Real model prerequisites

Use an existing authorized Foundry project and a deployed model. In PowerShell, set the following values in the current terminal only:

```powershell
$env:FOUNDRY_PROJECT_ENDPOINT = 'https://<account>.services.ai.azure.com/api/projects/<project>'
$env:AZURE_AI_MODEL_DEPLOYMENT_NAME = '<existing-model-deployment-name>'
```

Both programs require these values. There is no implicit model fallback. `DefaultAzureCredential` selects the authorized local or hosted identity. Sign in separately using your approved Azure identity workflow; do not put credentials in source files or commit `.azure`, `.env` or local settings.

From `demos`, run Demo 1:

```powershell
dotnet run --project .\01-console-agent\ConsoleAgent.csproj
```

For Demo 2, change to `02-hosted-agent` and run the already-installed Foundry azd extension:

```powershell
azd ai agent run --no-client
```

In another terminal in that same directory:

```powershell
azd ai agent invoke --local "Hello! Tell me a fun fact about .NET."
```

The local host is for development only. Do not expose port 8088 publicly. A listening HTTP process or successful health request does not prove model inference, conversation persistence or cloud deployment.

## Hosting version boundary

The hosting package is pinned to `1.27.0-preview.261030.1`, which requires **Responses protocol 2.0.0**, unlike older samples using 1.0. The official package release notes describe this breaking change. The three registrations remain `AgentHost.CreateBuilder()`, `AddFoundryResponses` and `RegisterProtocol`. The demo intentionally accepts no command-line arguments.

The committed `azure.yaml` describes the local startup command, code runtime and protocol. It is not evidence that an Azure environment exists or a deployment succeeded. Before cloud use, select and initialize an approved existing Foundry project with the current `azd ai agent init --help` workflow, confirm identity/model permissions and review generated configuration. Do not initialize over this source folder without preserving it. `azd provision`, `azd deploy` and `azd down` respectively create billable resources, publish a version and delete resources. They are not build commands and must not be run without explicit authorization.

Official references: [hosting integration](https://learn.microsoft.com/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp), [hosted-agent concepts](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents), [current Hello World manifest](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/hello-world/azure.yaml).
