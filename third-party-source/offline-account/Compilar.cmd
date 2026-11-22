@echo off
setlocal
if "%1"=="" (set OUO_TARGET=x64) else (set OUO_TARGET=%1)
if not "%OUO_TARGET%"=="x64" if not "%OUO_TARGET%"=="x86" exit /b 2
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" %OUO_TARGET% >nul
if errorlevel 1 exit /b 1
pushd "%~dp0"
if not exist "build-%OUO_TARGET%" mkdir "build-%OUO_TARGET%"
pushd "build-%OUO_TARGET%"
cl /nologo /O2 /MT /D NDEBUG /D WINVER=0x0601 /D _WIN32_WINNT=0x0601 /D LTC_NO_ASM /D LTC_NO_PROTOTYPES /D _CRT_SECURE_NO_WARNINGS /D _CRT_NONSTDC_NO_WARNINGS /I "..\ntpwedit-0.7\libntpw" /I "..\ntpwedit-0.7\libntpw\libtomcrypt\src\headers" /Fe:OuO.OfflineAccount.exe "..\OuOOfflineAccount.c" "..\ntpwedit-0.7\libntpw\ntpw.c" "..\ntpwedit-0.7\libntpw\libsam.c" "..\ntpwedit-0.7\libntpw\ntreg.c" "..\ntpwedit-0.7\libntpw\log.c" "..\ntpwedit-0.7\libntpw\libtomcrypt\src\ciphers\des.c" "..\ntpwedit-0.7\libntpw\libtomcrypt\src\hashes\md4.c" "..\ntpwedit-0.7\libntpw\libtomcrypt\src\misc\crypt\crypt_argchk.c" /link /SUBSYSTEM:CONSOLE,6.01
set OUO_RESULT=%ERRORLEVEL%
popd
popd
exit /b %OUO_RESULT%
