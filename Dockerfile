# -----------------------------------------------------------------------------
# Stage 1: Build & Publish
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Leverage Docker cache layer for NuGet dependencies
COPY ["src/LoanDocAI.Api/LoanDocAI.Api.csproj", "src/LoanDocAI.Api/"]
RUN dotnet restore "src/LoanDocAI.Api/LoanDocAI.Api.csproj"

# Copy source code and publish in Release mode
COPY ["src/LoanDocAI.Api/", "src/LoanDocAI.Api/"]
WORKDIR "/src/src/LoanDocAI.Api"
RUN dotnet publish "LoanDocAI.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 2: Hardened Runtime Container
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Enterprise Security: Run as unprivileged non-root user (app UID/GID: 1654)
USER app

# Copy published binaries & static dashboard assets
COPY --from=build --chown=app:app /app/publish .

# Copy institutional credit underwriting policy rules
COPY --chown=app:app data/policies/ ./data/policies/

# Runtime networking & diagnostics configuration
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Native container liveness healthcheck
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "LoanDocAI.Api.dll"]
