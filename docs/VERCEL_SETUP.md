# Vercel Deployment

## Quick Deploy

[![Deploy with Vercel](https://vercel.com/button)](https://vercel.com/new/clone)

## Manual Deployment

```bash
# Install Vercel CLI
npm install -g vercel

# Navigate to web console
cd gamma1-web

# Link and deploy
vercel link
vercel --prod
```

## Environment Variables

Configure in Vercel Dashboard:

| Variable | Description |
|----------|-------------|
| `NEXT_PUBLIC_ORCHESTRATOR_URL` | Orchestrator URL |
| `NEXT_PUBLIC_SIGNALR_HUB` | SignalR Hub URL |

## Project Configuration

The `vercel.json` in the root configures:
- Build output directory
- Routing rules
- Environment settings

---

Copyright © 2024 Malachi Hooper. All rights reserved.
