FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ./Refahi.Backend.slnx
RUN dotnet publish ./src/Refahi.Api/Refahi.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

#ENV ASPNETCORE_URLS=http://+:5000
#EXPOSE 5000
EXPOSE 8080
COPY --from=build /app/publish .
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN playwright_node=$(find /app/.playwright/node -type f -name node | head -n 1) \
    && "$playwright_node" /app/.playwright/package/cli.js install --with-deps chromium

ENTRYPOINT ["dotnet", "Refahi.Api.dll"]