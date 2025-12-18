import React, { useState, useEffect } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, Platform, Switch } from 'react-native';
import { Settings, Save, Server, Shield, Radio, Globe } from 'lucide-react-native';
import { colors, shadows } from '../theme';
import { GlassPane } from '../components/GlassPane';
import { getApiUrl, setApiUrl } from '../api/agent';

export const SettingsScreen = () => {
    const [url, setUrl] = useState(getApiUrl());
    const [autoConnect, setAutoConnect] = useState(true);
    const [isSaved, setIsSaved] = useState(false);

    const handleSave = () => {
        setApiUrl(url);
        setIsSaved(true);
        setTimeout(() => setIsSaved(false), 2000);
    };

    return (
        <View style={styles.container}>
            <View style={styles.header}>
                <View style={styles.iconBox}>
                    <Settings size={24} color={colors.primary} />
                </View>
                <View>
                    <Text style={styles.title}>CORE CONFIGURATION</Text>
                    <Text style={styles.subtitle}>Network Link & System Parameters</Text>
                </View>
            </View>

            <View style={styles.grid}>
                <GlassPane style={styles.card}>
                    <View style={styles.cardHeader}>
                        <Server size={20} color={colors.text} />
                        <Text style={styles.cardTitle}>NEURAL BRIDGE CONNECTION</Text>
                    </View>

                    <Text style={styles.label}>PRIMARY NODE UPLINK</Text>
                    <View style={styles.inputGroup}>
                        <TextInput
                            style={styles.input}
                            value={url}
                            onChangeText={setUrl}
                            placeholder="http://localhost:7777"
                            placeholderTextColor={colors.textDim}
                        />
                    </View>
                    <Text style={styles.hint}>
                        Point this to the IP address of the managed Node or Network Uplink.
                        Use 'localhost' if running locally.
                    </Text>

                    <View style={styles.switchRow}>
                        <Text style={styles.switchLabel}>Auto-Reconnect</Text>
                        <Switch
                            value={autoConnect}
                            onValueChange={setAutoConnect}
                            trackColor={{ false: colors.surfaceLight, true: colors.primary }}
                        />
                    </View>

                    <TouchableOpacity style={styles.saveBtn} onPress={handleSave}>
                        {isSaved ? <Shield size={18} color={colors.background} /> : <Save size={18} color={colors.background} />}
                        <Text style={styles.saveText}>{isSaved ? 'CONFIGURATION SAVED' : 'SAVE CONFIGURATION'}</Text>
                    </TouchableOpacity>
                </GlassPane>

                <GlassPane style={styles.card}>
                    <View style={styles.cardHeader}>
                        <Globe size={20} color={colors.text} />
                        <Text style={styles.cardTitle}>ACCESS CONTROL</Text>
                    </View>

                    <View style={styles.infoRow}>
                        <Text style={styles.infoLabel}>Role</Text>
                        <Text style={styles.infoValue}>Administrator</Text>
                    </View>
                    <View style={styles.infoRow}>
                        <Text style={styles.infoLabel}>Access Level</Text>
                        <Text style={styles.infoValue}>Level 5 (Unrestricted)</Text>
                    </View>
                    <View style={styles.infoRow}>
                        <Text style={styles.infoLabel}>Session ID</Text>
                        <Text style={styles.infoValue}>SESS_{Math.floor(Math.random() * 10000).toString(16).toUpperCase()}</Text>
                    </View>

                    <TouchableOpacity style={[styles.saveBtn, { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.accent, marginTop: 20 }]}>
                        <Text style={[styles.saveText, { color: colors.accent }]}>TERMINATE SESSION</Text>
                    </TouchableOpacity>
                </GlassPane>
            </View>
        </View>
    );
};

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    header: {
        flexDirection: 'row',
        alignItems: 'center',
        marginBottom: 30,
    },
    iconBox: {
        width: 48,
        height: 48,
        borderRadius: 12,
        backgroundColor: 'rgba(124, 58, 237, 0.1)',
        alignItems: 'center',
        justifyContent: 'center',
        marginRight: 16,
        borderWidth: 1,
        borderColor: colors.primary,
    },
    title: {
        color: colors.text,
        fontSize: 18,
        fontWeight: 'bold',
        letterSpacing: 1,
    },
    subtitle: {
        color: colors.textDim,
        fontSize: 12,
        letterSpacing: 0.5,
    },
    grid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: 20,
    },
    card: {
        flex: 1,
        minWidth: 300,
    },
    cardHeader: {
        flexDirection: 'row',
        alignItems: 'center',
        marginBottom: 20,
        gap: 10,
    },
    cardTitle: {
        color: colors.text,
        fontWeight: 'bold',
        fontSize: 14,
        letterSpacing: 1,
    },
    label: {
        color: colors.textDim,
        fontSize: 11,
        fontWeight: '600',
        marginBottom: 8,
        textTransform: 'uppercase',
    },
    inputGroup: {
        marginBottom: 8,
    },
    input: {
        backgroundColor: colors.surfaceLight,
        borderRadius: 8,
        padding: 12,
        color: colors.text,
        borderWidth: 1,
        borderColor: colors.border,
        fontSize: 14,
        fontFamily: Platform.OS === 'web' ? 'monospace' : 'System',
        ...Platform.select({
            web: { outlineStyle: 'none' },
            default: {}
        }) as any
    },
    hint: {
        color: colors.textDim,
        fontSize: 11,
        marginBottom: 24,
        lineHeight: 16,
    },
    switchRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: 24,
        paddingVertical: 8,
        borderTopWidth: 1,
        borderBottomWidth: 1,
        borderColor: 'rgba(255,255,255,0.05)',
    },
    switchLabel: {
        color: colors.text,
        fontSize: 14,
    },
    saveBtn: {
        backgroundColor: colors.primary,
        borderRadius: 8,
        padding: 14,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 8,
    },
    saveText: {
        color: colors.background,
        fontWeight: 'bold',
        fontSize: 12,
        letterSpacing: 1,
    },
    infoRow: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        paddingVertical: 12,
        borderBottomWidth: 1,
        borderColor: 'rgba(255,255,255,0.05)',
    },
    infoLabel: {
        color: colors.textDim,
    },
    infoValue: {
        color: colors.text,
        fontWeight: '600',
        fontFamily: Platform.OS === 'web' ? 'monospace' : 'System',
    },
});
