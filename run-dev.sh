#!/bin/bash

echo "Starting backend..."
dotnet watch run --project apps/api/pixels_site.Api.csproj &
BACKEND_PID=$!

echo "Starting frontend..."
cd apps/web && npm run dev &
FRONTEND_PID=$!

echo ""
echo "Backend: http://localhost:5080"
echo "Frontend: http://localhost:5173"
echo ""
echo "Press Ctrl+C to stop both servers"

trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null" EXIT

wait
