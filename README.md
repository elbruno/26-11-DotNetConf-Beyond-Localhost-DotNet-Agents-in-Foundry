# Beyond Localhost: .NET Agents in Foundry

Audience-safe source bundle accompanying the session. Follow [the demo README](demos/README.md) for five stages: simple MAF agent, host-ready/not deployed, same agent intended for Foundry deployment, local MAF agent calling Foundry Microsoft-Decision-1, and that decision agent intended for Foundry hosting. No Ollama dependency is used.

Run `pwsh -File .\Prepare-Demos.ps1` to restore locked packages and compile the stages without invoking a model or deploying Azure resources. Chat and decision calls need separate existing deployment configuration and authorized identities. Stages 03 and 05 include deployment-ready source, not verified deployed resources. No models are downloaded or created.

In the same PowerShell 7 terminal used for the demos, run `& .\demos\Set-DemoEnvironment.ps1` from this bundle root. It asks only for the visible project endpoint and model name, using `gpt-6-luna` when model input is blank; your project must already contain that deployment or a valid override. It does not ask for or access credentials. Demos 01/04 use AzureCliCredential; 02/03 use DefaultAzureCredential. The exact upstream Demo 05 instead uses AzureCliCredential locally and ManagedIdentityCredential when hosted. Values stay in the current process and its future children, not files or User/Machine settings. See the demo README for identity and separate-terminal guidance. Enter real values only locally, never in Copilot.

The main example follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/). Stage 05 preserves its upstream dependency pins, license and notices. Stage 04 links its exact routing logic/contracts, so keep the complete bundle together. Local execution and synthetic tests are not cloud deployment or real inference evidence.

No presentation is included. This source bundle contains no speaker notes, private planning content, credentials, caches, models, audio or build outputs.
