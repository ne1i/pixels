FROM oven/bun:latest AS build
WORKDIR /app
ARG VITE_API_URL
ENV VITE_API_URL=${VITE_API_URL}
COPY package.json bun.lock* ./
RUN bun install --frozen-lockfile
COPY . .
RUN bun run build

FROM ghcr.io/static-web-server/static-web-server:latest AS runtime
COPY --from=build /app/build /public

EXPOSE 80
