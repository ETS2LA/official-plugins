@echo off
rem Build ETS2LA
dotnet build ETS2LA

rem Discover all other project files and build them
for /d %%d in (*) do (
    if exist "%%d\%%d.csproj" (
        if not "%%d"=="ETS2LA" (
            dotnet build "%%d\%%d.csproj"
            rem Then copy Project/bin/Debug/net10.0/Project.dll to ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/Project.dll
            del "ETS2LA\ETS2LA\bin\Debug\net10.0\Plugins\%%d.dll" 2>nul
            copy "%%d\bin\Debug\net10.0\%%d.dll" "ETS2LA\ETS2LA\bin\Debug\net10.0\Plugins\%%d.dll"
        )
    )
)

rem Finally run ETS2LA
rem The cd below sets the current directory to the ETS2LA build output.
cd /d "%~dp0\ETS2LA\ETS2LA\bin\Debug\net10.0"
.\ETS2LA.exe