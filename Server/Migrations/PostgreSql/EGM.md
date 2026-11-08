# PostgreSQL Migrations

## Overview
Entity Framework migration history and model snapshot for PostgreSQL.

## Interface
- PostgreSqlDbContextModelSnapshot describes the current PostgreSQL schema model. [PostgreSqlDbContextModelSnapshot.cs:BuildModel]
- Migration classes provide ordered Up and Down schema changes, including screenshot, camera, audio, and keyboard capture storage. [20261106154408_Add_Screenshots.cs:Up] [20261106180349_AddCameraClips.cs:Up] [20261106205753_AddAudioClips.cs:Up] [20261106223204_AddKeyboardLogs.cs:Up]

## Invariants
- Provider migrations must remain consistent with the PostgreSQL context model snapshot. [PostgreSqlDbContextModelSnapshot.cs:BuildModel]

## Side Effects
- Applying or reverting a migration changes the PostgreSQL database schema. [20261106154408_Add_Screenshots.cs:Up] [20261106154408_Add_Screenshots.cs:Down]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj