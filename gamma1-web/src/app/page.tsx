'use client';

import { useEffect, useState, useRef } from 'react';

// Types
interface DroneNode {
  nodeId: string;
  role: string;
  binaryVersion: string;
  currentCpuLoad: number;
  ramUsedMb: number;
  isProbationary: boolean;
  totalTasksCompleted: number;
  lastHeartbeat: string;
}

interface PromptEntry {
  promptId: string;
  prompt: string;
  status: string;
  priority: number;
  createdAt: string;
  completedAt?: string;
}

interface ConsciousnessLine {
  id: string;
  text: string;
  type: 'event' | 'status' | 'warning' | 'error';
  timestamp: Date;
}

interface MeshStatus {
  activeNodes: number;
  totalNodes: number;
  meshHealth: number;
  nodes?: DroneNode[];
}

export default function NightframeConsole() {
  const [meshStatus, setMeshStatus] = useState<MeshStatus>({
    activeNodes: 0,
    totalNodes: 0,
    meshHealth: 0,
  });
  const [nodes, setNodes] = useState<DroneNode[]>([]);
  const [consciousness, setConsciousness] = useState<ConsciousnessLine[]>([]);
  const [prompts, setPrompts] = useState<PromptEntry[]>([]);
  const [inputPrompt, setInputPrompt] = useState('');
  const [isConnected, setIsConnected] = useState(false);
  const [activeTab, setActiveTab] = useState<'console' | 'nodes' | 'ledger' | 'gateway'>('console');

  // Gateway settings
  const [guestBandwidth, setGuestBandwidth] = useState(512);
  const [connectedGuests, setConnectedGuests] = useState(0);
  const [meshMembers, setMeshMembers] = useState(0);
  const [totalBytesServed, setTotalBytesServed] = useState(0);

  const consciousnessRef = useRef<HTMLDivElement>(null);

  // Simulated consciousness stream for demo
  useEffect(() => {
    const thoughts = [
      { text: '═══════════════════════════════════════════════', type: 'event' as const },
      { text: '◈ NIGHTFRAME ORCHESTRATOR v1.0.0 ONLINE', type: 'event' as const },
      { text: '═══════════════════════════════════════════════', type: 'event' as const },
      { text: '◎ Initializing mesh network...', type: 'status' as const },
      { text: '◎ gRPC service listening on port 5001', type: 'status' as const },
      { text: '◎ SignalR hub ready for console connections', type: 'status' as const },
      { text: '◈ Awaiting drone connections...', type: 'event' as const },
    ];

    let index = 0;
    const interval = setInterval(() => {
      if (index < thoughts.length) {
        addConsciousnessLine(thoughts[index].text, thoughts[index].type);
        index++;
      } else {
        // Random periodic updates
        const updates = [
          { text: '◎ Mesh health check: All systems nominal', type: 'status' as const },
          { text: '∿ Scanning for new drone connections...', type: 'status' as const },
          { text: '◎ Processing pending job queue...', type: 'status' as const },
        ];
        const randomUpdate = updates[Math.floor(Math.random() * updates.length)];
        addConsciousnessLine(randomUpdate.text, randomUpdate.type);
      }
    }, 1500);

    setIsConnected(true);

    return () => clearInterval(interval);
  }, []);

  // Auto-scroll consciousness stream
  useEffect(() => {
    if (consciousnessRef.current) {
      consciousnessRef.current.scrollTop = consciousnessRef.current.scrollHeight;
    }
  }, [consciousness]);

  const addConsciousnessLine = (text: string, type: ConsciousnessLine['type']) => {
    setConsciousness(prev => [...prev.slice(-100), {
      id: `${Date.now()}-${Math.random()}`,
      text,
      type,
      timestamp: new Date(),
    }]);
  };

  const handleSubmitPrompt = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputPrompt.trim()) return;

    const promptId = `PROMPT_${Date.now()}`;
    const newPrompt: PromptEntry = {
      promptId,
      prompt: inputPrompt,
      status: 'queued',
      priority: 5,
      createdAt: new Date().toISOString(),
    };

    setPrompts(prev => [newPrompt, ...prev]);
    addConsciousnessLine(`◈ Prompt received: "${inputPrompt.slice(0, 50)}..."`, 'event');
    addConsciousnessLine(`⟐ Queuing for distributed processing...`, 'status');

    setInputPrompt('');

    // Simulate processing
    setTimeout(() => {
      addConsciousnessLine(`◎ Prompt ${promptId.slice(0, 16)} now processing`, 'status');
      setPrompts(prev => prev.map(p =>
        p.promptId === promptId ? { ...p, status: 'processing' } : p
      ));
    }, 2000);

    setTimeout(() => {
      addConsciousnessLine(`◈ Prompt ${promptId.slice(0, 16)} completed`, 'event');
      setPrompts(prev => prev.map(p =>
        p.promptId === promptId ? { ...p, status: 'completed', completedAt: new Date().toISOString() } : p
      ));
    }, 5000);
  };

  // Simulated nodes for demo
  useEffect(() => {
    const demoNodes: DroneNode[] = [
      { nodeId: 'NFRAME_A1B2C3D4', role: 'ROLE_COMPUTE', binaryVersion: '1.0.0', currentCpuLoad: 0.45, ramUsedMb: 4096, isProbationary: false, totalTasksCompleted: 127, lastHeartbeat: new Date().toISOString() },
      { nodeId: 'NFRAME_E5F6G7H8', role: 'ROLE_STORAGE', binaryVersion: '1.0.0', currentCpuLoad: 0.12, ramUsedMb: 2048, isProbationary: false, totalTasksCompleted: 45, lastHeartbeat: new Date().toISOString() },
      { nodeId: 'NFRAME_I9J0K1L2', role: 'ROLE_GENERAL', binaryVersion: '1.0.0', currentCpuLoad: 0.78, ramUsedMb: 6144, isProbationary: true, totalTasksCompleted: 12, lastHeartbeat: new Date().toISOString() },
    ];

    setTimeout(() => {
      setNodes(demoNodes);
      setMeshStatus({
        activeNodes: 3,
        totalNodes: 3,
        meshHealth: 0.87,
        nodes: demoNodes,
      });
      addConsciousnessLine('◈ 3 drones connected to mesh', 'event');
    }, 3000);
  }, []);

  return (
    <div className="min-h-screen relative z-10">
      {/* Header */}
      <header className="glass-card mx-4 mt-4 mb-6 p-4 flex items-center justify-between animate-fade-in">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-purple-600 to-cyan-500 flex items-center justify-center animate-glow">
            <span className="text-2xl">◈</span>
          </div>
          <div>
            <h1 className="text-xl font-bold gradient-text">NIGHTFRAME</h1>
            <p className="text-sm text-[--nf-text-muted]">Decentralized AI Mesh Console</p>
          </div>
        </div>

        <div className="flex items-center gap-6">
          {/* Mesh Status */}
          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-sm text-[--nf-text-muted]">Mesh Health</div>
              <div className="text-lg font-bold text-[--nf-success]">
                {(meshStatus.meshHealth * 100).toFixed(0)}%
              </div>
            </div>
            <div className="text-right">
              <div className="text-sm text-[--nf-text-muted]">Active Nodes</div>
              <div className="text-lg font-bold text-[--nf-accent-primary]">
                {meshStatus.activeNodes}/{meshStatus.totalNodes}
              </div>
            </div>
          </div>

          {/* Connection Status */}
          <div className="flex items-center gap-2">
            <div className={`status-dot ${isConnected ? 'online' : 'offline'}`}></div>
            <span className="text-sm text-[--nf-text-secondary]">
              {isConnected ? 'Connected' : 'Disconnected'}
            </span>
          </div>
        </div>
      </header>

      {/* Tab Navigation */}
      <nav className="mx-4 mb-4 flex gap-2">
        {(['console', 'nodes', 'ledger', 'gateway'] as const).map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`px-6 py-2 rounded-lg font-medium transition-all ${activeTab === tab
              ? 'bg-[--nf-accent-primary] text-white'
              : 'bg-[--nf-bg-tertiary] text-[--nf-text-secondary] hover:text-white'
              }`}
          >
            {tab.charAt(0).toUpperCase() + tab.slice(1)}
          </button>
        ))}
      </nav>

      <main className="px-4 pb-8">
        {activeTab === 'console' && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 animate-fade-in">
            {/* Consciousness Stream */}
            <div className="lg:col-span-2 glass-card p-6">
              <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
                <span className="text-[--nf-accent-primary]">⟐</span>
                Consciousness Stream
              </h2>
              <div
                ref={consciousnessRef}
                className="h-96 overflow-y-auto font-mono text-sm bg-[--nf-bg-primary] rounded-lg p-4"
              >
                {consciousness.map(line => (
                  <div key={line.id} className={`consciousness-line ${line.type}`}>
                    <span className="text-[--nf-text-muted] mr-3">
                      {line.timestamp.toLocaleTimeString()}
                    </span>
                    {line.text}
                  </div>
                ))}
              </div>
            </div>

            {/* Prompt Input & Recent */}
            <div className="glass-card p-6">
              <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
                <span className="text-[--nf-accent-secondary]">◎</span>
                Neural Interface
              </h2>

              <form onSubmit={handleSubmitPrompt} className="mb-6">
                <textarea
                  value={inputPrompt}
                  onChange={e => setInputPrompt(e.target.value)}
                  placeholder="Enter your prompt for the mesh..."
                  className="nf-input h-32 resize-none mb-3"
                />
                <button type="submit" className="glow-button w-full">
                  Submit to Mesh
                </button>
              </form>

              <div>
                <h3 className="text-sm font-semibold text-[--nf-text-muted] mb-3">Recent Prompts</h3>
                <div className="space-y-2 max-h-48 overflow-y-auto">
                  {prompts.length === 0 ? (
                    <p className="text-sm text-[--nf-text-muted]">No prompts yet</p>
                  ) : (
                    prompts.map(p => (
                      <div key={p.promptId} className="p-3 bg-[--nf-bg-tertiary] rounded-lg">
                        <div className="flex items-center justify-between mb-1">
                          <span className={`chip ${p.status === 'completed' ? 'compute' :
                            p.status === 'processing' ? 'general' : 'relay'
                            }`}>
                            {p.status}
                          </span>
                          <span className="text-xs text-[--nf-text-muted]">
                            {new Date(p.createdAt).toLocaleTimeString()}
                          </span>
                        </div>
                        <p className="text-sm text-[--nf-text-secondary] truncate">
                          {p.prompt}
                        </p>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        {activeTab === 'nodes' && (
          <div className="animate-fade-in">
            <div className="glass-card p-6">
              <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
                <span className="text-[--nf-accent-tertiary]">∿</span>
                Mesh Nodes
              </h2>

              {nodes.length === 0 ? (
                <div className="text-center py-12">
                  <div className="text-6xl mb-4 opacity-50">◎</div>
                  <p className="text-[--nf-text-muted]">Waiting for drone connections...</p>
                </div>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {nodes.map(node => (
                    <div key={node.nodeId} className="p-4 bg-[--nf-bg-tertiary] rounded-xl border border-[--nf-border] hover:border-[--nf-accent-primary] transition-all">
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-2">
                          <div className={`mesh-node ${node.role.includes('COMPUTE') ? 'compute' : node.role.includes('STORAGE') ? 'storage' : ''}`}>
                            <span className="text-lg">
                              {node.role.includes('COMPUTE') ? '⚡' :
                                node.role.includes('STORAGE') ? '💾' : '🔗'}
                            </span>
                          </div>
                          <div>
                            <div className="font-semibold text-sm">{node.nodeId}</div>
                            <span className={`chip ${node.role.includes('COMPUTE') ? 'compute' : node.role.includes('STORAGE') ? 'storage' : 'general'}`}>
                              {node.role.replace('ROLE_', '')}
                            </span>
                          </div>
                        </div>
                        <div className={`status-dot ${node.isProbationary ? 'updating' : 'online'}`}></div>
                      </div>

                      <div className="space-y-2 text-sm">
                        <div className="flex justify-between">
                          <span className="text-[--nf-text-muted]">CPU Load</span>
                          <span>{(node.currentCpuLoad * 100).toFixed(0)}%</span>
                        </div>
                        <div className="progress-bar">
                          <div className="progress-bar-fill" style={{ width: `${node.currentCpuLoad * 100}%` }}></div>
                        </div>

                        <div className="flex justify-between">
                          <span className="text-[--nf-text-muted]">RAM Used</span>
                          <span>{(node.ramUsedMb / 1024).toFixed(1)} GB</span>
                        </div>

                        <div className="flex justify-between">
                          <span className="text-[--nf-text-muted]">Tasks</span>
                          <span>{node.totalTasksCompleted}</span>
                        </div>

                        {node.isProbationary && (
                          <div className="text-[--nf-warning] text-xs mt-2">
                            ⚠ Shadow Mode (Probationary)
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {activeTab === 'ledger' && (
          <div className="animate-fade-in">
            <div className="glass-card p-6">
              <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
                <span className="text-[--nf-success]">∴</span>
                Credit Ledger
              </h2>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Total Credits Issued</div>
                  <div className="text-2xl font-bold text-[--nf-success]">12,847</div>
                </div>
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Total Debits</div>
                  <div className="text-2xl font-bold text-[--nf-accent-tertiary]">3,421</div>
                </div>
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Network Balance</div>
                  <div className="text-2xl font-bold text-[--nf-accent-primary]">9,426</div>
                </div>
              </div>

              <h3 className="text-sm font-semibold text-[--nf-text-muted] mb-3">Top Contributors</h3>
              <div className="space-y-2">
                {nodes.map((node, i) => (
                  <div key={node.nodeId} className="flex items-center justify-between p-3 bg-[--nf-bg-tertiary] rounded-lg">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-purple-600 to-cyan-500 flex items-center justify-center font-bold">
                        {i + 1}
                      </div>
                      <span>{node.nodeId}</span>
                    </div>
                    <div className="flex items-center gap-4">
                      <span className="text-[--nf-success]">+{(node.totalTasksCompleted * 100).toLocaleString()}</span>
                      <span className={`chip ${node.role.includes('COMPUTE') ? 'compute' : 'storage'}`}>
                        {node.role.replace('ROLE_', '')}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {activeTab === 'gateway' && (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 animate-fade-in">
            {/* Gateway Controls */}
            <div className="glass-card p-6">
              <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
                <span className="text-[--nf-accent-primary]">🌐</span>
                Internet Gateway Settings
              </h2>

              <div className="space-y-6">
                {/* Guest Bandwidth Control */}
                <div>
                  <label className="block text-sm text-[--nf-text-muted] mb-2">
                    Guest Bandwidth Limit
                  </label>
                  <div className="flex items-center gap-4">
                    <input
                      type="range"
                      min="128"
                      max="10240"
                      step="128"
                      value={guestBandwidth}
                      onChange={(e) => setGuestBandwidth(parseInt(e.target.value))}
                      className="flex-1 h-2 bg-[--nf-bg-tertiary] rounded-lg appearance-none cursor-pointer"
                    />
                    <span className="text-lg font-bold text-[--nf-accent-primary] min-w-[100px] text-right">
                      {guestBandwidth} Kbps
                    </span>
                  </div>
                  <p className="text-xs text-[--nf-text-muted] mt-2">
                    Bandwidth for devices that haven't installed the drone
                  </p>
                </div>

                {/* Quick Presets */}
                <div>
                  <label className="block text-sm text-[--nf-text-muted] mb-2">
                    Quick Presets
                  </label>
                  <div className="flex gap-2">
                    {[256, 512, 1024, 2048].map(preset => (
                      <button
                        key={preset}
                        onClick={() => setGuestBandwidth(preset)}
                        className={`px-4 py-2 rounded-lg text-sm font-medium transition-all ${guestBandwidth === preset
                            ? 'bg-[--nf-accent-primary] text-white'
                            : 'bg-[--nf-bg-tertiary] text-[--nf-text-secondary] hover:bg-[--nf-bg-secondary]'
                          }`}
                      >
                        {preset >= 1024 ? `${preset / 1024} Mbps` : `${preset} Kbps`}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Member Bandwidth */}
                <div>
                  <label className="block text-sm text-[--nf-text-muted] mb-2">
                    Mesh Member Bandwidth
                  </label>
                  <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl flex items-center justify-between">
                    <span className="text-[--nf-text-secondary]">Devices with drone installed</span>
                    <span className="text-lg font-bold text-[--nf-success]">∞ Unlimited</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Gateway Statistics */}
            <div className="glass-card p-6">
              <h2 className="text-lg font-bold mb-4 flex items-center gap-2">
                <span className="text-[--nf-accent-secondary]">📊</span>
                Gateway Statistics
              </h2>

              <div className="grid grid-cols-2 gap-4 mb-6">
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Connected Guests</div>
                  <div className="text-2xl font-bold text-[--nf-accent-primary]">{connectedGuests}</div>
                </div>
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Mesh Members</div>
                  <div className="text-2xl font-bold text-[--nf-success]">{meshMembers}</div>
                </div>
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Data Served</div>
                  <div className="text-2xl font-bold text-[--nf-accent-secondary]">
                    {(totalBytesServed / 1024 / 1024 / 1024).toFixed(2)} GB
                  </div>
                </div>
                <div className="p-4 bg-[--nf-bg-tertiary] rounded-xl">
                  <div className="text-sm text-[--nf-text-muted] mb-1">Status</div>
                  <div className="text-2xl font-bold text-[--nf-success]">Online</div>
                </div>
              </div>

              <h3 className="text-sm font-semibold text-[--nf-text-muted] mb-3">Platform Breakdown</h3>
              <div className="space-y-2">
                <div className="flex items-center justify-between p-3 bg-[--nf-bg-tertiary] rounded-lg">
                  <div className="flex items-center gap-3">
                    <span className="text-xl">📱</span>
                    <span>iOS (No Install)</span>
                  </div>
                  <span className="chip compute">Free Internet</span>
                </div>
                <div className="flex items-center justify-between p-3 bg-[--nf-bg-tertiary] rounded-lg">
                  <div className="flex items-center gap-3">
                    <span className="text-xl">🤖</span>
                    <span>Android (Scout)</span>
                  </div>
                  <span className="chip storage">Full Member</span>
                </div>
                <div className="flex items-center justify-between p-3 bg-[--nf-bg-tertiary] rounded-lg">
                  <div className="flex items-center gap-3">
                    <span className="text-xl">💻</span>
                    <span>Desktop (Full Node)</span>
                  </div>
                  <span className="chip storage">Full Member</span>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="text-center py-4 text-sm text-[--nf-text-muted]">
        NIGHTFRAME v1.0.0 | Decentralized AI Mesh Network | {new Date().getFullYear()}
      </footer>
    </div>
  );
}
