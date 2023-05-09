#!/usr/bin/bash

set -e # exit on error

./clean.sh

# ensure remote exists
if [ -z "$(git remote | grep aur)" ]; then
  git init || true
  git remote add aur ssh://aur@aur.archlinux.org/clmath-git.git
fi

# run tests first
dotnet test -c Test

# update SRCINFO
makepkg --printsrcinfo > .SRCINFO
(git add .SRCINFO && git commit -m "SRCINFO") || (echo "Failed to commit .SRCINFO" >&2 | exit)

# build the executable
makepkg -f --noconfirm

# push to aur
git push aur
