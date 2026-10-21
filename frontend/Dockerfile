# Repository-root context is required so the root .dockerignore protects local secrets.
FROM node:22-bookworm-slim AS build
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
# Browser requests use the same-origin Nginx /api proxy, never a Docker service hostname.
ENV VITE_API_BASE_URL=""
RUN npm run build

FROM nginxinc/nginx-unprivileged:stable-alpine AS final
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/ /usr/share/nginx/html/
EXPOSE 8080
HEALTHCHECK --interval=20s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
