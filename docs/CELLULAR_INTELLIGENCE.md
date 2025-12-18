# Cellular Intelligence Module

## Overview

The Cellular Intelligence module provides GPS-free location capabilities using RF fingerprinting and handover prediction.

## Features

| Feature | Description |
|---------|-------------|
| **RF Fingerprinting** | GPS-free location (50-500m accuracy) |
| **Handover Prediction** | Preemptive tower switching via LSTM |
| **AT Command Control** | Quectel, Telit, Sierra modem support |
| **Drive-test Collection** | Training data acquisition |
| **OpenCellID Integration** | Cell tower database sync |

## Usage

```csharp
// Initialize cellular intelligence
var cellular = new CellularIntelligence("/dev/ttyUSB0");
await cellular.StartAsync();

// Get RF-predicted location
if (cellular.LastLocation != null)
{
    Console.WriteLine($"Location: {cellular.LastLocation.Latitude}, {cellular.LastLocation.Longitude}");
}

// Handle handover recommendations
cellular.OnHandoverRecommended += prediction => 
{
    Console.WriteLine($"Handover to {prediction.RecommendedCellId}");
};
```

## Supported Modems

- Quectel (EC25, EG25, BG96)
- Telit (LE910, ME910)
- Sierra Wireless (HL7800, WP76)

## Signal Measurements

| Metric | Description | Range |
|--------|-------------|-------|
| RSRP | Reference Signal Received Power | -140 to -44 dBm |
| RSRQ | Reference Signal Received Quality | -20 to -3 dB |
| RSSI | Received Signal Strength Indicator | -113 to -51 dBm |
| SINR | Signal to Interference + Noise Ratio | -20 to +30 dB |

---

Copyright © 2024 Malachi Hooper. All rights reserved.
