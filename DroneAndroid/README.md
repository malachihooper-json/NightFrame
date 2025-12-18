# Drone Android (Scout Node)

## Mobile Mesh Scout

A .NET MAUI implementation of the NIGHTFRAME node, optimized for mobile deployment and RF environment scanning.

### Features

- **RF Scanning**: Continuous monitoring of Wi-Fi and Bluetooth environments for mesh discovery.
- **GPS Integration**: Provides high-accuracy telemetry for RF fingerprinting training data.
- **Mobile Inference**: Utilizes on-device AI accelerators (NPU) where supported via ONNX.
- **Mesh Connectivity**: Connects to the primary Orchestrator as a lightweight scout node.

### Deployment

Target API Level: 33+ (Android 13.0 or higher).
Requires permissions for Location (Fine), Bluetooth (Admin), and Wi-Fi State.
