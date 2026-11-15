<#
@id        user.clean.delete-empty-folders
@title     Delete empty folders
@menu      Clean up
@when      folder, background
@output    toast
@risk      destructive
@keywords  empty, cleanup
@description Deletes every empty folder below this one. Undo does not cover this.
#>
$gone = 0
Get-ChildItem -LiteralPath {dir} -Recurse -Directory |
    Sort-Object { $_.FullName.Length } -Descending |
    Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName; $gone++ }
"Deleted $gone empty folders"
