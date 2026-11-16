<#
@id        user.inspect.changed-recently
@title     Changed in the last N days
@menu      Inspect
@when      folder, background
@output    grid
@risk      safe
@keywords  modified, recent, new files
@description Lists files changed in the last number of days that you type.
#>
Get-ChildItem -LiteralPath {dir} -Recurse -File |
    Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-[int]'{input:Days}') } |
    Sort-Object LastWriteTime -Descending |
    Select-Object FullName, LastWriteTime
