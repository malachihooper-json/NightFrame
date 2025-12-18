/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    DRONE CORE - MAIN CONTROL LOOP                          ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Orchestrates all drone subsystems: network, compute, cellular, stealth.   ║
 * ║  Integrates Cellular Intelligence for RF location and handover prediction. ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Grpc.Net.Client;
using NIGHTFRAME.Drone.Hardware;
using NIGHTFRAME.Drone.Network;
using NIGHTFRAME.Drone.Compute;
using NIGHTFRAME.Drone.Cellular;
using NIGHTFRAME.Orchestrator.Grpc;

// Alias to resolve ambiguity
using LocalHardwareSpecs = NIGHTFRAME.Drone.Hardware.HardwareSpecs;
using GrpcLocationInfo = NIGHTFRAME.Orchestrator.Grpc.LocationInfo;

namespace NIGHTFRAME.Drone.Core;

public class DroneCore : IDisposable
{
    private readonly NodeIdentity _identity;
    private readonly LocalHardwareSpecs _specs;
    private readonly string _orchestratorAddress;
    private readonly StealthController _stealth;
    private readonly ComputeEngine _compute;
    
    // NEW: Cellular Intelligence integration
    private CellularIntelligence? _cellular;
    private readonly string? _modemPort;
    private readonly bool _cellularEnabled;
    
    private GrpcChannel? _channel;
    private NightframeOrchestrator.NightframeOrchestratorClient? _client;
    private NodeRole _assignedRole = NodeRole.RoleUnknown;
    private GlobalState? _globalState;
    
    private bool _isConnected = false;
    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 100;
    private bool _disposed = false;
    
    // Latest location (from GPS or RF fingerprinting)
    private GrpcLocationInfo? _currentLocation;
    
    /// <summary>
    /// Creates a DroneCore with optional cellular intelligence.
    /// </summary>
    /// <param name="identity">Node identity</param>
    /// <param name="specs">Hardware specifications</param>
    /// <param name="orchestratorAddress">Orchestrator address</param>
    /// <param name="modemPort">Serial port for cellular modem (null to disable cellular)</param>
    public DroneCore(
        NodeIdentity identity, 
        LocalHardwareSpecs specs, 
        string orchestratorAddress,
        string? modemPort = null)
    {
        _identity = identity;
        _specs = specs;
        _orchestratorAddress = orchestratorAddress;
        _modemPort = modemPort;
        _cellularEnabled = !string.IsNullOrEmpty(modemPort);
        _stealth = new StealthController();
        _compute = new ComputeEngine();
    }
    
    /// <summary>
    /// Gets the current cellular intelligence service (null if unavailable).
    /// </summary>
    public CellularIntelligence? Cellular => _cellular;
    
    /// <summary>
    /// Gets the current location (from GPS or RF fingerprinting).
    /// </summary>
    public GrpcLocationInfo? CurrentLocation => _currentLocation;
    
    /// <summary>
    /// Gets whether cellular intelligence is active.
    /// </summary>
    public bool CellularActive => _cellular != null && _cellular.IsRunning;
    
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("◎ Starting drone core...");
        
        // Start stealth monitoring
        _stealth.Start();
        
        // Initialize cellular intelligence if enabled
        if (_cellularEnabled)
        {
            await InitializeCellularAsync(ct);
        }
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Connect to orchestrator
                if (!_isConnected)
                {
                    await ConnectToOrchestratorAsync(ct);
                }
                
                // Run main work loop
                await WorkLoopAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"∴ Connection error: {ex.Message}");
                _isConnected = false;
                _reconnectAttempts++;
                
                if (_reconnectAttempts >= MaxReconnectAttempts)
                {
                    Console.WriteLine("∴ Max reconnect attempts reached. Entering offline mode.");
                    await OfflineModeAsync(ct);
                    _reconnectAttempts = 0;
                }
                else
                {
                    // Exponential backoff
                    var delay = Math.Min(30000, 1000 * Math.Pow(2, _reconnectAttempts));
                    Console.WriteLine($"◎ Reconnecting in {delay/1000:F0}s...");
                    await Task.Delay((int)delay, ct);
                }
            }
        }
        
        // Cleanup
        await ShutdownAsync();
    }
    
    /// <summary>
    /// Initializes cellular intelligence service.
    /// </summary>
    private async Task InitializeCellularAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_modemPort))
        {
            Console.WriteLine("◎ Cellular disabled (no modem port specified)");
            return;
        }
        
        try
        {
            Console.WriteLine($"◎ Initializing Cellular Intelligence on {_modemPort}...");
            _cellular = new CellularIntelligence(_modemPort);
            
            // Wire up events
            _cellular.OnHandoverRecommended += OnHandoverRecommended;
            _cellular.OnLocationUpdated += OnLocationUpdated;
            
            await _cellular.StartAsync();
            
            Console.WriteLine("◈ Cellular Intelligence active:");
            Console.WriteLine($"  RF Location: {_cellular.RFLocationAvailable}");
            Console.WriteLine($"  Handover Prediction: {_cellular.HandoverPredictionAvailable}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Failed to initialize cellular: {ex.Message}");
            _cellular?.Dispose();
            _cellular = null;
        }
    }
    
    /// <summary>
    /// Handles handover recommendations from cellular intelligence.
    /// </summary>
    private void OnHandoverRecommended(HandoverPrediction prediction)
    {
        Console.WriteLine($"⚠ Handover recommended -> Cell {prediction.RecommendedCellId}");
        Console.WriteLine($"  Reason: {prediction.Reason}");
        Console.WriteLine($"  Time: {prediction.TimeToHandoverMs:F0}ms");
        
        // Execute handover if urgent
        if (prediction.HandoverImminent && prediction.TimeToHandoverMs < 1000)
        {
            Console.WriteLine("◎ Executing immediate handover...");
            _ = _cellular?.ExecuteHandoverAsync(prediction);
        }
    }
    
    /// <summary>
    /// Handles location updates from cellular intelligence.
    /// </summary>
    private void OnLocationUpdated(LocationPrediction location)
    {
        _currentLocation = new GrpcLocationInfo
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            AccuracyMeters = location.ConfidenceRadius,
            Confidence = location.Confidence,
            Source = LocationSource.LocationRfFingerprint,
            TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
    
    private async Task ConnectToOrchestratorAsync(CancellationToken ct)
    {
        Console.WriteLine($"◎ Connecting to orchestrator: {_orchestratorAddress}");
        
        _channel = GrpcChannel.ForAddress(_orchestratorAddress);
        _client = new NightframeOrchestrator.NightframeOrchestratorClient(_channel);
        
        // Build registration manifest
        var manifest = BuildManifest();
        
        var response = await _client.RegisterAsync(manifest, cancellationToken: ct);
        
        if (!response.Accepted)
        {
            Console.WriteLine($"∴ Registration rejected: {response.Reason}");
            
            if (response.Reason == "OUTDATED")
            {
                Console.WriteLine("◎ Initiating self-update...");
                await SelfUpdateAsync(response.GlobalState, ct);
                throw new InvalidOperationException("Update required");
            }
            
            throw new InvalidOperationException($"Registration rejected: {response.Reason}");
        }
        
        _assignedRole = response.AssignedRole;
        _globalState = response.GlobalState;
        _isConnected = true;
        _reconnectAttempts = 0;
        
        Console.WriteLine($"◈ Connected! Assigned role: {_assignedRole}");
        Console.WriteLine($"◎ Active nodes in mesh: {_globalState.ActiveNodeCount}");
        
        // Log cellular mesh info
        if (_globalState.TotalCellMeasurements > 0)
        {
            Console.WriteLine($"◎ Cell measurements in mesh: {_globalState.TotalCellMeasurements}");
            Console.WriteLine($"◎ Geographic regions covered: {_globalState.GeographicRegionsCovered}");
        }
        
        // Check if hydration needed
        if (response.Reason == "NEEDS_HYDRATION")
        {
            Console.WriteLine("◎ Model hydration required...");
            // TODO: Download model shards from storage nodes
        }
        
        // Sync cell database if available
        if (response.CellDatabaseInfo != null && _cellular != null)
        {
            Console.WriteLine($"◎ Cell database: {response.CellDatabaseInfo.TotalRecords} towers available");
            // TODO: Sync cell database if our version is outdated
        }
    }
    
    /// <summary>
    /// Builds the registration manifest including all capabilities.
    /// </summary>
    private DroneManifest BuildManifest()
    {
        var manifest = new DroneManifest
        {
            NodeId = _identity.NodeId,
            BinaryVersion = "2.0.0",
            Specs = BuildHardwareSpecs(),
            PublicKey = Google.Protobuf.ByteString.CopyFrom(_identity.PublicKey),
            UptimeSeconds = (long)(DateTime.UtcNow - _identity.CreatedAt).TotalSeconds,
            Cellular = BuildCellularCapabilities(),
            Neural = BuildNeuralCapabilities(),
            Location = _currentLocation
        };
        manifest.CachedModels.AddRange(_compute.GetCachedModels());
        
        return manifest;
    }
    
    private NIGHTFRAME.Orchestrator.Grpc.HardwareSpecs BuildHardwareSpecs()
    {
        return new NIGHTFRAME.Orchestrator.Grpc.HardwareSpecs
        {
            RamMb = _specs.RamMb,
            CpuCores = _specs.CpuCores,
            DiskFreeMb = _specs.DiskFreeMb,
            HasGpu = _specs.HasGpu,
            GpuName = _specs.GpuName ?? "",
            GpuVramMb = _specs.GpuVramMb,
            CurrentCpuLoad = _stealth.CurrentCpuLoad,
            EstimatedFlops = _specs.EstimatedFlops,
            ExecutionProvider = _compute.Capabilities.ExecutionProviders.FirstOrDefault() ?? "CPU"
        };
    }
    
    private CellularCapabilities BuildCellularCapabilities()
    {
        if (_cellular == null)
        {
            return new CellularCapabilities { Available = false };
        }
        
        var lastMeasurement = _cellular.LastMeasurement;
        var lastPrediction = _cellular.LastHandoverPrediction;
        
        return new CellularCapabilities
        {
            Available = true,
            ServingCellId = lastMeasurement?.CellId ?? 0,
            Rsrp = lastMeasurement?.RSRP ?? 0,
            Rsrq = lastMeasurement?.RSRQ ?? 0,
            Sinr = lastMeasurement?.SINR ?? 0,
            NeighborCellCount = _cellular.NeighborCells?.Count ?? 0,
            RadioType = lastMeasurement?.RadioType ?? "LTE",
            CarrierName = "", // TODO: Get from modem
            RfLocationAvailable = _cellular.RFLocationAvailable,
            HandoverPredictionAvailable = _cellular.HandoverPredictionAvailable,
            Mcc = lastMeasurement?.MCC ?? 0,
            Mnc = lastMeasurement?.MNC ?? 0
        };
    }
    
    private NeuralCapabilities BuildNeuralCapabilities()
    {
        var caps = _compute.Capabilities;
        var neuralCaps = new NeuralCapabilities
        {
            OnnxAvailable = caps.OnnxAvailable,
            MaxModelSizeMb = caps.MaxModelSizeMb,
            MaxBatchSize = caps.MaxBatchSize,
            InferenceLatencyMs = caps.InferenceLatencyMs
        };
        neuralCaps.ExecutionProviders.AddRange(caps.ExecutionProviders);
        neuralCaps.LoadedModels.AddRange(caps.LoadedModels);
        return neuralCaps;
    }
    
    private async Task WorkLoopAsync(CancellationToken ct)
    {
        Console.WriteLine("◎ Entering work loop...");
        
        // Start bidirectional stream
        using var stream = _client!.Connect();
        
        // Start heartbeat task
        var heartbeatTask = HeartbeatLoopAsync(stream.RequestStream, ct);
        
        // Start compute loop if we're a compute/general node
        var computeTask = _assignedRole switch
        {
            NodeRole.RoleCompute or NodeRole.RoleGeneral => ComputeLoopAsync(ct),
            _ => Task.CompletedTask
        };
        
        // Start cellular monitoring task if scout role
        var cellularTask = _assignedRole switch
        {
            NodeRole.RoleScout => CellularMonitorLoopAsync(stream.RequestStream, ct),
            _ => Task.CompletedTask
        };
        
        // Listen for commands from orchestrator
        while (await stream.ResponseStream.MoveNext(ct))
        {
            await HandleCommandAsync(stream.ResponseStream.Current, ct);
        }
        
        await Task.WhenAll(heartbeatTask, computeTask, cellularTask);
    }
    
    private async Task HeartbeatLoopAsync(
        Grpc.Core.IClientStreamWriter<DroneMessage> stream, 
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isConnected)
        {
            // Throttle if user is active
            await _stealth.WaitForIdleAsync(ct);
            
            var heartbeat = new Heartbeat
            {
                NodeId = _identity.NodeId,
                CpuLoad = _stealth.CurrentCpuLoad,
                RamUsedMb = GC.GetTotalMemory(false) / (1024 * 1024),
                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Cellular = BuildCellularCapabilities(),
                Location = _currentLocation
            };
            
            await stream.WriteAsync(new DroneMessage { Heartbeat = heartbeat });
            
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
    
    /// <summary>
    /// Cellular monitoring loop for Scout role - reports signal measurements.
    /// </summary>
    private async Task CellularMonitorLoopAsync(
        Grpc.Core.IClientStreamWriter<DroneMessage> stream,
        CancellationToken ct)
    {
        if (_cellular == null)
        {
            Console.WriteLine("∴ Scout role assigned but no cellular available");
            return;
        }
        
        Console.WriteLine("◎ Cellular monitoring loop started (Scout mode)");
        
        while (!ct.IsCancellationRequested && _isConnected)
        {
            try
            {
                var measurement = _cellular.LastMeasurement;
                var handover = _cellular.LastHandoverPrediction;
                var neighbors = _cellular.NeighborCells ?? new List<NeighborCell>();
                
                if (measurement != null)
                {
                    var update = new CellularStatusUpdate
                    {
                        NodeId = _identity.NodeId,
                        ServingCellId = measurement.CellId,
                        Rsrp = measurement.RSRP,
                        Rsrq = measurement.RSRQ,
                        Sinr = measurement.SINR,
                        HandoverImminent = handover?.HandoverImminent ?? false,
                        RecommendedCellId = handover?.RecommendedCellId ?? 0,
                        TimeToHandoverMs = handover?.TimeToHandoverMs ?? 0
                    };
                    
                    // Add neighbor cells
                    foreach (var neighbor in neighbors.Take(6))
                    {
                        update.Neighbors.Add(new NeighborCellInfo
                        {
                            CellId = neighbor.CellId,
                            PhysicalCellId = neighbor.PhysicalCellId,
                            Rsrp = neighbor.RSRP,
                            Rsrq = neighbor.RSRQ,
                            Earfcn = neighbor.EARFCN
                        });
                    }
                    
                    await stream.WriteAsync(new DroneMessage { CellularUpdate = update });
                }
                
                // Also send location updates
                if (_currentLocation != null)
                {
                    await stream.WriteAsync(new DroneMessage 
                    { 
                        LocationUpdate = new LocationUpdate
                        {
                            NodeId = _identity.NodeId,
                            Location = _currentLocation
                        }
                    });
                }
                
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ Cellular update error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }
    
    private async Task ComputeLoopAsync(CancellationToken ct)
    {
        Console.WriteLine("◎ Compute loop started - requesting work shards...");
        
        while (!ct.IsCancellationRequested && _isConnected)
        {
            // Throttle if user is active
            await _stealth.WaitForIdleAsync(ct);
            
            try
            {
                // Request a shard with neural capabilities
                var request = new ShardRequest
                {
                    NodeId = _identity.NodeId,
                    PreferredModel = _globalState?.CurrentModelHash ?? "",
                    NodeCapabilities = BuildNeuralCapabilities()
                };
                
                var shard = await _client!.RequestShardAsync(request, cancellationToken: ct);
                
                if (string.IsNullOrEmpty(shard.ShardId))
                {
                    // No work available
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }
                
                Console.WriteLine($"◈ Received shard {shard.ShardId} (layers {shard.StartLayer}-{shard.EndLayer})");
                
                // Process the shard
                var startTime = DateTime.UtcNow;
                var output = await _compute.ProcessShardAsync(shard, ct);
                var computeTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                
                // Submit result with execution provider info
                var result = new ShardResult
                {
                    ShardId = shard.ShardId,
                    NodeId = _identity.NodeId,
                    OutputData = Google.Protobuf.ByteString.CopyFrom(output),
                    ComputeTimeMs = computeTime,
                    ResultSignature = Google.Protobuf.ByteString.CopyFrom(
                        _identity.Sign(output)),
                    ExecutionProvider = _compute.Capabilities.ExecutionProviders.FirstOrDefault() ?? "CPU",
                    MemoryUsedMb = (int)(GC.GetTotalMemory(false) / (1024 * 1024))
                };
                
                var ack = await _client.SubmitResultAsync(result, cancellationToken: ct);
                
                if (ack.Accepted)
                {
                    Console.WriteLine($"◎ Shard completed in {computeTime}ms (+{ack.CreditsEarned} credits)");
                }
                else
                {
                    Console.WriteLine($"∴ Result rejected: {ack.RejectionReason}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ Compute error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
    
    private async Task HandleCommandAsync(MothershipCommand command, CancellationToken ct)
    {
        switch (command.PayloadCase)
        {
            case MothershipCommand.PayloadOneofCase.Execute:
                Console.WriteLine($"◈ Execute task: {command.Execute.TaskType}");
                // TODO: Execute task
                break;
            
            case MothershipCommand.PayloadOneofCase.Update:
                Console.WriteLine($"◈ Update command: {command.Update.NewVersion}");
                await SelfUpdateAsync(null, ct);
                break;
            
            case MothershipCommand.PayloadOneofCase.Hydrate:
                Console.WriteLine($"◈ Hydrate model from {command.Hydrate.StoragePeer}");
                await _compute.HydrateModelAsync(
                    command.Hydrate.ModelHash,
                    command.Hydrate.ShardStartLayer,
                    command.Hydrate.ShardEndLayer,
                    command.Hydrate.StoragePeer,
                    ct);
                break;
            
            case MothershipCommand.PayloadOneofCase.RoleChange:
                Console.WriteLine($"◈ Role changed to {command.RoleChange.NewRole}: {command.RoleChange.Reason}");
                _assignedRole = command.RoleChange.NewRole;
                break;
            
            case MothershipCommand.PayloadOneofCase.PeerIntro:
                Console.WriteLine($"◈ Peer introduction: {command.PeerIntro.PeerNodeId} @ {command.PeerIntro.PeerAddress}");
                // Log cellular info if available
                if (command.PeerIntro.PeerCellular?.Available == true)
                {
                    Console.WriteLine($"  Peer cellular: Cell {command.PeerIntro.PeerCellular.ServingCellId}");
                }
                break;
            
            case MothershipCommand.PayloadOneofCase.Terminate:
                Console.WriteLine($"∴ Terminate command: {command.Terminate.Reason}");
                await ShutdownAsync();
                Environment.Exit(0);
                break;
            
            case MothershipCommand.PayloadOneofCase.CellularCmd:
                await HandleCellularCommandAsync(command.CellularCmd, ct);
                break;
            
            case MothershipCommand.PayloadOneofCase.LocationReq:
                Console.WriteLine($"◈ Location request (high accuracy: {command.LocationReq.HighAccuracy})");
                // Trigger location update
                if (_currentLocation != null)
                {
                    // Location already available
                    Console.WriteLine($"  Current: {_currentLocation.Latitude:F6}, {_currentLocation.Longitude:F6}");
                }
                break;
        }
    }
    
    /// <summary>
    /// Handles cellular-specific commands from orchestrator.
    /// </summary>
    private async Task HandleCellularCommandAsync(CellularCommand cmd, CancellationToken ct)
    {
        if (_cellular == null)
        {
            Console.WriteLine("∴ Cellular command received but no cellular available");
            return;
        }
        
        switch (cmd.ActionCase)
        {
            case CellularCommand.ActionOneofCase.ForceHandover:
                Console.WriteLine($"◈ Force handover to cell {cmd.ForceHandover.TargetCellId}");
                var success = await _cellular.ExecuteHandoverAsync(
                    new HandoverPrediction
                    {
                        RecommendedCellId = cmd.ForceHandover.TargetCellId,
                        HandoverImminent = true,
                        Reason = HandoverReason.None
                    });
                Console.WriteLine(success ? "  Handover successful" : "  Handover failed");
                break;
            
            case CellularCommand.ActionOneofCase.StartDriveTest:
                Console.WriteLine($"◈ Starting drive test ({cmd.StartDriveTest.DurationSeconds}s)");
                // TODO: Start drive test session
                break;
            
            case CellularCommand.ActionOneofCase.StopDriveTest:
                Console.WriteLine("◈ Stopping drive test");
                // TODO: Stop drive test session
                break;
            
            case CellularCommand.ActionOneofCase.SyncDatabase:
                Console.WriteLine($"◈ Sync cell database v{cmd.SyncDatabase.DatabaseVersion}");
                // TODO: Sync cell database
                break;
        }
    }
    
    private async Task SelfUpdateAsync(GlobalState? state, CancellationToken ct)
    {
        Console.WriteLine("◎ Initiating self-update via watchdog...");
        
        // Signal watchdog to perform update
        var watchdogSignal = Path.Combine(AppContext.BaseDirectory, "update_ready.signal");
        
        if (state != null)
        {
            // Download new binary
            var tempPath = Path.Combine(AppContext.BaseDirectory, "brain_new.tmp");
            
            Console.WriteLine("◎ Downloading new binary...");
            using var updateStream = _client!.RequestUpdate(new UpdateRequest
            {
                NodeId = _identity.NodeId,
                CurrentVersion = "2.0.0",
                TargetPlatform = "win-x64"
            }, cancellationToken: ct);
            
            await using var fileStream = File.Create(tempPath);
            while (await updateStream.ResponseStream.MoveNext(ct))
            {
                var chunk = updateStream.ResponseStream.Current;
                await fileStream.WriteAsync(chunk.Data.Memory, ct);
            }
            
            Console.WriteLine("◎ Download complete. Signaling watchdog...");
        }
        
        // Signal watchdog
        await File.WriteAllTextAsync(watchdogSignal, DateTime.UtcNow.ToString("o"), ct);
        
        // Wait for watchdog to take over
        Console.WriteLine("◎ Waiting for watchdog...");
        await Task.Delay(5000, ct);
        
        // If we're still running, watchdog failed
        Console.WriteLine("∴ Watchdog did not respond. Continuing with current version.");
    }
    
    private async Task OfflineModeAsync(CancellationToken ct)
    {
        Console.WriteLine("◎ Entering offline mode...");
        Console.WriteLine("◎ Will retry connection every 5 minutes.");
        
        // In offline mode, cellular intelligence continues to collect data
        if (_cellular != null)
        {
            Console.WriteLine("◎ Cellular intelligence continues locally.");
        }
        
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
            
            try
            {
                await ConnectToOrchestratorAsync(ct);
                Console.WriteLine("◈ Connection restored!");
                return;
            }
            catch
            {
                Console.WriteLine("∴ Still offline...");
            }
        }
    }
    
    private async Task ShutdownAsync()
    {
        Console.WriteLine("◎ Shutting down drone core...");
        
        _stealth.Stop();
        
        if (_cellular != null)
        {
            _cellular.OnHandoverRecommended -= OnHandoverRecommended;
            _cellular.OnLocationUpdated -= OnLocationUpdated;
            _cellular.Stop();
            _cellular.Dispose();
        }
        
        _compute.Dispose();
        _channel?.Dispose();
        
        Console.WriteLine("◈ Drone shutdown complete.");
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _ = ShutdownAsync();
    }
}
