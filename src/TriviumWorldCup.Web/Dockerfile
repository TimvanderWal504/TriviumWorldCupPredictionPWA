# ── Build stage ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files and install dependencies (layer-cached)
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and build
COPY . ./
RUN npm run build

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM nginx:1.27-alpine AS runtime

# Remove default nginx static content
RUN rm -rf /usr/share/nginx/html/*

# Copy built React app
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx site config
COPY nginx/default.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
