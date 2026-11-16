<#
@id        user.copy.git-branch
@title     Git branch name
@menu      Copy/Git
@when      folder, background
@output    clipboard
@risk      safe
@requires  git
@keywords  branch, git
@description Copies the current git branch of this folder.
#>
git -C {dir} rev-parse --abbrev-ref HEAD
