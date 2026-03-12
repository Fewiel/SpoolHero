window.clipboardHelper = {
    copy: async (text) => {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    }
};
