# Beyond Localhost: .NET Agents in Foundry

Audience-safe source bundle accompanying the session. Follow [the demo README](demos/README.md) for the two-stage console-to-hosted example, prerequisites and commands.

Run `pwsh -File .\Prepare-Demos.ps1` to restore locked packages and compile both programs without invoking a model or deploying Azure resources. Real model calls require your own authorized Foundry project, deployment and identity.

In the same PowerShell 7 terminal used for the demos, run `& .\demos\Set-DemoEnvironment.ps1` from this bundle root. It prompts for masked configuration and offers the existing credential chain or a service principal. Values stay in the current process and its future children, not files or User/Machine settings. See the demo README for credential precedence and separate-terminal guidance. Enter real values only locally, never in Copilot.

The example follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/) and pins the current hosting package to Responses protocol v2.0. Local execution is not cloud deployment.

No presentation is included. This source bundle contains no speaker notes, private planning content, credentials, caches, models, audio or build outputs.
