FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props GymRatService.sln ./
COPY src/GymRatService.Common/GymRatService.Common.csproj src/GymRatService.Common/
COPY src/GymRatService.Repository/GymRatService.Repository.csproj src/GymRatService.Repository/
COPY src/GymRatService.Service/GymRatService.Service.csproj src/GymRatService.Service/
COPY src/GymRatService.API/GymRatService.API.csproj src/GymRatService.API/
RUN dotnet restore src/GymRatService.API/GymRatService.API.csproj

COPY src/ src/
RUN dotnet publish src/GymRatService.API/GymRatService.API.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GymRatService.API.dll"]
