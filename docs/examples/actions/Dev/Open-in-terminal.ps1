<#
@id        user.dev.windows-terminal
@title     Open in Windows Terminal
@menu      Dev
@when      folder, background
@output    none
@risk      safe
@requires  wt
@keywords  terminal, shell
@description Opens a Windows Terminal tab in this folder.
#>
wt -d {dir}
