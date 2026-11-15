<#
@id        user.dev.lines-of-code
@title     Count lines of code
@menu      Dev
@when      folder, background
@output    grid
@risk      safe
@keywords  loc, lines, code size
@description Counts files and lines per file type in this folder, ignoring node_modules and .git.
#>
Get-ChildItem -LiteralPath {dir} -Recurse -File -Include *.cs,*.js,*.ts,*.py,*.ps1 |
    Where-Object { $_.FullName -notmatch '\\(node_modules|\.git)\\' } |
    Group-Object Extension |
    ForEach-Object { [pscustomobject]@{ Type = $_.Name; Files = $_.Count; Lines = ($_.Group | Get-Content | Measure-Object -Line).Lines } }
