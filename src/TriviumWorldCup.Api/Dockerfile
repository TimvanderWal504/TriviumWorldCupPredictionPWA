# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (layer-cached)
COPY TriviumWorldCup.Api.csproj ./
RUN dotnet restore TriviumWorldCup.Api.csproj

# Copy remaining source and publish
COPY . ./
RUN dotnet publish TriviumWorldCup.Api.csproj -c Release -o /app/publish --no-restore

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system twc && adduser --system --ingroup twc twc
USER twc

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TriviumWorldCup.Api.dll"]
