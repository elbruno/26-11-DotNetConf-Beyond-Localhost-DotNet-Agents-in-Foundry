# Beyond Localhost: .NET Agents in Foundry

Audience-safe source bundle accompanying the session. Follow [the demo README](demos/README.md) for five stages: simple MAF agent, host-ready/not deployed, same agent intended for Foundry deployment, local MAF agent calling Foundry Microsoft-Decision-1, and that decision agent intended for Foundry hosting. No Ollama dependency is used.

The only setup script is `demos\Set-DemoEnvironment.ps1`. It configures existing chat and Decision-1 deployments, not build/export/authentication/deployment. Use standard `dotnet build <project> -c Release` separately when needed. A hosted registration is not proof of successful model access. No models are downloaded or created.

Run `& .\demos\Set-DemoEnvironment.ps1` once in PowerShell 7 from this bundle root. Four visible prompts show existing process/saved values or defaults and print the validated selection. The four non-secret endpoints/deployments are saved in ignored `demos\shared\demo-settings.local.json`; new terminals and demo runs reuse them automatically. Meaningful environment overrides win, blank overrides cannot erase saved settings, and invalid values fail explicitly. No keys, passwords, tokens, credential variables or sign-in are involved. Setup is separate from azd deployment configuration. Demos 01/04 use AzureCliCredential; 02/03 use DefaultAzureCredential. Demo 05 retains AzureCliCredential locally and agent-instance ManagedIdentityCredential when hosted.

For Demo 02, run only `azd ai agent run` from its folder and show the browser Inspector. For Demo 04, run `dotnet run --project .\demos\04-local-decision-agent\LocalDecisionAgent.csproj`; the console displays its question before inference and indented JSON afterward.

The main example follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/). Stage 05 preserves its upstream dependency pins, routing logic, license and notices, with documented configuration/packaging adaptations. Stage 04 links its exact routing logic/contracts, so keep the complete bundle together. Local execution and synthetic tests are not cloud deployment or real inference evidence.

No presentation is included. This source bundle contains no speaker notes, private planning content, credentials, caches, models, audio or build outputs.
