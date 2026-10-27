[CmdletBinding()]
param([string]$Destination='.local/backups')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$backupRoot=[IO.Path]::GetFullPath($Destination)
[IO.Directory]::CreateDirectory($backupRoot)|Out-Null
$name='velqora-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.dump'
# Docker writes the binary dump; docker cp avoids PowerShell binary redirection corruption.
docker compose exec -T postgres pg_dump -U velqora -d velqora -Fc -f /tmp/velqora-backup.dump
if($LASTEXITCODE -ne 0){throw 'Database backup failed.'}
docker compose cp postgres:/tmp/velqora-backup.dump (Join-Path $backupRoot $name)
if($LASTEXITCODE -ne 0){throw 'Backup copy failed.'}
docker compose exec -T postgres rm -f /tmp/velqora-backup.dump
Write-Host "Backup created: $(Join-Path $backupRoot $name)"
