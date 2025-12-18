import React from 'react';
import { View, StyleSheet } from 'react-native';
import { colors, shadows } from '../theme';

export const GlassPane = ({ children, style }: any) => (
    <View style={[styles.glassPane, style]}>
        {children}
    </View>
);

const styles = StyleSheet.create({
    glassPane: {
        backgroundColor: 'rgba(15, 23, 42, 0.7)',
        borderRadius: 16,
        borderWidth: 1,
        borderColor: 'rgba(255, 255, 255, 0.05)',
        padding: 20,
        ...shadows.card,
    },
});
