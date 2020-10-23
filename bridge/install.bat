﻿@echo off

set PROJECT_NAME=TON CLIENT DOTNET BRIDGE
set ROOT_DIR=%cd%
set INSTALL_DIR=%ROOT_DIR%\..

echo Building %PROJECT_NAME%.
if not exist .\cmake-build-release mkdir .\cmake-build-release
cd .\cmake-build-release

cmake .. -G "NMake Makefiles" -DCMAKE_INSTALL_PREFIX="%INSTALL_DIR%" -DCMAKE_BUILD_TYPE=Release
if %errorlevel% neq 0 exit /b %errorlevel%

nmake
if %errorlevel% neq 0 exit /b %errorlevel%

echo Running tests.
ctest --output-on-failure --timeout 100
if %errorlevel% neq 0 exit /b %errorlevel%

echo Installing %PROJECT_NAME% into %INSTALL_DIR%.
nmake install
if %errorlevel% neq 0 exit /b %errorlevel%

cd "%ROOT_DIR%"

echo %PROJECT_NAME% is successfully installed into %INSTALL_DIR%.
echo %PROJECT_NAME% build finished.
