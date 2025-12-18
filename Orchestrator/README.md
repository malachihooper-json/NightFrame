# Orchestrator

## Central Command Node

The Orchestrator is the central hub of the NIGHTFRAME mesh network. It manages node registration, task distribution, and the credit ledger system.

### Core Services
- **Shard Coordinator**: Manages the partitioning and distribution of neural network shards across the mesh.
- **Drone Registry**: Maintains a real-time registry of connected nodes, their hardware profiles, and probationary status.
- **Ledger Service**: Handles the cryptographic verification of compute results and maintains the credit-based economy.
- **Cell Coordinator**: Synchronizes cellular telemetry from scout nodes for global RF mapping.

### Communication Interfaces
- **gRPC (Port 5001)**: Primary bidirectional streaming interface for node-to-node and node-to-orchestrator communication.
- **SignalR Hub**: Provides high-frequency telemetry updates to connected administrative consoles.
- **REST API**: Standard endpoints for mesh status, task submission, and administrative control.
