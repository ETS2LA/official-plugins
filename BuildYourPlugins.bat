@echo off

rem Libraries and Plugins are in separate folders, so we need to loop through them both
for %%r in (Libraries Plugins) do (
    for /d %%d in (%%r\*) do (
        if exist "%%d\%%~nd.csproj" (
            dotnet build "%%d\%%~nd.csproj"
            if not exist "ETS2LA\ETS2LA\bin\Debug\net10.0\%%r" mkdir "ETS2LA\ETS2LA\bin\Debug\net10.0\%%r"
            rem They are then copied to ETS2LA/ETS2LA/bin/Debug/net10.0/Libraries/Project.dll 
            rem or ETS2LA/ETS2LA/bin/Debug/net10.0/Plugins/Project.dll depending on the type of project
            del "ETS2LA\ETS2LA\bin\Debug\net10.0\%%r\%%~nd.dll" 2>nul
            copy "%%d\bin\Debug\net10.0\%%~nd.dll" "ETS2LA\ETS2LA\bin\Debug\net10.0\%%r\%%~nd.dll"
            echo Copied %%~nd.dll to ETS2LA\ETS2LA\bin\Debug\net10.0\%%r\%%~nd.dll
        )
    )
)