#!/bin/sh
set -eu

# Vercel injects this deployment's API URL through the service binding.
# Keep an explicitly configured base URL available for local container checks.
if [ -z "${ApiSettings__BaseUrl:-}" ]; then
    : "${CSM_API_SERVICE_URL:?Vercel API service binding is missing}"
    export ApiSettings__BaseUrl="${CSM_API_SERVICE_URL%/}/api/"
fi

export ASPNETCORE_URLS="http://0.0.0.0:${PORT:-80}"
exec dotnet CSM.Web.dll
