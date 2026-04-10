window.materialPickerOutside = {
    _handler: null,
    register: (el, dotnetRef) => {
        materialPickerOutside.unregister();
        materialPickerOutside._handler = (e) => {
            if (!el.contains(e.target)) {
                dotnetRef.invokeMethodAsync('CloseDropdown');
                materialPickerOutside.unregister();
            }
        };
        setTimeout(() => document.addEventListener('click', materialPickerOutside._handler), 0);
    },
    unregister: () => {
        if (materialPickerOutside._handler) {
            document.removeEventListener('click', materialPickerOutside._handler);
            materialPickerOutside._handler = null;
        }
    }
};
