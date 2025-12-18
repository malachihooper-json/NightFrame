# Drone

## Desktop Mesh Node Implementation

The Drone project is the primary implementation of a NIGHTFRAME compute node. It is capable of performing local inference tasks, managing cellular intelligence modules, and facilitating network propagation.

### Core Modules

- **Compute Infrastructure**: Integrated ONNX Runtime utilizing hardware acceleration (CUDA, DirectML, CoreML).
- **Cellular Intelligence**: Low-level serial interface for AT-command interaction with RF modems. Supports RF fingerprinting and handover prediction.
- **Autonomous Networking**: Handles peer discovery via UDP/mDNS and establishes secure gRPC channels with the Orchestrator.
- **Propagation Logic**: Implements platform-aware captive portals to facilitate automated node onboarding.

### Configuration

Node identity is established via ECDSA keys generated on first execution. These keys are used to sign all outgoing telemetry and compute results.

### Hardware Interfacing

- **Neural Accelerators**: Automatic detection of available compute providers.
- **LTE/5G Modems**: Serial port communication via standard COM/tty interfaces.
