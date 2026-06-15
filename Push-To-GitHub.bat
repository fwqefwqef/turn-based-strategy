@echo off
setlocal EnableExtensions

cd /d "%~dp0"

echo.
echo  ========================================
echo   Save project to GitHub
echo  ========================================
echo   Folder: %CD%
echo.

git status -sb
echo.

set "MSG="
set /p MSG="Commit message (Enter to cancel): "

if not defined MSG (
    echo.
    echo Cancelled — no changes were committed.
    goto :done
)

echo.
echo Staging all changes...
git add .
if errorlevel 1 goto :fail

echo Committing...
git commit -m "%MSG%"
if errorlevel 1 (
    echo.
    echo Nothing to commit, or commit failed.
    goto :done
)

echo.
echo Pushing to GitHub...
git push
if errorlevel 1 (
    echo.
    echo Push failed.
    echo If auth failed, open PowerShell and run:
    echo   gh auth setup-git
    goto :done
)

echo.
echo Success — changes are on GitHub.

:done
echo.
pause
exit /b 0

:fail
echo.
echo Git command failed.
pause
exit /b 1
