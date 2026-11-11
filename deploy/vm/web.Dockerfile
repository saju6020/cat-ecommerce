# deploy/vm (owner, 2026-10-18) -- the website, built from the repository root:
#   docker build -f deploy/vm/web.Dockerfile --build-arg NEXT_PUBLIC_API_BASE_URL=https://<host> -t purrcare-web .
# NEXT_PUBLIC_* values are fixed into the bundle at build time, so the site address is a build argument.
FROM node:22-bookworm-slim AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
ARG NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL} \
    NEXT_PUBLIC_USE_MOCKS=false \
    NEXT_TELEMETRY_DISABLED=1
RUN test -n "$NEXT_PUBLIC_API_BASE_URL" && npm run build

FROM node:22-bookworm-slim AS web
WORKDIR /app
ENV NODE_ENV=production \
    PORT=3000 \
    HOSTNAME=0.0.0.0 \
    NEXT_TELEMETRY_DISABLED=1
COPY --from=build --chown=node:node /web/.next/standalone ./
COPY --from=build --chown=node:node /web/.next/static ./.next/static
COPY --from=build --chown=node:node /web/public ./public
USER node
EXPOSE 3000
CMD ["node", "server.js"]
