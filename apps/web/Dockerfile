FROM oven/bun:latest AS build
WORKDIR /app
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
COPY package.json bun.lock* ./
RUN bun install --frozen-lockfile
COPY . .
RUN bun run build

FROM ghcr.io/static-web-server/static-web-server:2-alpine AS runtime
COPY --from=build /app/build /var/public
ENV SERVER_ROOT=/var/public
ENV SERVER_FALLBACK_PAGE=/index.html
EXPOSE 80
