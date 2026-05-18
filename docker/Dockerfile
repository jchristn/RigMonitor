FROM node:24-alpine AS dashboard-build
WORKDIR /src/dashboard
COPY dashboard/package.json dashboard/package-lock.json ./
RUN npm ci
COPY dashboard/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/RigMonitor.Server/RigMonitor.Server.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=server-build /out/ ./
COPY --from=dashboard-build /src/dashboard/dist ./wwwroot
RUN mkdir -p /app/data/logs
EXPOSE 9990
ENTRYPOINT ["dotnet", "RigMonitor.Server.dll", "--settings", "/app/data/rigmonitor.json"]
