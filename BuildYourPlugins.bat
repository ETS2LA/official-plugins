@echo off

rem Discover all projects and build them, then copy the output dlls to the ETS2LA plugins folder.
for /d %%d in (*) do (
    if exist "%%d\%%d.csproj" (
        if not "%%d"=="ETS2LA" (
            dotnet build "%%d\%%d.csproj"
            rem Then copy Project/bin/Debug/net10.0/Project.dll to ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/Project.dll
            del "ETS2LA\ETS2LA\bin\Debug\net10.0\Plugins\%%d.dll" 2>nul
            echo Copying "%%d\bin\Debug\net10.0\%%d.dll" to "ETS2LA\ETS2LA\bin\Debug\net10.0\Plugins\%%d.dll"
            copy "%%d\bin\Debug\net10.0\%%d.dll" "ETS2LA\ETS2LA\bin\Debug\net10.0\Plugins\%%d.dll"
        )
    )
)