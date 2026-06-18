FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["MoneyTransferService.slnx", "./"]

COPY ["src/MoneyTransferService.Core/MoneyTransferService.Core.csproj", "src/MoneyTransferService.Core/"]
COPY ["src/MoneyTransferService.Entities/MoneyTransferService.Entities.csproj", "src/MoneyTransferService.Entities/"]
COPY ["src/MoneyTransferService.DataAccess/MoneyTransferService.DataAccess.csproj", "src/MoneyTransferService.DataAccess/"]
COPY ["src/MoneyTransferService.Business/MoneyTransferService.Business.csproj", "src/MoneyTransferService.Business/"]
COPY ["src/MoneyTransferService.WebAPI/MoneyTransferService.WebAPI.csproj", "src/MoneyTransferService.WebAPI/"]

RUN dotnet restore "MoneyTransferService.slnx"

COPY . . 

RUN dotnet publish "src/MoneyTransferService.WebAPI/MoneyTransferService.WebAPI.csproj" \ 
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MoneyTransferService.WebAPI.dll"]