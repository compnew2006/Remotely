[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
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

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'Server/Server.csproj'

Push-Location (Split-Path $serverProject -Parent)
try {
    $operation = "Apply migration '$MigrationName' to the configured $Provider target"
    if (-not $PSCmdlet.ShouldProcess($providerTarget.ConnectionVariable, $operation)) {
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

    $dotnetArguments = @(
        'ef'
        'database'
        'update'
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
        throw "dotnet ef database update failed with exit code $dotnetExitCode."
    }
}
finally {
    Pop-Location
}
