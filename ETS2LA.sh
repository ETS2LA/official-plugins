#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Build ETS2LA
cd "$SCRIPT_DIR"
dotnet build ETS2LA/ETS2LA.Linux.slnf

# Discover all other project files and build them
./BuildYourPlugins.sh

# Finally run ETS2LA
# The cd below sets the current directory to the ETS2LA build output.
cd "$SCRIPT_DIR/ETS2LA/ETS2LA/bin/Debug/net10.0" || exit 1
./ETS2LA