<#
@id        user.clean.dated-backup
@title     Dated backup copy
@menu      Clean up
@when      folder
@output    toast
@risk      modifies
@multi     each
@keywords  backup, snapshot, copy
@description Copies the folder next to itself with today's date in the name. The original is not changed.
#>
Copy-Item -LiteralPath {path} -Destination ({path} + ' ' + (Get-Date -Format 'yyyy-MM-dd')) -Recurse
'Backup created'
