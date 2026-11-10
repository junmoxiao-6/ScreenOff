@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [!] 未找到 dotnet，请先安装 .NET SDK 9 或更高版本：
    echo     https://dotnet.microsoft.com/download
    exit /b 1
)

echo [1/2] 编译并打包（自包含单文件）...
dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o dist
if errorlevel 1 (
    echo [!] 编译失败
    exit /b 1
)

echo.
echo [2/2] 完成。输出文件：
echo     %~dp0dist\ScreenOff.exe
echo.
echo 该文件自带 .NET 运行时，可直接复制到任意 64 位 Windows 10/11 上运行。
