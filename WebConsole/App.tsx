import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Dimensions, Platform, StatusBar } from 'react-native';
import { Network, Terminal, Zap, Globe, LayoutDashboard } from 'lucide-react-native';
import { colors } from './src/theme';
import { agentApi } from './src/api/agent';

// Screens
import { DashboardScreen } from './src/screens/DashboardScreen';
import { TerminalScreen } from './src/screens/TerminalScreen';
import { NodesScreen } from './src/screens/NodesScreen';
import { SettingsScreen } from './src/screens/SettingsScreen';
import { NavigationSidebar } from './src/components/NavigationSidebar';

export default function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [logs, setLogs] = useState<string[]>([]);
  const [nodes, setNodes] = useState<any[]>([]);
  const [status, setStatus] = useState<any>(null);

  // Global poller for state
  useEffect(() => {
    // Notify Core of presence (Visit)
    agentApi.notifyVisit();

    const interval = setInterval(async () => {
      // 1. Logs
      const newLogs = await agentApi.getConsciousness();
      if (newLogs.length !== logs.length) setLogs(newLogs.slice(-50));

      // 2. Nodes
      const newNodes = await agentApi.getNodes();
      setNodes(newNodes);

      // 3. Status
      const newStatus = await agentApi.getStatus();
      setStatus(newStatus);
    }, 1000);
    return () => clearInterval(interval);
  }, []);

  const renderContent = () => {
    switch (activeTab) {
      case 'dashboard':
        return <DashboardScreen logs={logs} nodes={nodes} status={status} />;
      case 'terminal':
        return <TerminalScreen />;
      case 'nodes':
        return <NodesScreen nodes={nodes} />;
      case 'settings':
        return <SettingsScreen />;
      default:
        return <DashboardScreen logs={logs} nodes={nodes} status={status} />;
    }
  };

  return (
    <View style={styles.container}>
      <StatusBar barStyle="light-content" />

      {/* Sidebar */}
      <NavigationSidebar activeTab={activeTab} onTabChange={setActiveTab} />

      {/* Main Content */}
      <View style={styles.main}>
        <View style={styles.header}>
          <View>
            <Text style={styles.headerTitle}>NIGHTFRAME // <Text style={{ color: colors.primary }}>CORE</Text></Text>
            <Text style={styles.headerSubtitle}>System Control Command • {activeTab.toUpperCase()}</Text>
          </View>
          <View style={styles.statusBadge}>
            <View style={[styles.statusDot, { backgroundColor: status?.status === 'Online' ? colors.secondary : colors.accent }]} />
            <Text style={styles.statusText}>{status?.status || 'CONNECTING...'}</Text>
          </View>
        </View>

        {renderContent()}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    flexDirection: 'row',
    backgroundColor: colors.background,
    fontFamily: Platform.OS === 'web' ? 'Inter, sans-serif' : 'System',
  },
  sidebar: {
    width: 70,
    backgroundColor: colors.surface,
    alignItems: 'center',
    paddingTop: 30,
    borderRightWidth: 1,
    borderRightColor: colors.border,
  },
  logoContainer: {
    marginBottom: 40,
    shadowColor: colors.primary,
    shadowOpacity: 0.5,
    shadowRadius: 10,
  },
  navItem: {
    marginBottom: 30,
    padding: 10,
  },
  navItemActive: {
    backgroundColor: colors.surfaceLight,
    borderRadius: 8,
  },
  main: {
    flex: 1,
    padding: 24,
    // Add max width for very large screens if needed?
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 24,
  },
  headerTitle: {
    color: colors.text,
    fontSize: 24,
    fontWeight: 'bold',
    letterSpacing: 2,
  },
  headerSubtitle: {
    color: colors.textDim,
    fontSize: 12,
    marginTop: 4,
    letterSpacing: 0.5,
  },
  statusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surfaceLight,
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: colors.border,
  },
  statusDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 8,
  },
  statusText: {
    color: colors.textDim,
    fontSize: 12,
    fontWeight: '600',
  },
});
