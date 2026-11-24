FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/RAC.Domain/RAC.Domain.csproj src/RAC.Domain/
COPY src/RAC.Application/RAC.Application.csproj src/RAC.Application/
COPY src/RAC.Infrastructure/RAC.Infrastructure.csproj src/RAC.Infrastructure/
COPY src/RAC.Api/RAC.Api.csproj src/RAC.Api/
RUN dotnet restore src/RAC.Api/RAC.Api.csproj
COPY src/RAC.Domain/ src/RAC.Domain/
COPY src/RAC.Application/ src/RAC.Application/
COPY src/RAC.Infrastructure/ src/RAC.Infrastructure/
COPY src/RAC.Api/ src/RAC.Api/
RUN dotnet publish src/RAC.Api/RAC.Api.csproj -c Release -o /app/publish --no-restore
COPY tools/RAC.AdminSetup/ tools/RAC.AdminSetup/
RUN dotnet publish tools/RAC.AdminSetup/RAC.AdminSetup.csproj -c Release -o /app/adminsetup
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/adminsetup /app/adminsetup
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RAC.Api.dll"]
