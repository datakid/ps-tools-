<#
@id        user.copy.sha256
@title     SHA-256 of the file
@menu      Copy
@when      file
@output    clipboard
@risk      safe
@multi     single
@keywords  hash, checksum
@description Copies the SHA-256 hash of the selected file.
#>
(Get-FileHash -LiteralPath {path} -Algorithm SHA256).Hash
