FROM mcr.microsoft.com/dotnet/aspnet:10.0

# The port has to have a build-time value: an empty EXPOSE is a build error,
# and the runtime value comes from ASPNETCORE_HTTP_PORTS (docker-compose sets
# it to 5000).
ARG ASPNETCORE_HTTP_PORTS=5000
EXPOSE ${ASPNETCORE_HTTP_PORTS}

# curl is used by the HEALTHCHECK below.  The apt lists are removed so the
# image does not carry a package cache that can never be used.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY Server/bin/publish /app

WORKDIR /app

# The SQLite database and the media folders live here.  The directory has to
# exist in the image, otherwise a plain "docker run" (without the volume that
# docker-compose mounts) fails to open the database and the container exits
# before the server starts.
RUN mkdir -p /app/AppData

ENTRYPOINT ["dotnet", "Remotely_Server.dll"]

# The probe is restricted to local callers (LocalOnlyFilter), so it has to run
# from inside the container - the same way the smoke test in CI runs it.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:${ASPNETCORE_HTTP_PORTS}/api/healthcheck || exit 1
