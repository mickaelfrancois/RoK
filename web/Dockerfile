# syntax=docker/dockerfile:1

# Build stage. The context is the repository root, not web/, because the companion
# references src/Rok.WebApi.Contracts and inherits the root Directory.Build.props.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Directory.Build.props ./
COPY src/Rok.WebApi.Contracts/ src/Rok.WebApi.Contracts/
COPY web/Rok.Companion/ web/Rok.Companion/

RUN dotnet restore web/Rok.Companion/Rok.Companion.csproj

# Publishing trims the assemblies. Anything crossing the wire must be declared in
# RokJsonContext or the page dies at runtime; the project makes IL2026 a build error,
# so a trimming mistake fails here rather than in the browser.
# The wasm-tools workload is optional: without it the publish reuses the prebuilt runtime, which
# works but skips the native relink. Add this line before the publish to shave a megabyte off the
# download, at the cost of a much longer build and a far bigger builder image:
#   RUN dotnet workload install wasm-tools
RUN dotnet publish web/Rok.Companion/Rok.Companion.csproj \
        -c Release \
        --no-restore \
        -o /publish

# Runtime stage: a static web server, plus a reverse proxy towards the machine running Rok.
FROM nginx:1.27-alpine AS runtime

# The official image renders /etc/nginx/templates/*.template with envsubst at startup,
# so ROK_API is a plain environment variable: one image, any host, no rebuild.
COPY web/docker/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /publish/wwwroot /usr/share/nginx/html

# Where Rok listens, as seen FROM THE CONTAINER. host.docker.internal is the Docker host;
# point it at a LAN address instead if Rok runs on another machine. No trailing slash.
# Only ROK_* variables are expanded in the nginx template; nothing else leaks into the config.
ENV NGINX_ENVSUBST_FILTER=^ROK_
ENV ROK_API=http://host.docker.internal:5075

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/index.html || exit 1
