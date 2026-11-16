<#
@id        user.copy.git-last-commits
@title     Last 10 commits
@menu      Copy/Git
@when      folder, background
@output    clipboard
@risk      safe
@requires  git
@keywords  log, history, commits
@description Copies a one-line summary of the last 10 commits in this folder.
#>
git -C {dir} log --oneline -10
