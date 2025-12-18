export const colors = {
    background: '#030014', // Deep space blue/black
    surface: '#0F172A', // Slate 900
    surfaceLight: '#1E293B', // Slate 800
    primary: '#7C3AED', // Violet 600
    primaryGlow: '#8B5CF6', // Violet 500
    secondary: '#10B981', // Emerald 500
    accent: '#F43F5E', // Rose 500
    text: '#F8FAFC', // Slate 50
    textDim: '#94A3B8', // Slate 400
    border: '#334155', // Slate 700
    terminal: '#0fa37f', // ChatGPT green logic style or retro green
    terminalBg: '#02040a',
};

export const shadows = {
    glow: {
        shadowColor: colors.primaryGlow,
        shadowOffset: { width: 0, height: 0 },
        shadowOpacity: 0.8,
        shadowRadius: 10,
        elevation: 10, // Android
    },
    card: {
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.3,
        shadowRadius: 5,
        elevation: 5,
    }
};
