# Build the Astro site (static) then serve it with nginx.
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
ARG BRAND=shabanejupi
ENV BRAND=$BRAND
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
