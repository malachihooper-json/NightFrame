/**
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    USE NIGHTFRAME HOOK                                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  React hook for real-time NIGHTFRAME mesh data via SignalR                ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import {
    getNightframeConnection,
    ConsciousnessEvent,
    DroneNode,
    CellularUpdate,
    PromptProgress,
    MeshStatus
} from '@/lib/signalr';

// ═══════════════════════════════════════════════════════════════════════════════
//                              TYPES
// ═══════════════════════════════════════════════════════════════════════════════

export interface ConsciousnessLine {
    id: string;
    text: string;
    type: 'event' | 'status' | 'warning' | 'error';
    timestamp: Date;
}

export interface PromptEntry {
    promptId: string;
    prompt: string;
    status: 'queued' | 'processing' | 'completed' | 'failed';
    priority: number;
    createdAt: string;
    completedAt?: string;
    result?: string;
    error?: string;
}

export type ConnectionState = 'connecting' | 'connected' | 'disconnected' | 'reconnecting';

export interface UseNightframeOptions {
    autoConnect?: boolean;
    maxConsciousnessLines?: number;
    hubUrl?: string;
}

export interface UseNightframeReturn {
    // Connection
    isConnected: boolean;
    connectionState: ConnectionState;
    connect: () => Promise<void>;
    disconnect: () => Promise<void>;

    // Data
    consciousness: ConsciousnessLine[];
    nodes: DroneNode[];
    meshStatus: MeshStatus | null;
    prompts: PromptEntry[];
    cellularData: Map<string, CellularUpdate>;

    // Actions
    submitPrompt: (prompt: string, priority?: number) => Promise<void>;
    refreshNodes: () => Promise<void>;
    refreshMeshStatus: () => Promise<void>;
    setGuestBandwidth: (kbps: number) => Promise<void>;
    sendNodeCommand: (nodeId: string, command: string, args?: Record<string, unknown>) => Promise<boolean>;

    // Helpers
    addConsciousnessLine: (text: string, type: ConsciousnessLine['type']) => void;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              HOOK IMPLEMENTATION
// ═══════════════════════════════════════════════════════════════════════════════

export function useNightframe(options: UseNightframeOptions = {}): UseNightframeReturn {
    const {
        autoConnect = true,
        maxConsciousnessLines = 200,
        hubUrl
    } = options;

    // State
    const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
    const [consciousness, setConsciousness] = useState<ConsciousnessLine[]>([]);
    const [nodes, setNodes] = useState<DroneNode[]>([]);
    const [meshStatus, setMeshStatus] = useState<MeshStatus | null>(null);
    const [prompts, setPrompts] = useState<PromptEntry[]>([]);
    const [cellularData, setCellularData] = useState<Map<string, CellularUpdate>>(new Map());

    // Refs
    const connectionRef = useRef(getNightframeConnection(hubUrl));
    const isInitialized = useRef(false);

    // ═════════════════════════════════════════════════════════════════════════════
    //                              HELPERS
    // ═════════════════════════════════════════════════════════════════════════════

    const addConsciousnessLine = useCallback((text: string, type: ConsciousnessLine['type']) => {
        setConsciousness(prev => {
            const newLine: ConsciousnessLine = {
                id: `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
                text,
                type,
                timestamp: new Date()
            };
            const updated = [...prev, newLine];
            // Trim to max lines
            return updated.slice(-maxConsciousnessLines);
        });
    }, [maxConsciousnessLines]);

    // ═════════════════════════════════════════════════════════════════════════════
    //                              EVENT HANDLERS
    // ═════════════════════════════════════════════════════════════════════════════

    const setupEventHandlers = useCallback(() => {
        const connection = connectionRef.current;

        // Connection state
        connection.on('onConnectionStateChange', (state) => {
            setConnectionState(state);
            if (state === 'connected') {
                addConsciousnessLine('◈ Connected to NIGHTFRAME Orchestrator', 'event');
            } else if (state === 'disconnected') {
                addConsciousnessLine('∴ Disconnected from Orchestrator', 'warning');
            } else if (state === 'reconnecting') {
                addConsciousnessLine('∿ Reconnecting to Orchestrator...', 'status');
            }
        });

        // Consciousness events
        connection.on('onConsciousness', (event: ConsciousnessEvent) => {
            addConsciousnessLine(event.text, event.type);
        });

        // Node events
        connection.on('onNodeConnected', (node: DroneNode) => {
            setNodes(prev => [...prev.filter(n => n.nodeId !== node.nodeId), node]);
            addConsciousnessLine(`◈ Node ${node.nodeId.slice(0, 12)} connected (${node.role.replace('ROLE_', '')})`, 'event');
        });

        connection.on('onNodeDisconnected', (nodeId: string) => {
            setNodes(prev => prev.filter(n => n.nodeId !== nodeId));
            addConsciousnessLine(`◎ Node ${nodeId.slice(0, 12)} disconnected`, 'status');
        });

        connection.on('onNodeUpdated', (node: DroneNode) => {
            setNodes(prev => prev.map(n => n.nodeId === node.nodeId ? node : n));
        });

        // Cellular updates
        connection.on('onCellularUpdate', (update: CellularUpdate) => {
            setCellularData(prev => {
                const newMap = new Map(prev);
                newMap.set(update.nodeId, update);
                return newMap;
            });
        });

        // Prompt progress
        connection.on('onPromptProgress', (progress: PromptProgress) => {
            setPrompts(prev => prev.map(p =>
                p.promptId === progress.promptId
                    ? { ...p, status: progress.status, result: progress.result, error: progress.error }
                    : p
            ));

            if (progress.status === 'completed') {
                addConsciousnessLine(`◈ Prompt ${progress.promptId.slice(0, 12)} completed`, 'event');
            } else if (progress.status === 'failed') {
                addConsciousnessLine(`∴ Prompt ${progress.promptId.slice(0, 12)} failed: ${progress.error}`, 'error');
            }
        });

        // Mesh status
        connection.on('onMeshStatusUpdate', (status: MeshStatus) => {
            setMeshStatus(status);
        });
    }, [addConsciousnessLine]);

    // ═════════════════════════════════════════════════════════════════════════════
    //                              ACTIONS
    // ═════════════════════════════════════════════════════════════════════════════

    const connect = useCallback(async () => {
        const connection = connectionRef.current;
        setConnectionState('connecting');

        const success = await connection.start();
        if (success) {
            // Fetch initial data
            try {
                const [nodesData, statusData] = await Promise.all([
                    connection.getNodes(),
                    connection.getMeshStatus()
                ]);
                setNodes(nodesData);
                setMeshStatus(statusData);
            } catch (error) {
                console.error('Failed to fetch initial data:', error);
            }
        }
    }, []);

    const disconnect = useCallback(async () => {
        await connectionRef.current.stop();
        setConnectionState('disconnected');
    }, []);

    const submitPrompt = useCallback(async (prompt: string, priority: number = 5) => {
        const connection = connectionRef.current;

        try {
            const promptId = await connection.submitPrompt(prompt, priority);

            const newPrompt: PromptEntry = {
                promptId,
                prompt,
                status: 'queued',
                priority,
                createdAt: new Date().toISOString()
            };

            setPrompts(prev => [newPrompt, ...prev]);
            addConsciousnessLine(`◈ Prompt queued: "${prompt.slice(0, 50)}..."`, 'event');
        } catch (error) {
            addConsciousnessLine(`∴ Failed to submit prompt: ${error}`, 'error');
            throw error;
        }
    }, [addConsciousnessLine]);

    const refreshNodes = useCallback(async () => {
        try {
            const nodesData = await connectionRef.current.getNodes();
            setNodes(nodesData);
        } catch (error) {
            console.error('Failed to refresh nodes:', error);
        }
    }, []);

    const refreshMeshStatus = useCallback(async () => {
        try {
            const status = await connectionRef.current.getMeshStatus();
            setMeshStatus(status);
        } catch (error) {
            console.error('Failed to refresh mesh status:', error);
        }
    }, []);

    const setGuestBandwidth = useCallback(async (kbps: number) => {
        try {
            await connectionRef.current.setGuestBandwidth(kbps);
            addConsciousnessLine(`◎ Guest bandwidth set to ${kbps} Kbps`, 'status');
        } catch (error) {
            addConsciousnessLine(`∴ Failed to set bandwidth: ${error}`, 'error');
            throw error;
        }
    }, [addConsciousnessLine]);

    const sendNodeCommand = useCallback(async (
        nodeId: string,
        command: string,
        args?: Record<string, unknown>
    ): Promise<boolean> => {
        try {
            const success = await connectionRef.current.sendNodeCommand(nodeId, command, args);
            if (success) {
                addConsciousnessLine(`◎ Command '${command}' sent to ${nodeId.slice(0, 12)}`, 'status');
            }
            return success;
        } catch (error) {
            addConsciousnessLine(`∴ Command failed: ${error}`, 'error');
            return false;
        }
    }, [addConsciousnessLine]);

    // ═════════════════════════════════════════════════════════════════════════════
    //                              LIFECYCLE
    // ═════════════════════════════════════════════════════════════════════════════

    useEffect(() => {
        if (isInitialized.current) return;
        isInitialized.current = true;

        // Setup event handlers
        setupEventHandlers();

        // Initial consciousness messages
        addConsciousnessLine('═══════════════════════════════════════════════', 'event');
        addConsciousnessLine('◈ NIGHTFRAME WEB CONSOLE v2.0', 'event');
        addConsciousnessLine('═══════════════════════════════════════════════', 'event');

        // Auto-connect if enabled
        if (autoConnect) {
            addConsciousnessLine('◎ Connecting to Orchestrator...', 'status');
            connect().catch(() => {
                // Connection failed, show demo mode
                addConsciousnessLine('∴ Orchestrator unavailable - running in demo mode', 'warning');
                setConnectionState('disconnected');
            });
        }

        // Cleanup on unmount
        return () => {
            connectionRef.current.stop();
        };
    }, [autoConnect, connect, setupEventHandlers, addConsciousnessLine]);

    // ═════════════════════════════════════════════════════════════════════════════
    //                              RETURN
    // ═════════════════════════════════════════════════════════════════════════════

    return {
        // Connection
        isConnected: connectionState === 'connected',
        connectionState,
        connect,
        disconnect,

        // Data
        consciousness,
        nodes,
        meshStatus,
        prompts,
        cellularData,

        // Actions
        submitPrompt,
        refreshNodes,
        refreshMeshStatus,
        setGuestBandwidth,
        sendNodeCommand,

        // Helpers
        addConsciousnessLine
    };
}

export default useNightframe;
