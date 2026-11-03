#requires -Version 7.0
<#
Purpose: Prompt for the configuration shared by both Beyond Localhost demos.
Run with & .\demos\Set-DemoEnvironment.ps1 in the PowerShell terminal used for demos.
Expected result: validated process environment values, with no files or sign-in.
Inputs are masked. Process environment values are plaintext, not a credential store.
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
Write-Host '1: Keep the existing DefaultAzureCredential chain (no credential variables changed)'
Write-Host '2: Supply a service principal through Azure Identity environment variables'
$choice = Read-Host -Prompt 'Choose 1 or 2'
if ($choice -notin @('1', '2')) { throw 'Invalid authentication choice. Enter 1 or 2. Nothing was changed.' }

# Collect and validate everything before changing the process environment.
$pending = @{}
$pending.FOUNDRY_PROJECT_ENDPOINT = Read-MaskedValue 'Foundry project endpoint (hidden)'
$pending.AZURE_AI_MODEL_DEPLOYMENT_NAME = Read-MaskedValue 'Existing model deployment name (hidden)'
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

Write-Warning 'DefaultAzureCredential behavior is unchanged. Existing environment credentials can take precedence over CLI sign-in; credential selectors such as AZURE_TOKEN_CREDENTIALS may restrict the chain. Other credential variables are preserved, not cleared.'
if ($choice -eq '1') {
    Write-Host 'Existing credential variables remain unchanged. This choice does not force CLI authentication.'
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

Write-Host 'Configuration set for both demos in this process only. No entered values were printed or saved.'
Write-Host 'Child processes inherit these plaintext values. Configure each separate terminal independently.'
Write-Host 'Do not use pwsh -File for this helper: that configures a child process which then exits.'
