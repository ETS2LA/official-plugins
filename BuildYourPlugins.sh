#!/bin/bash

# Discover all project files and build them
# (expect for ETS2LA itself)
for d in */; do
    d=${d%/}  # Remove trailing slash
    if [ -f "$d/$d.csproj" ]; then
        if [ "$d" != "ETS2LA" ]; then
            dotnet build "$d/$d.csproj"
            # Then copy Project/bin/Debug/net10.0/Project.dll to ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/Project.dll
            # Note how Linux builds also use .dll files, ETS2LA doesn't use .so files for simplicity.
            rm -f "ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/$d.dll" 2>/dev/null
            echo "Copying $d.dll to ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/"
            cp "$d/bin/Debug/net10.0/$d.dll" "ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/$d.dll"
        fi
    fi
done