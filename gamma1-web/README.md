# NIGHTFRAME Web Console

## Mesh Network Control Interface

Administrative dashboard for monitoring and managing the NIGHTFRAME mesh network. Built with Next.js and React.

---

## Functional Components

| Feature | Technical Description |
|---------|-----------------------|
| **Telemetry Stream** | Real-time system event logging and node status updates |
| **Neural Interface** | Input gateway for task submission to the distributed mesh |
| **Node Management** | Real-time monitoring of node metrics (CPU utilization, RAM, task load) |
| **Credit Ledger** | Transaction history and balance tracking for the economic layer |
| **Gateway Configuration** | Bandwidth allocation and network traffic management |

---

## Technical Stack

| Dependency | Purpose |
|------------|---------|
| **Next.js 16** | Application framework and routing |
| **React 19** | UI component architecture |
| **TailwindCSS 4** | Styling and layout orchestration |
| **TypeScript 5** | Static type checking and safety |
| **SignalR** | Real-time bidirectional telemetry |

---

## Deployment and Installation

### Local Development

1. Install dependencies:
   ```bash
   npm install
   ```

2. Configure environment variables:
   Create a `.env.local` file with the following:
   ```env
   NEXT_PUBLIC_ORCHESTRATOR_URL=http://localhost:5000
   NEXT_PUBLIC_SIGNALR_HUB=http://localhost:5000/hub/console
   ```

3. Execute development server:
   ```bash
   npm run dev
   ```

### Vercel Deployment

The console is pre-configured for Vercel deployment. Ensure environment variables are set in the Vercel project settings.

---

## System Integration

The console interfaces with the NIGHTFRAME Orchestrator via two main channels:

1. **REST API**: Used for status retrieval, ledger queries, and task submission.
2. **SignalR Hub**: Synchronous telemetry stream for real-time node state and system events.

---

## Directory Structure

```
gamma1-web/
├── src/
│   └── app/
│       ├── page.tsx          # Dashboard implementation
│       ├── layout.tsx        # Application shell
│       └── globals.css       # System theme definitions
├── public/                 # Static assets
├── package.json            # Dependency manifest
├── next.config.ts          # Framework configuration
└── tsconfig.json           # Compiler options
```

---

## Quality Control

```bash
# Static type verification
npx tsc --noEmit

# Linting
npm run lint

# Production build
npm run build
```

---

## Compliance

Part of the NIGHTFRAME distributed computing framework.
