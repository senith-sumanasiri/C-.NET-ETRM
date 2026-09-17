# ---- Stage 1: Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for layer caching
COPY *.csproj ./
RUN dotnet restore

# Copy the rest and publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# ---- Stage 2: Runtime ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# ScottPlot/SkiaSharp needs fontconfig to render text on Linux
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ConsoleApp1.dll"]
