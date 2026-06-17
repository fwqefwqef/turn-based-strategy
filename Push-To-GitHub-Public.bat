@echo off
setlocal EnableExtensions

cd /d "%~dp0"

echo.
echo  ========================================
echo   Save project to Public GitHub repo
echo  ========================================
echo   Folder: %CD%
echo.

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
    echo This folder is not a Git repository.
    goto :fail
)

git remote get-url public >nul 2>nul
if errorlevel 1 (
    echo Remote "public" is not configured.
    echo Add it first with:
    echo   git remote add public https://github.com/fwqefwqef/turn-based-strategy-public.git
    goto :fail
)

for /f "delims=" %%i in ('git branch --show-current') do set "BRANCH=%%i"

if not defined BRANCH (
    echo Could not determine the current branch.
    goto :fail
)

echo Current branch: %BRANCH%
echo Public remote : public
echo.

git status -sb
echo.

set "MSG="
set /p MSG="Commit message (Enter to cancel): "

if not defined MSG (
    echo.
    echo Cancelled - no changes were committed.
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
echo Pushing branch %BRANCH% to public...
git push -u public %BRANCH%
if errorlevel 1 (
    echo.
    echo Push failed.
    echo If auth failed, try:
    echo   gh auth setup-git
    goto :done
)

echo.
echo Success - changes are on the public GitHub repo.

:done
echo.
pause
exit /b 0

:fail
echo.
echo Git command failed.
pause
exit /b 1
