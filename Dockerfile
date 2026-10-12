# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY MiniPdf.DocxConverter.sln ./
COPY src/MiniPdf.DocxConverter.Api/MiniPdf.DocxConverter.Api.csproj src/MiniPdf.DocxConverter.Api/
COPY tests/MiniPdf.DocxConverter.Tests/MiniPdf.DocxConverter.Tests.csproj tests/MiniPdf.DocxConverter.Tests/

RUN dotnet restore

COPY src/ src/
COPY tests/ tests/

RUN dotnet test tests/MiniPdf.DocxConverter.Tests/MiniPdf.DocxConverter.Tests.csproj -c Release --no-restore
RUN dotnet publish src/MiniPdf.DocxConverter.Api/MiniPdf.DocxConverter.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install standard font libraries for Linux rendering
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    fonts-dejavu-core \
    fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "MiniPdf.DocxConverter.Api.dll"]
