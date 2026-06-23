@echo off
setlocal

cd /d "%~dp0"

echo.
echo Turn Based Strategy - Push To GitHub
echo.

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
    echo This folder is not a git repository.
    pause
    exit /b 1
)

for /f "delims=" %%i in ('git branch --show-current') do set "CURRENT_BRANCH=%%i"
if not defined CURRENT_BRANCH set "CURRENT_BRANCH=master"

echo Current branch: %CURRENT_BRANCH%
echo Remote:
git remote -v
echo.
echo Status:
git status --short
echo.

set /p COMMIT_MESSAGE=Commit message (leave blank to skip commit and only push): 

if not "%COMMIT_MESSAGE%"=="" (
    git add -A
    if errorlevel 1 (
        echo git add failed.
        pause
        exit /b 1
    )

    git commit -m "%COMMIT_MESSAGE%"
    if errorlevel 1 (
        echo.
        echo Commit failed or there was nothing to commit.
        echo Push was not attempted.
        pause
        exit /b 1
    )
)

echo.
echo Pushing origin %CURRENT_BRANCH%...
git push -u origin %CURRENT_BRANCH%
if errorlevel 1 (
    echo.
    echo Push failed.
    pause
    exit /b 1
)

echo.
echo Push complete.
pause
exit /b 0
