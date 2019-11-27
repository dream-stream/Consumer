FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-alpine AS base
WORKDIR /app
EXPOSE 80
RUN apk update && apk add --no-cache gcompat

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-alpine AS build
WORKDIR /src
COPY ["Consumer/Consumer.csproj", "Consumer/"]
RUN dotnet restore "Consumer/Consumer.csproj"
COPY . .
WORKDIR "/src/Consumer"
RUN dotnet build "Consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Consumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY libgrpc_csharp_ext.x86.so libgrpc_csharp_ext.x86.so
ENTRYPOINT ["dotnet", "Consumer.dll"]