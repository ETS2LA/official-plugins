@echo off
rem Build ETS2LA
dotnet build ETS2LA

rem Discover all other project files and build them
call .\BuildYourPlugins.bat

rem Finally run ETS2LA
rem The cd below sets the current directory to the ETS2LA build output.
echo Running ETS2LA...
cd /d "%~dp0\ETS2LA\ETS2LA\bin\Debug\net10.0"
.\ETS2LA.exe