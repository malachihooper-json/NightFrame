# NIGHTFRAME

## Decentralized AI Mesh Network

NIGHTFRAME is a distributed computing platform designed for collaborative neural network inference across a mesh of autonomous nodes. The system integrates cellular intelligence, GPU-accelerated ONNX runtime environments, and automated network propagation mechanisms.

---

## Technical Specifications

| Component | Specification |
|-----------|---------------|
| **Distributed Inference** | Pipeline-parallel model sharding across heterogeneous nodes |
| **Cellular Intelligence** | RF fingerprinting and handover prediction using LSTM models |
| **Neural Compute** | ONNX Runtime integration with support for CUDA, DirectML, and CoreML |
| **Mesh Discovery** | Hybrid discovery utilizing UDP broadcast and mDNS peer exchange |
| **Network Propagation** | Platform-aware captive portal for automated node onboarding |
| **Economic Layer** | Credit-based incentive system for compute and storage contribution |

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          WEB CONSOLE (Next.js)                              │
└───────────────────────────────────────┬─────────────────────────────────────┘
                                        │ SignalR Telemetry
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ORCHESTRATOR (Primary Node)                         │
│                         ASP.NET Core / gRPC                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  Drone Registry │ Ledger Service │ Shard Coordinator │ Cell Coordinator      │
└───────────────────────────────────────┬─────────────────────────────────────┘
                                        │ gRPC Bidirectional Streaming
                        ┌───────────────┼───────────────┐
                        ▼               ▼               ▼
                  ┌─────────┐     ┌─────────┐     ┌─────────┐
                  │ DESKTOP │     │  SCOUT  │     │ ANDROID │
                  │  DRONE  │     │  DRONE  │     │  SCOUT  │
                  │─────────│     │─────────│     │─────────│
                  │ Compute │     │ Cellular│     │ Wi-Fi   │
                  │ Storage │     │ RF Loc  │     │ Scanning│
                  │ Gateway │     │ Predict │     │ GPS Map │
                  └─────────┘     └─────────┘     └─────────┘
```

---

## Navigation and Directory Structure

| Path | Description | Documentation |
|------|-------------|---------------|
| `Orchestrator/` | Central command node handling gRPC and SignalR | [Orchestrator Core](Orchestrator/README.md) |
| `Drone/` | Desktop node implementation with ONNX and Cellular modules | [Drone Docs](Drone/README.md) |
| `DroneAndroid/` | Mobile scout node implemented in .NET MAUI | [Mobile Docs](DroneAndroid/README.md) |
| `gamma1-web/` | Administrative web interface (Next.js) | [Web Interface](gamma1-web/README.md) |
| `Shared/` | Core interfaces and message contracts | [Interface Definitions](Shared/README.md) |
| `Watchdog/` | Supervisor process for node reliability and updates | [Watchdog Specs](Watchdog/README.md) |
| `docs/` | Technical specifications and setup guides | [Documentation Index](docs/) |

---

## Technical Deployment

### System Requirements
- .NET 8.0 SDK
- Node.js 18 or higher (Web Console)
- Supported OS: Windows 10/11, macOS, Linux (various distributions)

### Build Process

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd Limitless0.1
   ```

2. Compile the solution:
   ```bash
   dotnet build NIGHTFRAME.sln
   ```

3. Initialize the Orchestrator in Genesis mode:
   ```bash
   dotnet run --project Orchestrator -- --mode genesis
   ```

4. Launch a standalone Drone node:
   ```bash
   dotnet run --project Drone
   ```

---

## Cellular Logic and RF Telemetry

The cellular module implements the following:
- **RF Fingerprinting**: Localizes nodes without GPS using signal strength patterns (approx. 50-500m precision).
- **Handover Prediction**: Utilizes LSTM networks to predict optimal tower switching.
- **Modem Interfacing**: Low-level AT command support for Quectel, Telit, and Sierra Wireless hardware.
- **OpenCellID**: Synchronizes with global cell tower databases for initial localization.

---

## Neural Compute Providers

| Provider | Platform Support | Hardware Acceleration |
|----------|------------------|-----------------------|
| **CUDA** | Windows / Linux | NVIDIA GPU |
| **DirectML** | Windows | Microsoft DirectML compatible GPUs |
| **CoreML** | macOS | Apple Silicon / Metal |
| **CPU** | All | x64 / ARM64 instructions |

---

## Security and Integrity

- **ECDSA Identity**: Nodes are identified via cryptographic key pairs.
- **Node Probation**: New nodes undergo a "Shadow Mode" evaluation period.
- **Consensus Validation**: Inference results are verified across multiple nodes where redundancy is required.
- **Signed Attribution**: Every compute result is cryptographically signed by the providing node.

---

## Economic Model (Credit Ledger)

| Metric | Credit Impact |
|--------|---------------|
| Compute Shard Completion | +100 units |
| Storage Allocation (per GB) | +10 units |
| Verified Uptime (per hour) | +5 units |
| Inference Request (Submit) | -50 units |

---

## Project Status

| Platform | Role | Status |
|----------|------|--------|
| Windows | Full Node | Operational |
| macOS | Full Node | Operational |
| Linux | Full Node | Operational |
| Android | Scout Node | Development |

---

## Compliance and Legal

Copyright © 2024 Malachi Hooper. All rights reserved. Proprietary software.
