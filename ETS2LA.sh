#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Build ETS2LA
cd "$SCRIPT_DIR"
dotnet build ETS2LA/ETS2LA.Linux.slnf

# Discover all other project files and build them
for d in */; do
    d=${d%/}  # Remove trailing slash
    if [ -f "$d/$d.csproj" ]; then
        if [ "$d" != "ETS2LA" ]; then
            dotnet build "$d/$d.csproj"
            # Then copy Project/bin/Debug/net10.0/Project.dll to ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/Project.dll
            # Note how Linux builds also use .dll files, ETS2LA doesn't use .so files for simplicity.
            rm -f "ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/$d.dll" 2>/dev/null
            cp "$d/bin/Debug/net10.0/$d.dll" "ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/$d.dll"
        fi
    fi
done

# Finally run ETS2LA
# The cd below sets the current directory to the ETS2LA build output.
cd "$SCRIPT_DIR/ETS2LA/ETS2LA/bin/Debug/net10.0" || exit 1
./ETS2LA