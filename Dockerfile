# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so dependency layers cache independently of source changes.
COPY src/SleepTokenWatcher/SleepTokenWatcher.csproj src/SleepTokenWatcher/
RUN dotnet restore src/SleepTokenWatcher/SleepTokenWatcher.csproj

COPY src/ src/
RUN dotnet publish src/SleepTokenWatcher/SleepTokenWatcher.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

# tzdata backs the Europe/London timestamps shown in emails.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# State lives here; mount a volume so it survives container replacement.
RUN mkdir -p /data && chown app:app /data
VOLUME ["/data"]

USER app
ENV DOTNET_EnableDiagnostics=0

# Considered unhealthy once the heartbeat is older than an hour (four missed 15-minute cycles).
HEALTHCHECK --interval=5m --timeout=10s --start-period=2m --retries=3 \
    CMD test -f /data/heartbeat && test $(( $(date +%s) - $(stat -c %Y /data/heartbeat) )) -lt 3600

ENTRYPOINT ["dotnet", "SleepTokenWatcher.dll"]
