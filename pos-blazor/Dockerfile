# Runtime image for KosovaPOS.Web (Blazor Server).
# Build the app locally first (`dotnet publish -c Release -o publish`), then this
# image just runs the published output. Mirrors the pos-web "build locally, ship
# output" flow.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Non-root user
RUN adduser -D -u 1001 pos
COPY publish/ ./
RUN mkdir -p /keys && chown -R pos:pos /app /keys
USER pos

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    POS_KEYS_DIR=/keys

EXPOSE 8080
ENTRYPOINT ["dotnet", "KosovaPOS.Web.dll"]
