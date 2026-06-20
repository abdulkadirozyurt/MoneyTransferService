FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["MoneyTransferService.slnx", "./"]

COPY ["src/MoneyTransferService.Core/MoneyTransferService.Core.csproj", "src/MoneyTransferService.Core/"]
COPY ["src/MoneyTransferService.Entities/MoneyTransferService.Entities.csproj", "src/MoneyTransferService.Entities/"]
COPY ["src/MoneyTransferService.DataAccess/MoneyTransferService.DataAccess.csproj", "src/MoneyTransferService.DataAccess/"]
COPY ["src/MoneyTransferService.Business/MoneyTransferService.Business.csproj", "src/MoneyTransferService.Business/"]
COPY ["src/MoneyTransferService.WebAPI/MoneyTransferService.WebAPI.csproj", "src/MoneyTransferService.WebAPI/"]
COPY ["test/MoneyTransferService.Business.Tests/MoneyTransferService.Business.Tests.csproj", "test/MoneyTransferService.Business.Tests/"]

RUN dotnet restore "MoneyTransferService.slnx"

COPY . . 

RUN dotnet publish "src/MoneyTransferService.WebAPI/MoneyTransferService.WebAPI.csproj" \ 
    -c Release \
    -o /app/publish \
    --no-restore

# Stage to build migration bundle
FROM build AS migrator-build
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/MoneyTransferService.DataAccess/MoneyTransferService.DataAccess.csproj \
    --startup-project src/MoneyTransferService.WebAPI/MoneyTransferService.WebAPI.csproj \
    -o /app/efbundle

# Final migrator runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=migrator-build /app/efbundle .
ENTRYPOINT ["./efbundle"]

# Final WebAPI runtime stage (keep it at the end to be the default target)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MoneyTransferService.WebAPI.dll"]
