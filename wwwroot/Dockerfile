# Giai đoạn 1: Build mã nguồn bằng SDK .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TTLooks_New.csproj", "./"]
RUN dotnet restore "TTLooks_New.csproj"

COPY . .
RUN dotnet publish "TTLooks_New.csproj" -c Release -o /app/publish

# Giai đoạn 2: Khởi chạy ứng dụng
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render tự động cấp cổng qua biến môi trường PORT (mặc định 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TTLooks_New.dll"]