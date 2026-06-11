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

# Copy nginx config as a template — API_UPSTREAM is substituted at startup by the entrypoint script.
# The template is placed in conf.d/ (not templates/) to avoid nginx's default envsubst which
# would also clobber nginx's own $variables ($host, $remote_addr, etc.).
COPY nginx/default.conf.azure /etc/nginx/conf.d/default.conf.template

# Entrypoint script: runs envsubst '$API_UPSTREAM' before nginx starts.
# nginx Docker image executes all scripts in /docker-entrypoint.d/ on startup.
COPY nginx/20-envsubst-api.sh /docker-entrypoint.d/20-envsubst-api.sh
RUN chmod +x /docker-entrypoint.d/20-envsubst-api.sh

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
