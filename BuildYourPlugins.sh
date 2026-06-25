#!/bin/bash

# Libraries and Plugins are in separate folders, so we need to loop through them both
for root in Libraries Plugins; do
  for d in "$root"/*; do
    [ -d "$d" ] || continue
    name="$(basename "$d")"
    if [ -f "$d/$name.csproj" ]; then
      dotnet build "$d/$name.csproj"
      # They are then copied to ETS2LA/ETS2LA/bin/Debug/net10.0/Libraries/Project.dll 
      # or ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/Project.dll depending on the type of project
      out_dir="ETS2LA/ETS2LA/bin/Debug/net10.0/$root"
      mkdir -p "$out_dir"
      rm -f "$out_dir/$name.dll" 2>/dev/null
      cp "$d/bin/Debug/net10.0/$name.dll" "$out_dir/$name.dll"
    fi
  done
done