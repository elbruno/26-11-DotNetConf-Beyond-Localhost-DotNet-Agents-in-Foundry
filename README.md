# Beyond Localhost: .NET Agents in Foundry

Audience-safe V08 source bundle accompanying the session. Follow [the demo README](demos/README.md) for five stages: simple MAF agent, host-ready/not deployed, hosted agent extended with three read-only function tools, local MAF agent calling Foundry Microsoft-Decision-1, and that decision agent intended for Foundry hosting. No Ollama dependency is used.

The only setup script is `demos\Set-DemoEnvironment.ps1`. It configures existing chat and Decision-1 deployments, not build/export/authentication/deployment. Use standard `dotnet build <project> -c Release` separately when needed. A hosted registration is not proof of successful model access. No models are downloaded or created.

Run `& .\demos\Set-DemoEnvironment.ps1` once in PowerShell 7 from this bundle root. Four visible prompts show existing process/saved values or defaults and print the validated selection. The four non-secret endpoints/deployments are saved in ignored `demos\shared\demo-settings.local.json`; new terminals and demo runs reuse them automatically. Meaningful environment overrides win, blank overrides cannot erase saved settings, and invalid values fail explicitly. No keys, passwords, tokens, credential variables or sign-in are involved. Setup is separate from azd deployment configuration. Demos 01/04 use AzureCliCredential; 02/03 use DefaultAzureCredential. Demo 05 uses AzureCliCredential locally and DefaultAzureCredential when hosted.

For Demo 02, run only `azd ai agent run` from its folder and show the browser Inspector. For Demo 04, run `dotnet run --project .\demos\04-local-decision-agent\LocalDecisionAgent.csproj`; the console displays its question before inference and indented JSON afterward.

Demo 03 adds `GetDotNetFact`, `AddNumbers` and `CountWords` as read-only MAF
tools with OpenTelemetry activities. Its prompts and trace-navigation steps
are in the demo README. A live trace should be checked for the expected tool
spans; a fluent answer alone is not evidence that the tools ran. Demo 02 is
unchanged.

The hosted examples include tool and native-decision instrumentation. A
completed HTTP exchange or local test does not prove successful hosted
inference; verify the final response status, typed result, native request, and
matching trace for each live run. The Demo 05 caller appends the required
Responses API version while preserving the endpoint path and query, keeps
decision JSON on standard output, and writes a validated response ID to
standard error for Trace search. These source-level checks do not establish
cloud authorization or hosted reliability.

The main example follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/). Stage 05 preserves its upstream dependency pins, routing logic, license and notices, with documented configuration/packaging adaptations. Stage 04 links its exact routing logic/contracts, so keep the complete bundle together. Local execution and synthetic tests are not cloud deployment or real inference evidence.

No presentation is included. This source bundle contains no speaker notes, private planning content, credentials, caches, models, audio or build outputs.
