$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$project = Join-Path $PSScriptRoot 'backend-current\qlthuvien_vip\qlthuvien_vip\qlthuvien_vip\qlthuvien_vip.csproj'
dotnet run --project $project --no-launch-profile --urls http://localhost:5161
