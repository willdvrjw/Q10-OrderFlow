# Etapa de construcción: React + TypeScript + Vite.
FROM node:24-alpine AS build
WORKDIR /website
COPY web/package.json ./
# El lockfile se generará y versionará tras la primera restauración comprobada.
RUN npm install --no-audit --no-fund
COPY web/ ./
RUN npm run build

# Etapa de ejecución: Nginx sirve el frontend y actúa como proxy.
FROM nginx:1.29-alpine AS runtime
COPY infra/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /website/dist/ /usr/share/nginx/html/
EXPOSE 80
