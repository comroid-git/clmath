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
(git add . && git commit -m "SRCINFO")

# build the executable
makepkg -f --noconfirm

# push to aur
git push aur
