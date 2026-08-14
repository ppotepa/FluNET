$ErrorActionPreference = 'Stop'

dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build

dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- version
dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- contract
dotnet run --project src/FluNET.Tool/FluNET.Tool.csproj --configuration Release --no-build -- --help
