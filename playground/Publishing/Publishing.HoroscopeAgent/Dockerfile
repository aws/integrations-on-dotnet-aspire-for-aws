FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet restore "playground/Publishing/Publishing.HoroscopeAgent/Publishing.HoroscopeAgent.csproj" -a $TARGETARCH
WORKDIR "/src/playground/Publishing/Publishing.HoroscopeAgent"
RUN dotnet build "Publishing.HoroscopeAgent.csproj" -c Release -o /app/build -a $TARGETARCH

FROM build AS publish
ARG TARGETARCH
RUN dotnet publish "Publishing.HoroscopeAgent.csproj" -c Release -o /app/publish -a $TARGETARCH

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Publishing.HoroscopeAgent.dll"]
