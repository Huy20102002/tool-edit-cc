@echo off
chcp 65001 > nul
echo =====================================================================
echo          AUTO VIDEO EDITOR - BUILD & PACKAGE INSTALLER (.EXE)
echo =====================================================================
echo.

echo [1/3] Đang biên dịch dự án Standalone (Không cần cài đặt .NET trên máy khác)...
dotnet publish src/AutoVideoEditor.App/AutoVideoEditor.App.csproj -c Release -r win-x64 --self-contained true -o bin/publish/AutoVideoEditor_Standalone
if %ERRORLEVEL% NEQ 0 (
    echo [LỖI] Biên dịch .NET thất bại.
    pause
    exit /b %ERRORLEVEL%
)

if exist "C:\ffmpeg\bin\ffmpeg.exe" (
    if not exist "bin\publish\AutoVideoEditor_Standalone\ffmpeg" mkdir "bin\publish\AutoVideoEditor_Standalone\ffmpeg"
    copy /y "C:\ffmpeg\bin\ffmpeg.exe" "bin\publish\AutoVideoEditor_Standalone\ffmpeg\ffmpeg.exe" >nul
    copy /y "C:\ffmpeg\bin\ffprobe.exe" "bin\publish\AutoVideoEditor_Standalone\ffmpeg\ffprobe.exe" >nul
)

echo.
echo [2/3] Đang tìm kiếm trình biên dịch Inno Setup (ISCC.exe)...
set ISCC_PATH=""
if exist "C:\Users\%USERNAME%\AppData\Local\Programs\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH="C:\Users\%USERNAME%\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH="C:\Program Files\Inno Setup 6\ISCC.exe"
) else (
    where ISCC.exe >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        set ISCC_PATH=ISCC.exe
    )
)

if %ISCC_PATH%=="" (
    echo [CẢNH BÁO] Không tìm thấy Inno Setup Compiler. Đang tự động cài đặt qua winget...
    winget install --id JRSoftware.InnoSetup -e --silent --accept-source-agreements --accept-package-agreements
    if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set ISCC_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if exist "C:\Users\%USERNAME%\AppData\Local\Programs\Inno Setup 6\ISCC.exe" set ISCC_PATH="C:\Users\%USERNAME%\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
)

echo.
echo [3/3] Đang đóng gói bộ cài đặt Installer qua Inno Setup...
%ISCC_PATH% "installer\setup.iss"
if %ERRORLEVEL% NEQ 0 (
    echo [LỖI] Đóng gói Inno Setup thất bại.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo =====================================================================
echo [THÀNH CÔNG] Đã tạo file cài đặt hoàn tất:
echo 👉 bin\dist\AutoVideoEditor_Setup_v1.0.0.exe
echo =====================================================================
echo.
pause
