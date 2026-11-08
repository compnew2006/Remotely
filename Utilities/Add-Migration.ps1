[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('SQLite', 'SQLServer', 'PostgreSQL')]
    [string]$Provider,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[A-Za-z0-9_][A-Za-z0-9_ -]*)$')]
    [string]$MigrationName,

    [string]$DotNetPath
)

$ErrorActionPreference = 'Stop'

$providerTargets = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'MigrationProviderTargets.psd1')

$providerTarget = $providerTargets[$Provider]
if (-not $PSCmdlet.ShouldProcess("$Provider migrations", "Add migration '$MigrationName'")) {
    return
}

if (-not (Test-Path "Env:$($providerTarget.ConnectionVariable)")) {
    throw "Set the configured target in environment variable $($providerTarget.ConnectionVariable)."
}

if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $DotNetPath = $dotnetCommand.Source
}
elseif (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) {
    throw "The .NET executable was not found at '$DotNetPath'."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'Server/Server.csproj'

Push-Location (Split-Path $serverProject -Parent)
try {
    $dotnetArguments = @(
        'ef'
        'migrations'
        'add'
        $MigrationName
        '--context'
        $providerTarget.Context
        '--project'
        $serverProject
        '--startup-project'
        $serverProject
    )

    & $DotNetPath @dotnetArguments
    $dotnetExitCode = $LASTEXITCODE
    if ($dotnetExitCode -ne 0) {
        throw "dotnet ef migrations add failed with exit code $dotnetExitCode."
    }
}
finally {
    Pop-Location
}
