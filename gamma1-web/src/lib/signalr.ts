/**
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SIGNALR CONNECTION MANAGER                              ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Manages real-time WebSocket connection to NIGHTFRAME Orchestrator        ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

import * as signalR from '@microsoft/signalr';

// ═══════════════════════════════════════════════════════════════════════════════
//                              TYPES
// ═══════════════════════════════════════════════════════════════════════════════

export interface ConsciousnessEvent {
    text: string;
    type: 'event' | 'status' | 'warning' | 'error';
    timestamp: string;
}

export interface DroneNode {
    nodeId: string;
    role: string;
    binaryVersion: string;
    currentCpuLoad: number;
    ramUsedMb: number;
    isProbationary: boolean;
    totalTasksCompleted: number;
    lastHeartbeat: string;
    // Cellular fields (v2.0)
    hasCellular?: boolean;
    cellularTechnology?: string;
    signalStrength?: number;
    // Neural fields (v2.0)
    hasGpu?: boolean;
    gpuName?: string;
    estimatedFlops?: number;
    // Location
    latitude?: number;
    longitude?: number;
    locationConfidence?: number;
}

export interface CellularUpdate {
    nodeId: string;
    technology: string;
    rsrp: number;
    rsrq: number;
    sinr: number;
    cellId: number;
    neighborCount: number;
    timestamp: string;
}

export interface PromptProgress {
    promptId: string;
    status: 'queued' | 'processing' | 'completed' | 'failed';
    progress: number;
    result?: string;
    error?: string;
}

export interface MeshStatus {
    activeNodes: number;
    totalNodes: number;
    meshHealth: number;
    totalCredits: number;
    pendingPrompts: number;
}

export interface NightframeEvents {
    onConsciousness: (event: ConsciousnessEvent) => void;
    onNodeConnected: (node: DroneNode) => void;
    onNodeDisconnected: (nodeId: string) => void;
    onNodeUpdated: (node: DroneNode) => void;
    onCellularUpdate: (update: CellularUpdate) => void;
    onPromptProgress: (progress: PromptProgress) => void;
    onMeshStatusUpdate: (status: MeshStatus) => void;
    onConnectionStateChange: (state: 'connecting' | 'connected' | 'disconnected' | 'reconnecting') => void;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              SIGNALR MANAGER CLASS
// ═══════════════════════════════════════════════════════════════════════════════

class NightframeSignalR {
    private connection: signalR.HubConnection | null = null;
    private hubUrl: string;
    private events: Partial<NightframeEvents> = {};
    private reconnectAttempts = 0;
    private maxReconnectAttempts = 10;
    private isStarted = false;

    constructor(hubUrl?: string) {
        this.hubUrl = hubUrl || process.env.NEXT_PUBLIC_SIGNALR_HUB || 'http://localhost:5000/hub/console';
    }

    /**
     * Initialize and start the SignalR connection
     */
    async start(): Promise<boolean> {
        if (this.isStarted) {
            console.log('◎ SignalR already started');
            return true;
        }

        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(this.hubUrl)
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: (retryContext) => {
                        // Exponential backoff: 1s, 2s, 4s, 8s... max 30s
                        return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Register all event handlers
            this.registerHandlers();

            // Connection state handlers
            this.connection.onreconnecting(() => {
                console.log('∿ SignalR reconnecting...');
                this.events.onConnectionStateChange?.('reconnecting');
            });

            this.connection.onreconnected(() => {
                console.log('◈ SignalR reconnected');
                this.reconnectAttempts = 0;
                this.events.onConnectionStateChange?.('connected');
            });

            this.connection.onclose((error) => {
                console.log('◎ SignalR disconnected', error);
                this.isStarted = false;
                this.events.onConnectionStateChange?.('disconnected');
            });

            // Start connection
            this.events.onConnectionStateChange?.('connecting');
            await this.connection.start();

            this.isStarted = true;
            this.reconnectAttempts = 0;
            console.log('◈ SignalR connected to', this.hubUrl);
            this.events.onConnectionStateChange?.('connected');

            return true;
        } catch (error) {
            console.error('∴ SignalR connection failed:', error);
            this.events.onConnectionStateChange?.('disconnected');
            return false;
        }
    }

    /**
     * Stop the SignalR connection
     */
    async stop(): Promise<void> {
        if (this.connection) {
            await this.connection.stop();
            this.isStarted = false;
            console.log('◎ SignalR stopped');
        }
    }

    /**
     * Register event handlers for SignalR hub methods
     */
    private registerHandlers(): void {
        if (!this.connection) return;

        // Consciousness stream
        this.connection.on('ConsciousnessEvent', (event: ConsciousnessEvent) => {
            this.events.onConsciousness?.(event);
        });

        // Node events
        this.connection.on('NodeConnected', (node: DroneNode) => {
            this.events.onNodeConnected?.(node);
        });

        this.connection.on('NodeDisconnected', (nodeId: string) => {
            this.events.onNodeDisconnected?.(nodeId);
        });

        this.connection.on('NodeStatusUpdate', (node: DroneNode) => {
            this.events.onNodeUpdated?.(node);
        });

        // Cellular updates
        this.connection.on('CellularUpdate', (update: CellularUpdate) => {
            this.events.onCellularUpdate?.(update);
        });

        // Prompt progress
        this.connection.on('PromptProgress', (progress: PromptProgress) => {
            this.events.onPromptProgress?.(progress);
        });

        // Mesh status
        this.connection.on('MeshStatusUpdate', (status: MeshStatus) => {
            this.events.onMeshStatusUpdate?.(status);
        });
    }

    /**
     * Subscribe to events
     */
    on<K extends keyof NightframeEvents>(event: K, handler: NightframeEvents[K]): void {
        this.events[event] = handler;
    }

    /**
     * Unsubscribe from events
     */
    off<K extends keyof NightframeEvents>(event: K): void {
        delete this.events[event];
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    //                              INVOKE METHODS (Client → Server)
    // ═══════════════════════════════════════════════════════════════════════════════

    /**
     * Submit a prompt for distributed processing
     */
    async submitPrompt(prompt: string, priority: number = 5): Promise<string> {
        if (!this.connection) throw new Error('Not connected');
        return await this.connection.invoke<string>('SubmitPrompt', prompt, priority);
    }

    /**
     * Request current mesh status
     */
    async getMeshStatus(): Promise<MeshStatus> {
        if (!this.connection) throw new Error('Not connected');
        return await this.connection.invoke<MeshStatus>('GetMeshStatus');
    }

    /**
     * Request list of all nodes
     */
    async getNodes(): Promise<DroneNode[]> {
        if (!this.connection) throw new Error('Not connected');
        return await this.connection.invoke<DroneNode[]>('GetNodes');
    }

    /**
     * Update gateway bandwidth settings
     */
    async setGuestBandwidth(kbps: number): Promise<void> {
        if (!this.connection) throw new Error('Not connected');
        await this.connection.invoke('SetGuestBandwidth', kbps);
    }

    /**
     * Get a specific node's details
     */
    async getNodeDetails(nodeId: string): Promise<DroneNode | null> {
        if (!this.connection) throw new Error('Not connected');
        return await this.connection.invoke<DroneNode | null>('GetNodeDetails', nodeId);
    }

    /**
     * Send a command to a specific node
     */
    async sendNodeCommand(nodeId: string, command: string, args?: Record<string, unknown>): Promise<boolean> {
        if (!this.connection) throw new Error('Not connected');
        return await this.connection.invoke<boolean>('SendNodeCommand', nodeId, command, args);
    }

    /**
     * Get connection state
     */
    get state(): signalR.HubConnectionState {
        return this.connection?.state ?? signalR.HubConnectionState.Disconnected;
    }

    /**
     * Check if connected
     */
    get isConnected(): boolean {
        return this.connection?.state === signalR.HubConnectionState.Connected;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              SINGLETON EXPORT
// ═══════════════════════════════════════════════════════════════════════════════

// Create singleton instance
let instance: NightframeSignalR | null = null;

export function getNightframeConnection(hubUrl?: string): NightframeSignalR {
    if (!instance) {
        instance = new NightframeSignalR(hubUrl);
    }
    return instance;
}

export { NightframeSignalR };
export default getNightframeConnection;
