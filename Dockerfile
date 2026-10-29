# Giai đoạn 1: Build mã nguồn bằng SDK .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy file cấu hình dự án và restore
COPY ["TTLooks_New.csproj", "./"]
RUN dotnet restore "TTLooks_New.csproj"

# Copy toàn bộ code và build bản Release
COPY . .
RUN dotnet publish "TTLooks_New.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Giai đoạn 2: Runtime chạy web
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Cấu hình cổng cho Render
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TTLooks_New.dll"]