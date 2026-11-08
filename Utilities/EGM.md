# Utilities Scripts

## Overview

PowerShell maintenance, database migration, publishing, signing, and command-catalog scripts. The two example reverse-proxy configuration files are reference assets rather than executable entry points.

## Interface

- Add-Migration.ps1 accepts MigrationName and scaffolds migrations for SQLite, SQL Server, and PostgreSQL contexts [Utilities/Add-Migration.ps1:MigrationName].
- Remove-Migration.ps1 removes a migration for each of the three database contexts [Utilities/Remove-Migration.ps1:dotnet ef migrations remove].
- Update-Database.ps1 accepts MigrationName and updates each database context [Utilities/Update-Database.ps1:MigrationName].
- Publish.ps1 accepts OutDir, RID, CertificatePath, CertificatePassword, and CurrentVersion; it also defines Replace-LineInFile and Wait-ForExists [Utilities/Publish.ps1:OutDir/RID/CertificatePath/CertificatePassword/CurrentVersion/Replace-LineInFile/Wait-ForExists].
- Sign-UpdatePackage.ps1 accepts PackagePath, InstallerPath, Version, KeyId, optional KeyPath, and optional OutputDirectory [Utilities/Sign-UpdatePackage.ps1:PackagePath/InstallerPath/Version/KeyId/KeyPath/OutputDirectory].
- Get-CMDCommands.ps1, Get-WindowsCommands.ps1, Get-PSCommands.ps1, and Output-PSCommands.ps1 generate console-command catalogs from built-in text, Windows command documentation, or PowerShell help [Utilities/Get-CMDCommands.ps1:Commands; Utilities/Get-WindowsCommands.ps1:Find-Index; Utilities/Get-PSCommands.ps1:Append-Command; Utilities/Output-PSCommands.ps1:Start-Process].

## Invariants

- Add-Migration.ps1 and Remove-Migration.ps1 locate the sibling Server project and invoke all three named database contexts [Utilities/Add-Migration.ps1:SqliteDbContext/SqlServerDbContext/PostgreSqlDbContext; Utilities/Remove-Migration.ps1:SqliteDbContext/SqlServerDbContext/PostgreSqlDbContext].
- Update-Database.ps1 rewrites Server/appsettings.json to select SQLite, SQL Server, then PostgreSQL, and finally writes SQLite as the last provider value [Utilities/Update-Database.ps1:Replace-LineInFile].
- Publish.ps1 uses the current Git commit timestamp when CurrentVersion is empty and invokes dotnet from PATH [Utilities/Publish.ps1:CurrentVersion/git show/dotnet publish]. For net10.0 projects, AGENTS.md specifies the user-profile .NET 10 SDK because PATH may resolve to SDK 9 [AGENTS.md:Testing instructions].
- Sign-UpdatePackage.ps1 requires a private key supplied by KeyPath or REMOTELY_UPDATE_SIGNING_KEY; its help text says keys must never be committed [Utilities/Sign-UpdatePackage.ps1:KeyPath/REMOTELY_UPDATE_SIGNING_KEY].
- The command-catalog scripts contain machine-specific paths; Get-WindowsCommands.ps1 changes to a hard-coded WindowsServerDocs checkout and Output-PSCommands.ps1 launches hard-coded PowerShell script paths [Utilities/Get-WindowsCommands.ps1:Set-Location; Utilities/Output-PSCommands.ps1:Start-Process].

## Side Effects

- Add-Migration.ps1 scaffolds files for three EF contexts; Remove-Migration.ps1 removes the latest migration for each context [Utilities/Add-Migration.ps1:dotnet ef migrations add; Utilities/Remove-Migration.ps1:dotnet ef migrations remove].
- Update-Database.ps1 edits Server/appsettings.json and applies migrations to configured SQLite, SQL Server, and PostgreSQL databases [Utilities/Update-Database.ps1:Replace-LineInFile/dotnet ef database update].
- Publish.ps1 removes existing publish-folder contents, invokes dotnet publish for agent and desktop targets, archives and moves client packages into Server/wwwroot/Content, and may publish the server to OutDir when both RID and OutDir are supplied [Utilities/Publish.ps1:Remove-Item/dotnet publish/Compress-Archive/Move-Item].
- When a certificate path and password are supplied, Publish.ps1 invokes signtool.exe on the Windows desktop executables [Utilities/Publish.ps1:SignAssemblies/signtool.exe].
- Sign-UpdatePackage.ps1 reads the package, installer, and private key; it writes a manifest JSON file and a base64 signature file to OutputDirectory or the package directory [Utilities/Sign-UpdatePackage.ps1:ReadAllBytes/WriteAllBytes/WriteAllText].
- Get-CMDCommands.ps1 removes and writes the user's Downloads/CMD.txt; Get-WindowsCommands.ps1 also writes that file; Get-PSCommands.ps1 runs Update-Help and writes Downloads/PS.txt; Output-PSCommands.ps1 starts separate powershell.exe and pwsh.exe processes [Utilities/Get-CMDCommands.ps1:Remove-Item/Out-File; Utilities/Get-WindowsCommands.ps1:Out-File; Utilities/Get-PSCommands.ps1:Update-Help/Out-File; Utilities/Output-PSCommands.ps1:Start-Process].

## Verification

There is no Utilities-specific automated test target.

Repository verification command, not run for this documentation task:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
```
