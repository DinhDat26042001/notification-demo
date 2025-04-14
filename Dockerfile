# Build stage
FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app
COPY --from=build /app/out ./

# Render sẽ cung cấp biến PORT, ta lắng nghe biến này
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "NotificationAPI.dll"]
