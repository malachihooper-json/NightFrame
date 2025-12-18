import React from 'react';
import { View, TouchableOpacity, StyleSheet } from 'react-native';
import { Network, Terminal, Zap, Globe, LayoutDashboard, Settings } from 'lucide-react-native';
import { colors } from '../theme';

interface SidebarProps {
    activeTab: string;
    onTabChange: (tab: string) => void;
}

export const NavigationSidebar = ({ activeTab, onTabChange }: SidebarProps) => (
    <View style={styles.sidebar}>
        <View style={styles.logoContainer}>
            <Zap size={28} color={colors.primary} />
        </View>
        <TouchableOpacity onPress={() => onTabChange('dashboard')} style={[styles.navItem, activeTab === 'dashboard' && styles.navItemActive]}>
            <LayoutDashboard size={24} color={activeTab === 'dashboard' ? colors.primary : colors.textDim} />
        </TouchableOpacity>
        <TouchableOpacity onPress={() => onTabChange('nodes')} style={[styles.navItem, activeTab === 'nodes' && styles.navItemActive]}>
            <Globe size={24} color={activeTab === 'nodes' ? colors.primary : colors.textDim} />
        </TouchableOpacity>
        <TouchableOpacity onPress={() => onTabChange('terminal')} style={[styles.navItem, activeTab === 'terminal' && styles.navItemActive]}>
            <Terminal size={24} color={activeTab === 'terminal' ? colors.primary : colors.textDim} />
        </TouchableOpacity>

        <View style={{ flex: 1 }} />

        <TouchableOpacity onPress={() => onTabChange('settings')} style={[styles.navItem, activeTab === 'settings' && styles.navItemActive]}>
            <Settings size={24} color={activeTab === 'settings' ? colors.primary : colors.textDim} />
        </TouchableOpacity>
    </View>
);

const styles = StyleSheet.create({
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
});
