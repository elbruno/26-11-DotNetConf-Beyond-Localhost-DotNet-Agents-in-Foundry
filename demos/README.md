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

Use an existing authorized Foundry project and a deployed model. From the bundle root in **PowerShell 7**, run the interactive helper in the terminal you will use for the demos:

```powershell
& .\demos\Set-DemoEnvironment.ps1
```

Enter real values only in your own terminal, never in Copilot or recorded capture. All configuration inputs are masked. The helper validates every required input before changing anything and never prints or writes entered values to files. It configures `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` for both programs, with no implicit model fallback.

Choose **1** to preserve the existing `DefaultAzureCredential` chain without changing any credential variables. This does not force Azure CLI authentication: existing environment credentials can take precedence over CLI sign-in. Choose **2** to supply `AZURE_TENANT_ID`, `AZURE_CLIENT_ID` and `AZURE_CLIENT_SECRET` for a service principal. Use the existing tenant/application GUIDs and the secret **value**, not its identifier. This option sets those three variables but does not clear other credential settings. Selectors such as `AZURE_TOKEN_CREDENTIALS` may restrict which credentials run. Neither choice authenticates, grants access or verifies the identity; perform any approved sign-in separately. API-key authentication is not used by these programs.

The helper changes only the **current PowerShell process**, never User/Machine environment settings. Use `&` as shown, not `pwsh -File`, which configures a short-lived child process. Child processes such as `dotnet` and `azd` inherit the values. Run the helper separately in any other terminal that needs the configuration; already-open terminals do not inherit later changes. Process environment values are plaintext, not an encrypted credential store. Close configured terminals when finished, avoid environment dumps/transcripts, and do not put credentials in source files or commit `.azure`, `.env` or local settings.

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

The local host is for development only. The installed AgentServer SDK uses `ListenAnyIP` on port 8088, so it binds all network interfaces and may be reachable from the LAN, not just localhost. Use a controlled recording network with existing inbound firewall protection, no public exposure and no tunnels. Review that protection before running; these instructions do not change operating-system/firewall settings. `ASPNETCORE_URLS` alone does not override this SDK listener. Stop the host when finished and never treat it as a production authentication boundary. A listening HTTP process or successful readiness request does not prove model inference, conversation persistence or cloud deployment.

## Hosting version boundary

The hosting package is pinned to `1.27.0-preview.261030.1`, which requires **Responses protocol 2.0.0**, unlike older samples using 1.0. The official package release notes describe this breaking change. The three registrations remain `AgentHost.CreateBuilder()`, `AddFoundryResponses` and `RegisterProtocol`. The demo intentionally accepts no command-line arguments.

The committed `azure.yaml` describes the local startup command, code runtime and protocol. It is not evidence that an Azure environment exists or a deployment succeeded. Before cloud use, select and initialize an approved existing Foundry project with the current `azd ai agent init --help` workflow, confirm identity/model permissions and review generated configuration. Do not initialize over this source folder without preserving it. `azd provision`, `azd deploy` and `azd down` respectively create billable resources, publish a version and delete resources. They are not build commands and must not be run without explicit authorization.

Official references: [hosting integration](https://learn.microsoft.com/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp), [hosted-agent concepts](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents), [current Hello World manifest](https://github.com/microsoft-foundry/foundry-samples/blob/main/samples/csharp/hosted-agents/agent-framework/hello-world/azure.yaml).
