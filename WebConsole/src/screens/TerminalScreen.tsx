import React, { useState, useRef, useEffect } from 'react';
import { View, Text, TextInput, ScrollView, StyleSheet, Platform } from 'react-native';
import { colors } from '../theme';
import { GlassPane } from '../components/GlassPane';
import { agentApi } from '../api/agent';

export const TerminalScreen = () => {
    const [history, setHistory] = useState<string[]>(['NIGHTFRAME CORE [Version 1.0.0]', '(c) Nightframe Systems. All rights reserved.', '', 'Authorized Access Verified.', 'Type "help" for command list.']);
    const [input, setInput] = useState('');
    const scrollViewRef = useRef<ScrollView>(null);

    const handleCommand = async () => {
        if (!input.trim()) return;
        const cmd = input.trim();
        setHistory(prev => [...prev, `> ${cmd}`]);
        setInput('');

        // Process command
        let response = '';
        const parts = cmd.split(' ');

        switch (parts[0].toLowerCase()) {
            case 'help':
                response = 'Available commands: help, status, clear, scan, nodes, inject <prompt>';
                break;
            case 'clear':
                setHistory([]);
                return;
            case 'status':
                const status = await agentApi.getStatus();
                response = JSON.stringify(status, null, 2);
                break;
            case 'scan':
                response = 'Initiating network scan... [MOCK] Scan complete. 3 nodes found.';
                break;
            case 'nodes':
                const nodes = await agentApi.getNodes();
                response = nodes.map((n: any) => `${n.hostname} [${n.role}] - ${n.status}`).join('\n');
                break;
            case 'inject':
                response = await agentApi.sendMessage(parts.slice(1).join(' '));
                break;
            default:
                response = `Unknown command: ${parts[0]}`;
        }

        setHistory(prev => [...prev, response]);
    };

    return (
        <View style={styles.container}>
            <GlassPane style={styles.terminalWindow}>
                <ScrollView
                    ref={scrollViewRef}
                    style={styles.history}
                    onContentSizeChange={() => scrollViewRef.current?.scrollToEnd({ animated: true })}
                >
                    {history.map((line, i) => (
                        <Text key={i} style={styles.line}>{line}</Text>
                    ))}
                </ScrollView>
                <View style={styles.inputRow}>
                    <Text style={styles.prompt}>{'>'}</Text>
                    <TextInput
                        style={styles.input}
                        value={input}
                        onChangeText={setInput}
                        onSubmitEditing={handleCommand}
                        placeholder="Enter command..."
                        placeholderTextColor={colors.textDim}
                        autoFocus
                    />
                </View>
            </GlassPane>
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
        padding: 0,
    },
    terminalWindow: {
        flex: 1,
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
    },
    history: {
        flex: 1,
        marginBottom: 10,
    },
    line: {
        color: colors.terminal,
        fontFamily: Platform.OS === 'web' ? 'monospace' : 'System',
        fontSize: 14,
        marginBottom: 4,
    },
    inputRow: {
        flexDirection: 'row',
        alignItems: 'center',
        borderTopWidth: 1,
        borderTopColor: colors.border,
        paddingTop: 10,
    },
    prompt: {
        color: colors.primary,
        marginRight: 10,
        fontWeight: 'bold',
    },
    input: {
        flex: 1,
        color: colors.text,
        fontFamily: Platform.OS === 'web' ? 'monospace' : 'System',
        fontSize: 14,
        ...Platform.select({
            web: { outlineStyle: 'none' },
            default: {}
        }) as any
    }
});
