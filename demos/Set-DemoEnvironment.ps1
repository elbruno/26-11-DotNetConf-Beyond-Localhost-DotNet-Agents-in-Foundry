#requires -Version 7.0
<#
Purpose: Prompt for the configuration shared by both Beyond Localhost demos.
Run with & .\demos\Set-DemoEnvironment.ps1 in the PowerShell terminal used for demos.
Expected result: validated process environment values, with no files or sign-in.
Endpoint/model inputs are visible; service-principal inputs are masked.
Process environment values are plaintext, not a credential store.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-MaskedValue([string]$Prompt) {
    $secureValue = Read-Host -Prompt $Prompt -AsSecureString
    $buffer = [IntPtr]::Zero
    try {
        # Preserve embedded control characters so validation can reject them.
        $buffer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($buffer)
    }
    finally {
        if ($buffer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($buffer) }
        $secureValue.Dispose()
    }
}

Write-Host 'Configure both demos in this terminal only. No sign-in or model call is performed.'
Write-Host 'The local console uses AzureCliCredential only; use your separately approved Azure CLI sign-in.'
Write-Host 'The deployable host uses DefaultAzureCredential. Its optional configuration follows:'
Write-Host '1: Keep existing credential variables (also sufficient for the local console)'
Write-Host '2: Supply service-principal variables for the deployable host, not the CLI-only console'
$choice = Read-Host -Prompt 'Choose 1 or 2'
if ($choice -notin @('1', '2')) { throw 'Invalid authentication choice. Enter 1 or 2. Nothing was changed.' }

# Collect and validate everything before changing the process environment.
$pending = @{}
$pending.FOUNDRY_PROJECT_ENDPOINT = Read-Host -Prompt 'Foundry project endpoint'
$pending.AZURE_AI_MODEL_DEPLOYMENT_NAME = Read-Host -Prompt 'Existing model deployment name [gpt-6-luna]'
if ($pending.AZURE_AI_MODEL_DEPLOYMENT_NAME -eq '') {
    $pending.AZURE_AI_MODEL_DEPLOYMENT_NAME = 'gpt-6-luna'
}
$endpoint = $null
if ($pending.FOUNDRY_PROJECT_ENDPOINT -match '[\s\p{Cc}]' -or
    -not [Uri]::TryCreate($pending.FOUNDRY_PROJECT_ENDPOINT, [UriKind]::Absolute, [ref]$endpoint) -or
    $endpoint.Scheme -ne 'https' -or -not $endpoint.Host -or
    $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or
    $endpoint.AbsolutePath -notmatch '^/api/projects/[^/]+/?\z') {
    throw 'Invalid Foundry project endpoint. Use an HTTPS /api/projects/<project> URL without credentials, query or fragment. Nothing was changed.'
}
if ($pending.AZURE_AI_MODEL_DEPLOYMENT_NAME -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z') {
    throw 'Invalid model deployment name. Use 1 to 128 letters, digits, dots, underscores or hyphens, starting with a letter or digit. Nothing was changed.'
}

if ($choice -eq '2') {
    $pending.AZURE_TENANT_ID = Read-MaskedValue 'Service principal tenant ID (hidden)'
    $pending.AZURE_CLIENT_ID = Read-MaskedValue 'Service principal application/client ID (hidden)'
    $pending.AZURE_CLIENT_SECRET = Read-MaskedValue 'Service principal client secret (hidden)'
    foreach ($name in @('AZURE_TENANT_ID', 'AZURE_CLIENT_ID')) {
        $id = [Guid]::Empty
        if (-not [Guid]::TryParse($pending[$name], [ref]$id) -or $id -eq [Guid]::Empty) {
            throw "Invalid $name. Enter a nonempty GUID. Nothing was changed."
        }
        $pending[$name] = $id.ToString()
    }
    if ([string]::IsNullOrWhiteSpace($pending.AZURE_CLIENT_SECRET) -or
        $pending.AZURE_CLIENT_SECRET -match '\p{Cc}' -or $pending.AZURE_CLIENT_SECRET.Length -gt 32766) {
        throw 'Invalid client secret. Enter a nonempty value without control characters, at most 32766 characters. Nothing was changed.'
    }
}

Write-Warning 'For the deployable host, DefaultAzureCredential may prefer environment credentials over CLI sign-in; AZURE_TOKEN_CREDENTIALS may restrict the chain. Other credential variables are preserved, not cleared. The CLI-only console does not use this chain or the supplied service-principal variables.'
if ($choice -eq '1') {
    Write-Host 'Existing credential variables remain unchanged. This choice does not force CLI authentication in the deployable host.'
}
else {
    Write-Host 'The service principal will be available to EnvironmentCredential. Access and credential-chain selection still require separate verification.'
}

$previous = @{}
foreach ($name in $pending.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    foreach ($name in $pending.Keys) {
        [Environment]::SetEnvironmentVariable($name, $pending[$name], 'Process')
    }
}
catch {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
    throw 'Unable to set process configuration. Previous process values were restored.'
}
finally {
    $pending.Clear()
    $previous.Clear()
}

Write-Host 'Configuration set for both demos in this process only. Endpoint/model input was visible; no secret values were printed and no entered values were saved to files.'
Write-Host 'The default model name is a suggestion, not verification that the deployment exists.'
Write-Host 'Child processes inherit these plaintext values. Configure each separate terminal independently.'
Write-Host 'Do not use pwsh -File for this helper: that configures a child process which then exits.'
