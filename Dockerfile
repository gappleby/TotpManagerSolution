# ── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer — only invalidated when project files change
COPY TotpManagerSolution.slnx .
COPY TotpManager.Core/TotpManager.Core.csproj TotpManager.Core/
COPY TotpManager.Web/TotpManager.Web.csproj   TotpManager.Web/
RUN dotnet restore TotpManager.Web/TotpManager.Web.csproj

# Copy source and publish
COPY TotpManager.Core/ TotpManager.Core/
COPY TotpManager.Web/  TotpManager.Web/
RUN dotnet publish TotpManager.Web/TotpManager.Web.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TotpManager.Web.dll"]
