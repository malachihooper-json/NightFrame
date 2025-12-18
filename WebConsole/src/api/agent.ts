import axios from 'axios';

let API_URL = 'http://localhost:7777';

// Allow configuration from the UI
export const setApiUrl = (url: string) => {
    API_URL = url.replace(/\/$/, ''); // Remove trailing slash
    console.log('API URL updated to:', API_URL);
};

export const getApiUrl = () => API_URL;

// Mock data for development when backend is offline
const MOCK_NODES = [
    { nodeId: 'N1', hostname: 'Alpha-01', role: 'Compute', status: 'Online', cpuLoad: 0.45, ramUsage: 1024 * 1024 * 500 },
    { nodeId: 'N2', hostname: 'Beta-Worker', role: 'General', status: 'Online', cpuLoad: 0.12, ramUsage: 1024 * 1024 * 200 },
    { nodeId: 'N3', hostname: 'Gamma-Infiltrator', role: 'Infiltration', status: 'Offline', cpuLoad: 0, ramUsage: 0 },
    { nodeId: 'N4', hostname: 'Delta-Host', role: 'Compute', status: 'Online', cpuLoad: 0.88, ramUsage: 1024 * 1024 * 8000 },
];

const MOCK_LOGS = [
    "◈ SYSTEM INITIALIZATION COMPLETE",
    "◎ Core Identity: REACT_CONSOLE_HQ",
    "⟁ Uplink established to Neural Mesh",
    "⟐ Subordinate Node Detected: Alpha-01",
    "◈ Command Authority Verified",
    "∿ Data Stream Active",
];

export const agentApi = {
    async getStatus() {
        try {
            const res = await axios.get(`${API_URL}/status`, { timeout: 2000 });
            return res.data;
        } catch (e) {
            console.warn('Backend offline, using mock status');
            return {
                nodeId: 'MOCK_AGENT',
                hostname: 'LOCALHOST',
                role: 'Host',
                status: 'Online',
                uptime: 12345
            };
        }
    },

    async getNodes() {
        try {
            const res = await axios.get(`${API_URL}/nodes`, { timeout: 2000 });
            return res.data;
        } catch (e) {
            return MOCK_NODES;
        }
    },

    async getConsciousness() {
        try {
            const res = await axios.get(`${API_URL}/consciousness`, { timeout: 2000 });
            return res.data.logs || [];
        } catch (e) {
            // Return random slice of mock logs to simulate activity
            if (Math.random() > 0.7) {
                MOCK_LOGS.push(`⟐ Processing shard ${Math.floor(Math.random() * 9999)}...`);
            }
            return MOCK_LOGS;
        }
    },

    async sendMessage(message: string) {
        try {
            const res = await axios.post(`${API_URL}/chat`, { message });
            return res.data.response;
        } catch (e) {
            return `[MOCK] Agent received: "${message}". Backend is offline.`;
        }
    },

    async deployNode(role: string) {
        try {
            await axios.post(`${API_URL}/nodes/deploy`, { role });
            return true;
        } catch (e) {
            console.log('[MOCK] Deploying node:', role);
            return true;
        }
    },

    async killNode(nodeId: string) {
        try {
            await axios.delete(`${API_URL}/nodes/${nodeId}`);
            return true;
        } catch (e) {
            console.log('[MOCK] Killing node:', nodeId);
            return true;
        }
    },

    async notifyVisit() {
        try {
            await axios.post(`${API_URL}/core/uplink?action=visit`);
        } catch (e) { /* silent fail */ }
    },

    async notifyShutdown() {
        try {
            await axios.post(`${API_URL}/core/uplink?action=shutdown`);
        } catch (e) { /* silent fail */ }
    }
};
