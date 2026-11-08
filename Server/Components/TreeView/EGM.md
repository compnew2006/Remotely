# Tree View Components

## Overview
Reusable tree and tree-item components for hierarchical selection and expansion.

## Interface
- TreeView and TreeViewItem render the tree structure and individual nodes. [TreeView.razor.cs:TreeView] [TreeViewItem.razor.cs:TreeViewItem]
- TreeItemType identifies supported item presentation types. [TreeItemType.cs:TreeItemType]

## Invariants
- Tree expansion and selection are represented through the component parameters and callbacks. [TreeView.razor.cs:SelectedNode] [TreeViewItem.razor.cs:Expanded]

## Side Effects
- Node interactions invoke the parent selection or expansion callback. [TreeViewItem.razor.cs:OnItemClick]

## Verification
- Run: & "$env:USERPROFILE\.dotnet\dotnet.exe" build Server/Server.csproj