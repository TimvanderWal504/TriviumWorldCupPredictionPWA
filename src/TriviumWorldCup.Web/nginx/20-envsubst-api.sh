#!/bin/sh
# Substitute ${API_UPSTREAM} in the nginx config at container startup.
# Using explicit variable list ('$API_UPSTREAM') ensures nginx's own $variables
# ($host, $remote_addr, etc.) are NOT touched by envsubst.
envsubst '$API_UPSTREAM' \
  < /etc/nginx/conf.d/default.conf.template \
  > /etc/nginx/conf.d/default.conf
