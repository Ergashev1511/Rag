# Build context: repozitoriy ildizi (docker compose build api yoki docker build -f src/Rag.Api/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Rag.sln ./
COPY src/Rag.Domain/Rag.Domain.csproj src/Rag.Domain/
COPY src/Rag.Application/Rag.Application.csproj src/Rag.Application/
COPY src/Rag.Infrastructure/Rag.Infrastructure.csproj src/Rag.Infrastructure/
COPY src/Rag.Api/Rag.Api.csproj src/Rag.Api/
RUN dotnet restore src/Rag.Api/Rag.Api.csproj

COPY src/ src/
RUN dotnet publish src/Rag.Api/Rag.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Rag.Api.dll"]
