# deploy/vm (owner, 2026-10-18) -- the API and the migrations bundle, packaged from output built BEFOREHAND by
# deploy/vm/scripts/publish-api.sh. That build needs the private Platform.Infrastructure.* packages, whose feed is reachable
# only from the office network, so it runs on a developer machine; the VM only packages the result. Context: deploy/vm
#   docker build -f deploy/vm/api.Dockerfile --target api      -t purrcare-api deploy/vm
#   docker build -f deploy/vm/api.Dockerfile --target migrator -t purrcare-migrator deploy/vm
# No secret is baked in: every setting arrives as an environment variable at run time (/opt/purrcare/.env).
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app
COPY out/api/ .
# Workstation GC keeps the API's memory small on a 4 GB machine shared with PostgreSQL, ClamAV and the web.
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
USER app
EXPOSE 5000
ENTRYPOINT ["dotnet", "CatEcommerce.Api.dll"]

# Migrations are never applied at startup: the bundle is run on its own, as catecommerce_migrator (deploy/database/roles.sql).
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS migrator
COPY --chmod=755 out/efbundle /efbundle
USER app
ENTRYPOINT ["/efbundle"]
