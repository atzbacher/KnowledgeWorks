const AppOptions = {
    set(name, value) {
        // Store the option
        this[name] = value;
    },
    get(name) {
        // Return the stored value or default
        if (name === 'annotationEditorMode') {
            return 0; // 0 = NONE (allows switching), -1 = DISABLE (blocks all editing)
        }
        return this[name];
    }
};

// Make it available globally
if (typeof window !== 'undefined') {
    window.AppOptions = AppOptions;
}