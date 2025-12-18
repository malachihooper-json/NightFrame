import React, { useState } from 'react';
import { View, Text, ScrollView, TouchableOpacity, StyleSheet, Platform, Alert } from 'react-native';
import { Server, Shield, Cpu, Activity, Plus, HardDrive } from 'lucide-react-native';
import { colors } from '../theme';
import { GlassPane } from '../components/GlassPane';
import { agentApi } from '../api/agent';

interface NodesScreenProps {
    nodes: any[];
}

export const NodesScreen = ({ nodes }: NodesScreenProps) => {

    // Calculate network stats
    const totalCpu = nodes.reduce((acc, node) => acc + (node.cpuLoad || 0), 0) / (nodes.length || 1);
    const totalRam = nodes.reduce((acc, node) => acc + (node.ramUsage || 0), 0);
    const onlineNodes = nodes.filter(n => n.status === 'Online').length;

    const handleDeploy = async () => {
        const success = await agentApi.deployNode('Compute');
        if (success) {
            if (Platform.OS === 'web') {
                window.alert('Deployment signal sent to Network Core.');
            } else {
                Alert.alert('Deployment Initiated', 'Signal sent to Network Core.');
            }
        }
    };

    const formatBytes = (bytes: number) => {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    };

    const getNodeIcon = (role: string) => {
        switch (role) {
            case 'Defense': return <Shield size={20} color={colors.secondary} />;
            case 'Infiltration': return <Activity size={20} color={colors.accent} />;
            default: return <Server size={20} color={colors.primary} />;
        }
    };

    return (
        <View style={styles.container}>
            {/* Network Summary Bar */}
            <GlassPane style={styles.summaryBar}>
                <View style={styles.summaryItem}>
                    <Activity size={24} color={colors.primary} />
                    <View>
                        <Text style={styles.summaryLabel}>NETWORK HEALTH</Text>
                        <Text style={styles.summaryValue}>OPTIMAL</Text>
                    </View>
                </View>
                <View style={[styles.vertDivider]} />
                <View style={styles.summaryItem}>
                    <Server size={24} color={colors.text} />
                    <View>
                        <Text style={styles.summaryLabel}>ACTIVE NODES</Text>
                        <Text style={styles.summaryValue}>{onlineNodes} / {nodes.length}</Text>
                    </View>
                </View>
                <View style={[styles.vertDivider]} />
                <View style={styles.summaryItem}>
                    <Cpu size={24} color={colors.secondary} />
                    <View>
                        <Text style={styles.summaryLabel}>AVG CORE LOAD</Text>
                        <Text style={styles.summaryValue}>{Math.round(totalCpu * 100)}%</Text>
                    </View>
                </View>
                <View style={[styles.vertDivider]} />
                <View style={styles.summaryItem}>
                    <HardDrive size={24} color={colors.accent} />
                    <View>
                        <Text style={styles.summaryLabel}>TOTAL MEMORY</Text>
                        <Text style={styles.summaryValue}>{formatBytes(totalRam)}</Text>
                    </View>
                </View>
            </GlassPane>

            <ScrollView contentContainerStyle={styles.grid}>
                {nodes.map((node, i) => (
                    <GlassPane key={i} style={styles.nodeCard}>
                        <View style={styles.cardHeader}>
                            <View style={styles.nodeIconContainer}>
                                {getNodeIcon(node.role)}
                            </View>
                            <View style={{ flex: 1, marginLeft: 12 }}>
                                <Text style={styles.nodeName}>{node.hostname}</Text>
                                <Text style={styles.nodeId}>{node.nodeId}</Text>
                            </View>
                            <View style={[styles.statusTag, { backgroundColor: node.status === 'Online' ? 'rgba(16, 185, 129, 0.2)' : 'rgba(244, 63, 94, 0.2)' }]}>
                                <Text style={[styles.statusText, { color: node.status === 'Online' ? colors.secondary : colors.accent }]}>
                                    {node.status.toUpperCase()}
                                </Text>
                            </View>
                        </View>

                        <View style={styles.cardBody}>
                            <View style={styles.statRow}>
                                <Text style={styles.statLabel}>Role</Text>
                                <Text style={styles.statValue}>{node.role}</Text>
                            </View>

                            <View style={styles.meterContainer}>
                                <View style={styles.meterHeader}>
                                    <Text style={styles.meterLabel}>CPU</Text>
                                    <Text style={styles.meterValue}>{Math.round((node.cpuLoad || 0) * 100)}%</Text>
                                </View>
                                <View style={styles.track}>
                                    <View style={[styles.fill, { width: `${(node.cpuLoad || 0) * 100}%`, backgroundColor: colors.primary }]} />
                                </View>
                            </View>

                            <View style={styles.meterContainer}>
                                <View style={styles.meterHeader}>
                                    <Text style={styles.meterLabel}>RAM</Text>
                                    <Text style={styles.meterValue}>{formatBytes(node.ramUsage || 0)}</Text>
                                </View>
                                <View style={styles.track}>
                                    <View style={[styles.fill, { width: '40%', backgroundColor: colors.secondary }]} />
                                </View>
                            </View>
                        </View>

                        <TouchableOpacity style={styles.manageBtn}>
                            <Text style={styles.manageBtnText}>MANAGE NODE</Text>
                        </TouchableOpacity>
                    </GlassPane>
                ))}

                {/* Deploy New Node Card */}
                <TouchableOpacity style={styles.addCard} onPress={handleDeploy}>
                    <Plus size={40} color={colors.textDim} />
                    <Text style={styles.addText}>DEPLOY NEW NODE</Text>
                </TouchableOpacity>

            </ScrollView>
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    summaryBar: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        paddingVertical: 15,
        marginBottom: 20,
    },
    summaryItem: {
        flexDirection: 'row',
        alignItems: 'center',
        flex: 1,
        justifyContent: 'center',
    },
    vertDivider: {
        width: 1,
        backgroundColor: colors.border,
        height: '80%',
        alignSelf: 'center',
    },
    summaryLabel: {
        color: colors.textDim,
        fontSize: 10,
        marginLeft: 10,
        fontWeight: 'bold',
        letterSpacing: 1,
    },
    summaryValue: {
        color: colors.text,
        fontSize: 16,
        marginLeft: 10,
        fontWeight: 'bold',
    },
    grid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: 20,
        paddingBottom: 40,
    },
    nodeCard: {
        width: '31%', // 3 columns gap 
        minWidth: 300,
        marginBottom: 20,
    },
    cardHeader: {
        flexDirection: 'row',
        alignItems: 'center',
        marginBottom: 20,
    },
    nodeIconContainer: {
        width: 40,
        height: 40,
        borderRadius: 20,
        backgroundColor: colors.surfaceLight,
        alignItems: 'center',
        justifyContent: 'center',
        borderWidth: 1,
        borderColor: colors.border,
    },
    nodeName: {
        color: colors.text,
        fontWeight: 'bold',
        fontSize: 16,
    },
    nodeId: {
        color: colors.textDim,
        fontSize: 12,
        fontFamily: Platform.OS === 'web' ? 'monospace' : 'System',
    },
    statusTag: {
        paddingHorizontal: 8,
        paddingVertical: 2,
        borderRadius: 4,
    },
    statusText: {
        fontSize: 10,
        fontWeight: 'bold',
    },
    cardBody: {
        gap: 15,
        marginBottom: 20,
    },
    statRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
    },
    statLabel: {
        color: colors.textDim,
        fontSize: 12,
    },
    statValue: {
        color: colors.text,
        fontWeight: '600',
    },
    meterContainer: {
        gap: 5,
    },
    meterHeader: {
        flexDirection: 'row',
        justifyContent: 'space-between',
    },
    meterLabel: {
        color: colors.textDim,
        fontSize: 10,
        fontWeight: 'bold',
    },
    meterValue: {
        color: colors.text,
        fontSize: 10,
    },
    track: {
        height: 4,
        backgroundColor: colors.surfaceLight,
        borderRadius: 2,
        overflow: 'hidden',
    },
    fill: {
        height: '100%',
        borderRadius: 2,
    },
    manageBtn: {
        backgroundColor: colors.surfaceLight,
        padding: 10,
        borderRadius: 6,
        alignItems: 'center',
        borderWidth: 1,
        borderColor: colors.border,
    },
    manageBtnText: {
        color: colors.primary,
        fontSize: 11,
        fontWeight: 'bold',
        letterSpacing: 1,
    },
    addCard: {
        width: '31%',
        minWidth: 300,
        height: 250, // approx height of other cards
        borderWidth: 2,
        borderColor: colors.border,
        borderStyle: 'dashed',
        borderRadius: 16,
        alignItems: 'center',
        justifyContent: 'center',
        marginBottom: 20,
        opacity: 0.5,
    },
    addText: {
        color: colors.textDim,
        marginTop: 10,
        fontWeight: 'bold',
        letterSpacing: 1,
    },
});
