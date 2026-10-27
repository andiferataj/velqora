$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.local/dotnet'
$env:NUGET_HTTP_CACHE_PATH=Join-Path $projectRoot '.local/nuget-http-cache'
$env:NUGET_SCRATCH=Join-Path $projectRoot '.local/nuget-scratch'
$env:NUGET_PLUGINS_CACHE_PATH=Join-Path $projectRoot '.local/nuget-plugins'
