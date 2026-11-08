# Server Properties

## Overview
Assembly and launch metadata for the Server project.

## Interface
- AssemblyInfo contains assembly-level metadata and test visibility declarations. [AssemblyInfo.cs:InternalsVisibleTo]

## Invariants
- Assembly metadata is compiled into the Server assembly. [AssemblyInfo.cs:InternalsVisibleTo]

## Side Effects
- No runtime behavior is defined in this metadata directory. [AssemblyInfo.cs:InternalsVisibleTo]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj