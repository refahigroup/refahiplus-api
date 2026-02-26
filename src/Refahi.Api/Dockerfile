FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src


COPY ./Refahi.Backend.slnx ./

#COPY ./nuget.config ./ 2>/dev/null || true
#COPY ./global.json ./ 2>/dev/null || true
#COPY ./Directory.Build.props ./ 2>/dev/null || true
#COPY ./Directory.Build.targets ./ 2>/dev/null || true

COPY ./src/*/*.csproj ./src/
COPY ./src/*/*/*.csproj ./src/
COPY ./src/*/*/*/*.csproj ./src/

RUN dotnet restore ./Refahi.Backend.slnx

COPY . .

RUN dotnet publish ./src/Refahi.Api/Refahi.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Refahi.Api.dll"]