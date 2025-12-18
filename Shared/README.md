# Shared Components

## Interface Definitions and Message Contracts

The Shared project contains the core abstractions and data models utilized by all nodes in the NIGHTFRAME mesh network.

### Principal Interfaces

- **INeuralCompute**: Protocol for sharding and executing model inference.
- **ICellularIntelligence**: Interface for RF sensing and location prediction.
- **INetworkNode**: Base definition for node discovery and identity management.

### Data Contracts

Contains Protobuf definitions and C# message classes for gRPC and SignalR communication, ensuring type safety and binary compatibility across the mesh.
