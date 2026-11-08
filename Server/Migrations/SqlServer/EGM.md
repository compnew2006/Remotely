# SQL Server Migrations

## Overview
Entity Framework migration history and model snapshot for SQL Server.

## Interface
- SqlServerDbContextModelSnapshot describes the current SQL Server schema model. [SqlServerDbContextModelSnapshot.cs:BuildModel]
- Migration classes provide ordered Up and Down schema changes, including screenshot, camera, audio, and keyboard capture storage. [20261106154352_Add_Screenshots.cs:Up] [20261106180334_AddCameraClips.cs:Up] [20261106205737_AddAudioClips.cs:Up] [20261106223148_AddKeyboardLogs.cs:Up]

## Invariants
- Provider migrations must remain consistent with the SQL Server context model snapshot. [SqlServerDbContextModelSnapshot.cs:BuildModel]

## Side Effects
- Applying or reverting a migration changes the SQL Server database schema. [20261106154352_Add_Screenshots.cs:Up] [20261106154352_Add_Screenshots.cs:Down]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj