# =============================================================================
# FlexiWork Backend (FW.API) — build context: thư mục FW.Backend
#   docker build -t flexiwork-api ./FW.Backend
# =============================================================================

# ---------- Mô hình nhận diện khuôn mặt (InsightFace buffalo_sc, không lưu trong repo) ----------
FROM alpine:3.20 AS models
ARG BUFFALO_SC_URL=https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_sc.zip
ARG BUFFALO_SC_SHA256=57d31b56b6ffa911c8a73cfc1707c73cab76efe7f13b675a05223bf42de47c72
RUN apk add --no-cache curl unzip     && curl -fsSL -o /tmp/buffalo_sc.zip "$BUFFALO_SC_URL"     && echo "$BUFFALO_SC_SHA256  /tmp/buffalo_sc.zip" | sha256sum -c -     && mkdir -p /models/buffalo_sc     && unzip -j /tmp/buffalo_sc.zip det_500m.onnx w600k_mbf.onnx -d /models/buffalo_sc

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file project trước để tận dụng cache khi restore
COPY FW.Backend.sln ./
COPY FW.Domain/FW.Domain.csproj FW.Domain/
COPY FW.Application/FW.Application.csproj FW.Application/
COPY FW.Infrastructure/FW.Infrastructure.csproj FW.Infrastructure/
COPY FW.API/FW.API.csproj FW.API/
# Chỉ khôi phục / xuất bản cho linux-x64: bỏ thư viện native của Windows, macOS, iOS, Android (ONNX Runtime, SkiaSharp)
RUN dotnet restore FW.API/FW.API.csproj -r linux-x64

COPY . .
RUN dotnet publish FW.API/FW.API.csproj -c Release -r linux-x64 --self-contained false -o /app/publish --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    TZ=Asia/Ho_Chi_Minh
EXPOSE 8080

COPY --from=build /app/publish .
COPY --from=models /models ./models
ENTRYPOINT ["dotnet", "FW.API.dll"]
