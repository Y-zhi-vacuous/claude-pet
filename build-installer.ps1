# 千千安装包构建脚本
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=== 第1步：发布千千 ===" -ForegroundColor Cyan
dotnet publish "$Root/src/ClaudePet/ClaudePet.csproj" -c Release -o "$Root/publish/ClaudePet" --self-contained false
if ($LASTEXITCODE -ne 0) { throw "千千发布失败" }
Write-Host "千千发布完成" -ForegroundColor Green

Write-Host ""
Write-Host "=== 第2步：构建安装向导 ===" -ForegroundColor Cyan
dotnet publish "$Root/src/Setup/Setup.csproj" -c Release -o "$Root/publish/Setup" --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Setup 构建失败" }
Write-Host "安装向导构建完成" -ForegroundColor Green

Write-Host ""
Write-Host "=== 第3步：复制千千文件到安装向导目录 ===" -ForegroundColor Cyan
Copy-Item -Path "$Root/publish/ClaudePet/*" -Destination "$Root/publish/Setup/" -Recurse -Force
Write-Host "文件复制完成" -ForegroundColor Green

Write-Host ""
Write-Host "=== 完成！ ===" -ForegroundColor Green
Write-Host "安装向导: $Root\publish\Setup\千千安装向导.exe"
Write-Host ""
Write-Host "如需分发：将 publish\Setup\ 整个文件夹打包为 zip 即可"
Write-Host "用户解压后双击 Setup\千千安装向导.exe 即可安装"
