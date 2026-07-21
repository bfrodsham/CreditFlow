FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CreditFlow/CreditFlow.csproj src/CreditFlow/
RUN dotnet restore src/CreditFlow/CreditFlow.csproj

COPY . .
RUN dotnet publish src/CreditFlow/CreditFlow.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render provides PORT at runtime
CMD ["sh","-c","ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet CreditFlow.dll"]
