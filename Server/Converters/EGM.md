# Database Converters

## Overview
Entity Framework value converters for database-specific date and time representations.

## Interface
- PostgresDateTimeOffsetConverter converts DateTimeOffset values using UTC and local-time expressions. [PostgresDateTimeOffsetConverter.cs:PostgresDateTimeOffsetConverter] [PostgresDateTimeOffsetConverter.cs:ToUtc]

## Invariants
- PostgreSQL values are normalized through the converter expressions when writing and reading. [PostgresDateTimeOffsetConverter.cs:ToUtc] [PostgresDateTimeOffsetConverter.cs:ToLocalTime]

## Side Effects
- The converter changes the database representation of DateTimeOffset properties during EF materialization and persistence. [PostgresDateTimeOffsetConverter.cs:PostgresDateTimeOffsetConverter]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj