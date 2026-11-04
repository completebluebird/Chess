#!/usr/bin/env bash
# Installs the .NET SDK that tools/CoreTests needs, for environments without Unity (e.g. cloud).
set -euo pipefail

if command -v dotnet >/dev/null && dotnet --list-sdks | grep -q '^10\.'; then
  echo ".NET 10 SDK already installed."
  exit 0
fi

curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
echo "Installed. Add to PATH: export PATH=\"\$HOME/.dotnet:\$PATH\""
