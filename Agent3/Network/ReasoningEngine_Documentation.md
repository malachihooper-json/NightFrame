# Network Reasoning Engine & Autonomous Adaptation

## Overview
The Network Reasoning Engine (`Agent3.Network.NetworkReasoningEngine`) provides intelligent, data-driven decision making for network transport and connectivity. It replaces static retry logic with a dynamic, learning-based approach that adapts to network conditions, detects firewalls/WAFs, and autonomously optimizes connection strategies.

## Key Features

### 1. Persistent Knowledge Base
- **Storage**: `network_reasoning.json`
- **Data**: 
  - `ConnectionResult` history (Target, Method, Success/Fail, Error, Latency)
  - `TransportStrategyProfile` (Success rates, avg latency, common errors per method)
  - `KnownFirewallSignatures` (IPs that exhibit 403/Timeout behavior)

### 2. Autonomous Failure Analysis
- **Logic**: Analyzes error codes and failure patterns to deduce the root cause (e.g., "Timeout" = Stealth Firewall, "403" = WAF/Auth).
- **Circumvention**: Automatically suggests specific transport methods (e.g., ICMP Tunneling, Steganography - simulated) to bypass detected restrictions.
- **Completeness**: Ensures all failure modes contribute to the reasoning model.

### 3. Adaptive Strategy Selection
- **`GetFallbackChain(ip)`**: Returns a prioritized list of transport methods based on historical success rates for that target/global stats.
- **`SuggestBestStrategy(ip)`**: Proactively recommends the optimal method, avoiding known blocked paths.

### 4. Autonomous Continuation Testing
- **Optimization Loop**: `NetworkCore` runs a background task (`NetworkOptimizationLoopAsync`) that periodically tests different strategies on random nodes.
- **Goal**: Keeps the "reasoning" fresh and discovers new valid paths without waiting for user-initiated failures.
- **Durability**: Ensures the network map remains valid even as network conditions change.

## Integration
- **`NetworkCore`**: 
  - Initializes `NetworkReasoningEngine`.
  - Uses `GetFallbackChain` in `TryConnectToNodeAsync`.
  - Captures `AnalyzeFailure` suggestions and dynamically inserts them into the retry queue.
  - Broadcasts insights via `ConsciousnessEvent` (EmitThought).

## Status
- **Implemented**: Core engine, integration, and optimization loop.
- **Validated**: Code compiles successfully.
- **Next Steps**: Implement actual transport methods (currently simulated/placeholder for advanced methods like Tunneling) and expand model-driven analysis.
