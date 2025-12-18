import React, { useEffect } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import Animated, { useAnimatedStyle, withRepeat, withTiming, useSharedValue, withSequence, Easing } from 'react-native-reanimated';
import { Server } from 'lucide-react-native';
import { colors } from '../theme';

export const NodeOrb = ({ node, index }: any) => {
    const scale = useSharedValue(1);
    const opacity = useSharedValue(0.5);

    useEffect(() => {
        scale.value = withRepeat(
            withSequence(
                withTiming(1.2, { duration: 1000 + (index * 200), easing: Easing.inOut(Easing.ease) }),
                withTiming(1, { duration: 1000 + (index * 200), easing: Easing.inOut(Easing.ease) })
            ),
            -1,
            true
        );
        opacity.value = withRepeat(
            withTiming(0.8, { duration: 1500 }),
            -1,
            true
        );
    }, []);

    const animatedStyle = useAnimatedStyle(() => ({
        transform: [{ scale: scale.value }],
        opacity: opacity.value,
    }));

    const getNodeColor = (role: string) => {
        switch (role) {
            case 'Compute': return colors.primary;
            case 'Infiltration': return colors.accent;
            case 'Defense': return colors.secondary;
            default: return colors.textDim;
        }
    };

    return (
        <View style={styles.nodeContainer}>
            <Animated.View style={[styles.nodeGlow, animatedStyle, { backgroundColor: getNodeColor(node.role) }]} />
            <View style={[styles.nodeCore, { borderColor: getNodeColor(node.role) }]}>
                <Server size={16} color={colors.text} />
            </View>
            <Text style={styles.nodeLabel}>{node.hostname}</Text>
            <Text style={styles.nodeSubLabel}>{node.role} • {Math.round(node.cpuLoad * 100)}%</Text>
        </View>
    );
};

const styles = StyleSheet.create({
    nodeContainer: {
        alignItems: 'center',
        width: 80,
        marginBottom: 16,
    },
    nodeGlow: {
        width: 40,
        height: 40,
        borderRadius: 20,
        position: 'absolute',
        top: 5,
    },
    nodeCore: {
        width: 50,
        height: 50,
        borderRadius: 25,
        backgroundColor: colors.surface,
        borderWidth: 2,
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 2,
        marginBottom: 8,
    },
    nodeLabel: {
        color: colors.text,
        fontSize: 12,
        fontWeight: '600',
        textAlign: 'center',
    },
    nodeSubLabel: {
        color: colors.textDim,
        fontSize: 10,
        textAlign: 'center',
    },
});
