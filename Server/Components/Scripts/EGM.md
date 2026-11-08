# Script Components

## Overview
Blazor UI for executing scripts, managing saved scripts, browsing script trees, and scheduling device or group runs.

## Interface
- RunScript selects scripts and targets and submits execution requests. [RunScript.razor.cs:ExecuteScript]
- SavedScripts creates, edits, and deletes saved scripts. [SavedScripts.razor.cs:OnValidSubmit] [SavedScripts.razor.cs:DeleteSelectedScript]
- ScriptSchedules creates, selects, refreshes, and deletes schedules. [ScriptSchedules.razor.cs:OnValidSubmit] [ScriptSchedules.razor.cs:RefreshSchedules]
- ScriptTreeNode is the tree view model used by these pages. [ScriptTreeNode.cs:ScriptTreeNode]

## Invariants
- Scheduled execution rows are refreshed from the schedule service after mutations. [ScriptSchedules.razor.cs:RefreshSchedules]
- RunScript requires a selected script and target before it submits. [RunScript.razor.cs:ExecuteScript]

## Side Effects
- Execution requests dispatch script work to selected devices or groups. [RunScript.razor.cs:ExecuteScript]
- Save and schedule handlers persist through the data service. [SavedScripts.razor.cs:OnValidSubmit] [ScriptSchedules.razor.cs:OnValidSubmit]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj