#Build Stage
FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS Build
WORKDIR /source
COPY . .
RUN dotnet restore "./lyrebird-task.csproj" --disable-parallel
RUN dotnet publish "./lyrebird-task.csproj" -c release -o /app --no-restore

# Serve Stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal
WORKDIR /app
COPY --from=build /app ./

EXPOSE 5000

ENTRYPOINT ["dotnet", "lyrebird-task.csproj.dll"]
