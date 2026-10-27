[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
# Restore into a new disposable database. Never replace the working application database.
$database='velqora_restore_'+[Guid]::NewGuid().ToString('N')
if($database -notmatch '^velqora_restore_[a-f0-9]{32}$'){throw 'Invalid rehearsal database name.'}
$archive='/tmp/'+$database+'.dump'
$created=$false
try{
 docker compose exec -T postgres pg_dump -U velqora -d velqora -Fc -f $archive
 if($LASTEXITCODE -ne 0){throw 'Rehearsal backup failed.'}
 docker compose exec -T postgres createdb -U velqora $database
 if($LASTEXITCODE -ne 0){throw 'Could not create disposable restore database.'}
 $created=$true
 docker compose exec -T postgres pg_restore -U velqora -d $database --exit-on-error $archive
 if($LASTEXITCODE -ne 0){throw 'Rehearsal restore failed.'}
 $query='SELECT count(*) FROM information_schema.tables WHERE table_schema=''public'';'
 $tables=$query | docker compose exec -T postgres psql -U velqora -d $database -tA -v ON_ERROR_STOP=1
 if($LASTEXITCODE -ne 0 -or [int]($tables.Trim()) -ne 23){throw 'Restored database table count differs from the expected 22 application tables plus migration history.'}
 $query='SELECT count(*) FROM "AspNetUsers";'
 $users=$query | docker compose exec -T postgres psql -U velqora -d $database -tA -v ON_ERROR_STOP=1
 if($LASTEXITCODE -ne 0 -or [int]($users.Trim()) -lt 3){throw 'Restored initial accounts are missing.'}
 $query='SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId"=''202610260001_Initial'';'
 $migration=$query | docker compose exec -T postgres psql -U velqora -d $database -tA -v ON_ERROR_STOP=1
 if($LASTEXITCODE -ne 0 -or [int]($migration.Trim()) -ne 1){throw 'Restored migration history is missing.'}
 # pg_restore exits on schema/data errors; explicitly check append-only triggers survived.
 $query='SELECT count(*) FROM information_schema.triggers WHERE trigger_schema=''public'';'
 $triggers=$query | docker compose exec -T postgres psql -U velqora -d $database -tA -v ON_ERROR_STOP=1
 if($LASTEXITCODE -ne 0 -or [int]($triggers.Trim()) -lt 2){throw 'Restored append-only protection is missing.'}
 Write-Host 'PASS: custom-format backup restores into a disposable database with schema, accounts, migration history and append-only triggers.'
}finally{
 if($created){docker compose exec -T postgres dropdb -U velqora --force $database | Out-Null}
 docker compose exec -T postgres rm -f $archive | Out-Null
}
