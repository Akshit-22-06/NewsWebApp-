# ==========================================================
# Multi-stage Dockerfile for NewsWebApp (.NET 10.0)
# Production-ready, containerized deployment
# ==========================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["NewsWebApp.csproj", "./"]
RUN dotnet restore "NewsWebApp.csproj"

# Copy full application code and build
COPY . .
RUN dotnet build "NewsWebApp.csproj" -c Release -o /app/build

# Publish optimized release binaries
FROM build AS publish
RUN dotnet publish "NewsWebApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Expose HTTP ports
EXPOSE 8080
EXPOSE 80

# Environment configurations
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DatabaseProvider=Sqlite
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/newswebapp.db"

# Create persistent data directory for SQLite database
RUN mkdir -p /app/data && chmod 777 /app/data

# Copy published output from build stage
COPY --from=publish /app/publish .

# Expose persistent data volume
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "NewsWebApp.dll"]
