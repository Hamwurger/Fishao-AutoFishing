@echo off
rem Builds ..\FishaoBot.exe - one file, the pictures and the icon are built into it.
rem Uses the C# compiler that ships with Windows (.NET Framework 4), nothing to install.
rem   Bot\         the bot itself (capture, input, minigame, dialogs, statistics)
rem   UI\          the window  (UI\Controls = custom drawn controls, UI\Pages = tabs)
rem   Resources\   pictures searched for in the game + the icon
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set RES=/resource:Resources\close_x.png,close_x.png /resource:Resources\close_x2.png,close_x2.png /resource:Resources\energy_bolt.png,energy_bolt.png /resource:Resources\levelup_star.png,levelup_star.png /resource:Resources\splash.png,splash.png /resource:Resources\bait_empty.png,bait_empty.png
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /platform:anycpu /win32icon:Resources\FishaoBot.ico /out:..\FishaoBot.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll /r:System.Web.Extensions.dll %RES% /recurse:*.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo Built ..\FishaoBot.exe
