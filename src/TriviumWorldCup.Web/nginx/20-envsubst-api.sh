#!/bin/sh
# Inject API_UPSTREAM and the container's actual DNS resolver into the nginx config.
# Using explicit variable list prevents envsubst from clobbering nginx's own $variables
# ($host, $remote_addr, etc.).
#
# NGINX_RESOLVER is read from /etc/resolv.conf at runtime so it works in any environment
# (Docker Compose, ACA staging, ACA production) without hardcoding an IP.
NGINX_RESOLVER=$(awk '/^nameserver/{print $2; exit}' /etc/resolv.conf)
export NGINX_RESOLVER
envsubst '$API_UPSTREAM $NGINX_RESOLVER' \
  < /etc/nginx/conf.d/default.conf.template \
  > /etc/nginx/conf.d/default.conf
