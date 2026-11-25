# NillyFont Pro — Clean Uninstaller (Zero Admin · HKCU only)
param([switch]$Silent)
$ErrorActionPreference = 'SilentlyContinue'
Get-Process NillyFontPro -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (Test-Path 'HKCU:\Software\Classes\Applications\NillyFontPro.exe') { Remove-Item 'HKCU:\Software\Classes\Applications\NillyFontPro.exe' -Recurse -Force }
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'NillyFont Pro.lnk') -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Nilly graphics\NillyFont Pro.lnk') -Force
if (-not $Silent) { Write-Host '  Το NillyFont Pro απεγκαταστάθηκε καθαρά.' -ForegroundColor Green }
