# Beyond Localhost: .NET Agents in Foundry

Audience-safe source bundle accompanying the session. Follow [the demo README](demos/README.md) for the two-stage console-to-hosted example, prerequisites and commands.

Run `pwsh -File .\Prepare-Demos.ps1` to restore locked packages and compile both programs without invoking a model or deploying Azure resources. Real model calls require your own authorized Foundry project, deployment and identity.

In the same PowerShell 7 terminal used for the demos, run `& .\demos\Set-DemoEnvironment.ps1` from this bundle root. It asks only for the visible project endpoint and model name, using `gpt-6-luna` when model input is blank; your project must already contain that deployment or a valid override. It does not ask for or access credentials. The local-only console uses AzureCliCredential; the deployable host uses DefaultAzureCredential. Values stay in the current process and its future children, not files or User/Machine settings. See the demo README for identity and separate-terminal guidance. Enter real values only locally, never in Copilot.

The example follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/) and pins the current hosting package to Responses protocol v2.0. Local execution is not cloud deployment.

No presentation is included. This source bundle contains no speaker notes, private planning content, credentials, caches, models, audio or build outputs.
