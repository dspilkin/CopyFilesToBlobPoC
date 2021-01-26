#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["fnfimageprocessing_AKS_Container_queues/fnfimageprocessing_AKS_Container_queues.csproj", "fnfimageprocessing_AKS_Container_queues/"]
RUN dotnet restore "fnfimageprocessing_AKS_Container_queues/fnfimageprocessing_AKS_Container_queues.csproj"
COPY . .
WORKDIR "/src/fnfimageprocessing_AKS_Container_queues"
RUN dotnet build "fnfimageprocessing_AKS_Container_queues.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "fnfimageprocessing_AKS_Container_queues.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "fnfimageprocessing_AKS_Container_queues.dll"]