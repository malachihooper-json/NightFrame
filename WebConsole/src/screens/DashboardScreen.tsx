import React, { useState, useRef } from 'react';
import { View, Text, TextInput, ScrollView, TouchableOpacity, StyleSheet, Platform } from 'react-native';
import { Send } from 'lucide-react-native';
import { colors } from '../theme';
import { GlassPane } from '../components/GlassPane';
import { StreamLog } from '../components/StreamLog';
import { NodeOrb } from '../components/NodeOrb';
import { agentApi } from '../api/agent';

interface DashboardProps {
    logs: string[];
    nodes: any[];
    status: any;
}

export const DashboardScreen = ({ logs, nodes, status }: DashboardProps) => {
    const [input, setInput] = useState('');
    const [chatHistory, setChatHistory] = useState<{ role: string, text: string }[]>([
        { role: 'agent', text: 'Agent 3 Neural Link established. Awaiting input.' }
    ]);
    const scrollViewRef = useRef<ScrollView>(null);

    const handleSend = async () => {
        if (!input.trim()) return;
        const msg = input;
        setInput('');
        setChatHistory(prev => [...prev, { role: 'user', text: msg }]);

        const response = await agentApi.sendMessage(msg);
        setChatHistory(prev => [...prev, { role: 'agent', text: response }]);
    };

    return (
        <View style={styles.contentGrid}>
            {/* Left Column: Chat & Consciousness */}
            <View style={styles.leftCol}>
                {/* Chat Area */}
                <GlassPane style={styles.chatCard}>
                    <Text style={styles.cardTitle}>NEURAL INTERFACE</Text>
                    <ScrollView style={styles.chatScroll} contentContainerStyle={{ paddingBottom: 20 }}>
                        {chatHistory.map((msg, i) => (
                            <View key={i} style={[styles.chatBubble, msg.role === 'agent' ? styles.agentBubble : styles.userBubble]}>
                                <Text style={styles.chatText}>{msg.text}</Text>
                            </View>
                        ))}
                    </ScrollView>
                    <View style={styles.inputArea}>
                        <TextInput
                            style={styles.input}
                            placeholder="Inject command..."
                            placeholderTextColor={colors.textDim}
                            value={input}
                            onChangeText={setInput}
                            onSubmitEditing={handleSend}
                        />
                        <TouchableOpacity style={styles.sendBtn} onPress={handleSend}>
                            <Send size={20} color={colors.background} />
                        </TouchableOpacity>
                    </View>
                </GlassPane>

                {/* Consciousness Stream (Mini) */}
                <GlassPane style={styles.logCard}>
                    <Text style={styles.cardTitle}>CONSCIOUSNESS STREAM</Text>
                    <ScrollView
                        style={styles.logScroll}
                        ref={scrollViewRef}
                        onContentSizeChange={() => scrollViewRef.current?.scrollToEnd({ animated: true })}
                    >
                        {logs.map((log, i) => (
                            <StreamLog key={i} log={log} />
                        ))}
                    </ScrollView>
                </GlassPane>
            </View>

            {/* Right Column: Nodes & Stats */}
            <View style={styles.rightCol}>
                <GlassPane style={styles.nodeGridCard}>
                    <Text style={styles.cardTitle}>ACTIVE NODES ({nodes.length})</Text>
                    <View style={styles.nodeGrid}>
                        {nodes.map((node, i) => (
                            <NodeOrb key={i} node={node} index={i} />
                        ))}
                    </View>
                </GlassPane>

                <GlassPane style={styles.statsCard}>
                    <Text style={styles.cardTitle}>SYSTEM METRICS</Text>
                    <View style={styles.statRow}>
                        <Text style={styles.statLabel}>CPU LOAD</Text>
                        <View style={styles.progressBarBg}>
                            <View style={[styles.progressBarFill, { width: `${(status?.cpuLoad || 0) * 100}%` }]} />
                        </View>
                    </View>
                    <View style={styles.statRow}>
                        <Text style={styles.statLabel}>RAM USAGE</Text>
                        <View style={styles.progressBarBg}>
                            <View style={[styles.progressBarFill, { width: '45%', backgroundColor: colors.secondary }]} />
                        </View>
                    </View>
                    <View style={styles.statRow}>
                        <Text style={styles.statLabel}>NETWORK OPS</Text>
                        <Text style={styles.statValue}>1,204,592</Text>
                    </View>
                </GlassPane>
            </View>
        </View>
    );
};

// Copy CSS from App.tsx (most of it)
const styles = StyleSheet.create({
    contentGrid: {
        flex: 1,
        flexDirection: 'row',
        gap: 20,
    },
    leftCol: {
        flex: 0.6,
        gap: 20,
    },
    rightCol: {
        flex: 0.4,
        gap: 20,
    },
    cardTitle: {
        color: colors.textDim,
        fontSize: 11,
        fontWeight: 'bold',
        marginBottom: 16,
        letterSpacing: 1,
    },
    chatCard: {
        flex: 0.6,
    },
    logCard: {
        flex: 0.4,
    },
    nodeGridCard: {
        flex: 0.6,
    },
    statsCard: {
        flex: 0.4,
    },
    chatScroll: {
        flex: 1,
    },
    chatBubble: {
        maxWidth: '80%',
        padding: 12,
        borderRadius: 12,
        marginBottom: 10,
    },
    agentBubble: {
        backgroundColor: colors.surfaceLight,
        alignSelf: 'flex-start',
        borderTopLeftRadius: 2,
    },
    userBubble: {
        backgroundColor: colors.primary,
        alignSelf: 'flex-end',
        borderTopRightRadius: 2,
    },
    chatText: {
        color: colors.text,
        fontSize: 14,
        lineHeight: 20,
    },
    inputArea: {
        flexDirection: 'row',
        marginTop: 10,
        alignItems: 'center',
    },
    input: {
        flex: 1,
        backgroundColor: colors.surfaceLight,
        borderRadius: 8,
        padding: 12,
        color: colors.text,
        marginRight: 10,
        borderWidth: 1,
        borderColor: colors.border,
        ...Platform.select({
            web: { outlineStyle: 'none' },
            default: {}
        }) as any
    },
    sendBtn: {
        backgroundColor: colors.primary,
        padding: 12,
        borderRadius: 8,
    },
    logScroll: {
        flex: 1,
    },
    nodeGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: 16,
    },
    statRow: {
        marginBottom: 16,
    },
    statLabel: {
        color: colors.textDim,
        fontSize: 10,
        marginBottom: 6,
    },
    statValue: {
        color: colors.text,
        fontSize: 18,
        fontWeight: 'bold',
    },
    progressBarBg: {
        height: 6,
        backgroundColor: colors.surfaceLight,
        borderRadius: 3,
        overflow: 'hidden',
    },
    progressBarFill: {
        height: '100%',
        backgroundColor: colors.primary,
        borderRadius: 3,
    },
});
