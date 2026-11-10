# =============================================================================
# FlexiWork Backend (FW.API) — build context: thư mục FW.Backend
#   docker build -t flexiwork-api ./FW.Backend
# =============================================================================

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file project trước để tận dụng cache khi restore
COPY global.json FW.Backend.sln ./
COPY FW.Domain/FW.Domain.csproj FW.Domain/
COPY FW.Application/FW.Application.csproj FW.Application/
COPY FW.Infrastructure/FW.Infrastructure.csproj FW.Infrastructure/
COPY FW.API/FW.API.csproj FW.API/
RUN dotnet restore FW.API/FW.API.csproj

COPY . .
RUN dotnet publish FW.API/FW.API.csproj -c Release -o /app/publish --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Ho_Chi_Minh
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FW.API.dll"]
