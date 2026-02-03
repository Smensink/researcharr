#!/bin/bash
# Script to trigger import process for finished downloads
# This calls the RefreshMonitoredDownloads command which will:
# 1. Refresh the download queue
# 2. Process any finished downloads that are pending import

API_KEY="${READARR_API_KEY:-}"
API_URL="${READARR_API_URL:-http://localhost:7337}"

if [ -z "$API_KEY" ]; then
    echo "Error: READARR_API_KEY environment variable not set"
    echo "Usage: READARR_API_KEY=your_api_key ./trigger_import.sh"
    exit 1
fi

echo "Triggering import process for finished downloads..."
curl -X POST "${API_URL}/api/v1/command" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${API_KEY}" \
  -d '{"name": "RefreshMonitoredDownloads"}'

echo ""
echo "Import process triggered. Check the logs for details."

