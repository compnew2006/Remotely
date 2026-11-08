# Agent Extensions

## Overview

Small adapters used by the agent when it translates platform library values into shared DTOs.

## Interface

- PowerShell completion conversion is exposed by PowerShellCompletionExtensions.ToPwshCompletion [Agent/Extensions/PowerShellCompletionExtensions.cs:PowerShellCompletionExtensions.ToPwshCompletion].

## Invariants

- The adapter copies the current match and replacement positions and maps each completion type through EnumMapper.ToEnum [Agent/Extensions/PowerShellCompletionExtensions.cs:PowerShellCompletionExtensions.ToPwshCompletion].

## Side Effects

- The conversion only creates and returns a DTO; it performs no I/O [Agent/Extensions/PowerShellCompletionExtensions.cs:PowerShellCompletionExtensions.ToPwshCompletion].

## Verification

- Run from the repository root: & "$env:USERPROFILE\.dotnet\dotnet.exe" test Remotely.sln
