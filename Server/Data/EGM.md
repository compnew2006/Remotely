# Server Data Contexts

## Overview
Entity Framework contexts, design-time factories, and the shared Identity and application entity model.

## Interface
- AppDb declares the application DbSets and shared model configuration. [AppDb.cs:AppDb] [AppDb.cs:OnModelCreating]
- SqliteDbContext, SqlServerDbContext, and PostgreSqlDbContext configure provider-specific connections. [SqliteDbContext.cs:SqliteDbContext] [SqlServerDbContext.cs:SqlServerDbContext] [PostgreSqlDbContext.cs:PostgreSqlDbContext]
- IAppDbFactory and AppDbFactory provide a context factory; DesignTimeContexts supplies EF tooling factories. [AppDbFactory.cs:IAppDbFactory] [DesignTimeContexts.cs:SqliteDbContextDesignTime]

## Invariants
- Provider contexts inherit the common AppDb entity model. [SqliteDbContext.cs:SqliteDbContext] [SqlServerDbContext.cs:SqlServerDbContext] [PostgreSqlDbContext.cs:PostgreSqlDbContext]
- Relationships, table mappings, and provider-sensitive serialization are configured in AppDb.OnModelCreating. [AppDb.cs:OnModelCreating]

## Side Effects
- EF context creation connects to the configured provider and can emit database diagnostics. [SqliteDbContext.cs:OnConfiguring] [AppDb.cs:OnConfiguring]
- Factory calls create configured AppDb contexts. [AppDbFactory.cs:GetContext]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj