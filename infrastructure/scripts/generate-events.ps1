[CmdletBinding()]
param([ValidateRange(1,100000)][int]$Count=1000,[string]$Api='http://localhost:8080')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$key=$null
foreach($line in Get-Content -LiteralPath '.env'){if($line -match '^INGESTION_API_KEY=(.+)$'){$key=$Matches[1]}}
if(-not $key){throw 'Generate .env with bootstrap.ps1 first.'}
$accepted=0
for($offset=0;$offset -lt $Count;$offset+=1000){
  $batch=@(1..([Math]::Min(1000,$Count-$offset))|ForEach-Object{
    @{id=[Guid]::NewGuid().ToString();schemaVersion=1;occurredAt=[DateTimeOffset]::UtcNow.ToString('O');source='powershell-generator';
      type='test.telemetry';device='GENERATOR-01';account='lab.generator';ipAddress='192.0.2.90';synthetic=$true;attributes=@{sequence=($offset+$_).ToString()}}
  })
  $result=Invoke-RestMethod "$Api/api/ingest/events" -Method Post -Headers @{'X-Ingestion-Key'=$key} -ContentType application/json -Body (ConvertTo-Json -InputObject $batch -Depth 5)
  $accepted+=$result.accepted
}
Write-Host "Durably accepted $accepted events."
