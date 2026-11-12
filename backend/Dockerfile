# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached between code changes.
COPY FinanceManager.Api/FinanceManager.Api.csproj FinanceManager.Api/
COPY FinanceManager.Application/FinanceManager.Application.csproj FinanceManager.Application/
COPY FinanceManager.Domain/FinanceManager.Domain.csproj FinanceManager.Domain/
COPY FinanceManager.Infrastructure/FinanceManager.Infrastructure.csproj FinanceManager.Infrastructure/
RUN dotnet restore FinanceManager.Api/FinanceManager.Api.csproj

COPY . .
RUN dotnet publish FinanceManager.Api/FinanceManager.Api.csproj \
    --configuration Release --no-restore --output /app/publish

# Self-contained migration runner: applies pending EF migrations, then exits.
FROM build AS migrations-build
RUN dotnet tool install --global dotnet-ef --version "10.*"
ENV PATH="$PATH:/root/.dotnet/tools"
# Match the image architecture (amd64 -> x64, arm64 -> arm64).
ARG TARGETARCH
RUN RID="linux-$([ "$TARGETARCH" = "amd64" ] && echo x64 || echo "$TARGETARCH")" && \
    dotnet ef migrations bundle \
    --project FinanceManager.Infrastructure \
    --startup-project FinanceManager.Api \
    --configuration Release \
    --self-contained --target-runtime "$RID" \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=migrations-build /app/efbundle .
USER $APP_UID
# The connection string comes from ConnectionStrings__DefaultConnection.
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"$ConnectionStrings__DefaultConnection\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FinanceManager.Api.dll"]
