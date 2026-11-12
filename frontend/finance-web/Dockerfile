# syntax=docker/dockerfile:1

FROM node:24-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS web
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /src/dist/finance-web/browser /usr/share/nginx/html
# API_UPSTREAM (e.g. api:8080) is substituted into the config at startup.
ENV API_UPSTREAM=api:8080
EXPOSE 80
