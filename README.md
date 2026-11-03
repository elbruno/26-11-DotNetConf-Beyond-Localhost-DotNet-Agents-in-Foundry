# Beyond Localhost: .NET Agents in Foundry

Audience-safe source bundle accompanying the session. Follow [the demo README](demos/README.md) for the two-stage console-to-hosted example, prerequisites and commands.

Run `pwsh -File .\Prepare-Demos.ps1` to restore locked packages and compile both programs without invoking a model or deploying Azure resources. Real model calls require your own authorized Foundry project, deployment and identity.

The example follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/) and pins the current hosting package to Responses protocol v2.0. Local execution is not cloud deployment.

No presentation is included. This source bundle contains no speaker notes, private planning content, credentials, caches, models, audio or build outputs.
