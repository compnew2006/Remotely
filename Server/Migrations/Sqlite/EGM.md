# SQLite Migrations

## Overview
Entity Framework migration history and model snapshot for SQLite.

## Interface
- SqliteDbContextModelSnapshot describes the current SQLite schema model. [SqliteDbContextModelSnapshot.cs:BuildModel]
- Migration classes provide ordered Up and Down schema changes, including screenshot, camera, audio, and keyboard capture storage. [20261106154256_Add_Screenshots.cs:Up] [20261106180313_AddCameraClips.cs:Up] [20261106205713_AddAudioClips.cs:Up] [20261106223133_AddKeyboardLogs.cs:Up]

## Invariants
- Provider migrations must remain consistent with the SQLite context model snapshot. [SqliteDbContextModelSnapshot.cs:BuildModel]

## Side Effects
- Applying or reverting a migration changes the SQLite database schema. [20261106154256_Add_Screenshots.cs:Up] [20261106154256_Add_Screenshots.cs:Down]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj