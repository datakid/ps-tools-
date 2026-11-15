<#
@id        user.inspect.big-files
@title     Files bigger than...
@menu      Inspect
@when      folder, background
@output    grid
@risk      safe
@keywords  large, size, space
@description Lists the files in this folder tree that are bigger than the size you type, in megabytes.
#>
Get-ChildItem -LiteralPath {dir} -Recurse -File |
    Where-Object { $_.Length -gt ([int]'{input:Minimum size in MB}' * 1MB) } |
    Sort-Object Length -Descending |
    Select-Object FullName, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
