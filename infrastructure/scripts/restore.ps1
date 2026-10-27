[CmdletBinding()]
param([Parameter(Mandatory)][string]$Backup,[Parameter(Mandatory)][switch]$ReplaceDatabase)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$backupPath=(Resolve-Path -LiteralPath $Backup).Path
docker compose stop dashboard api worker
if($LASTEXITCODE -ne 0){throw 'Could not stop application services.'}
docker compose cp $backupPath postgres:/tmp/velqora-restore.dump
if($LASTEXITCODE -ne 0){throw 'Could not copy backup.'}
docker compose exec -T postgres pg_restore -U velqora -d velqora --clean --if-exists --exit-on-error /tmp/velqora-restore.dump
if($LASTEXITCODE -ne 0){throw 'Restore failed. Services remain stopped for inspection.'}
docker compose exec -T postgres rm -f /tmp/velqora-restore.dump
docker compose up -d api worker dashboard
if($LASTEXITCODE -ne 0){throw 'Service restart failed.'}
