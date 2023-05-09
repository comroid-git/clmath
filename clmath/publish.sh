#!/usr/bin/bash

set -e # exit on error

./clean.sh

echo "ensure remote exists"
if [ -z "$(git remote | grep aur)" ]; then
  git init || true
  git remote add aur ssh://aur@aur.archlinux.org/clmath-git.git
fi

echo "run tests first"
dotnet test -c Test

echo "update SRCINFO"
makepkg --printsrcinfo > .SRCINFO
(git add . && git commit -m "SRCINFO") || true

echo "build the executable"
makepkg -f --noconfirm

echo "show the current state"
pwd
tree

echo "push to aur"
git push --set-upstream aur master
