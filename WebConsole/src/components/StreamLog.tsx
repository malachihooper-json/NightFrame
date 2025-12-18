import React from 'react';
import { View, Text, StyleSheet, Platform } from 'react-native';
import { colors } from '../theme';

export const StreamLog = ({ log }: { log: string }) => (
    <View style={styles.logEntry}>
        <Text style={styles.logPrefix}>›</Text>
        <Text style={styles.logText}>{log}</Text>
    </View>
);

const styles = StyleSheet.create({
    logEntry: {
        flexDirection: 'row',
        marginBottom: 4,
    },
    logPrefix: {
        color: colors.primary,
        marginRight: 8,
        fontWeight: 'bold',
    },
    logText: {
        color: colors.terminal,
        fontSize: 12,
        fontFamily: Platform.OS === 'web' ? 'monospace' : 'System',
    },
});
