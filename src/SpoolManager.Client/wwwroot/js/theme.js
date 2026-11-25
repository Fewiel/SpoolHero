window.themeHelper = {
    // Stored preference: 'light', 'dark' or 'auto' (follow the operating system)
    getPreference: () => {
        try { return localStorage.getItem('theme') || 'auto'; } catch { return 'auto'; }
    },

    resolve: (pref) => {
        if (pref === 'light' || pref === 'dark') return pref;
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    // Effective theme currently shown ('light' or 'dark')
    getTheme: () => window.themeHelper.resolve(window.themeHelper.getPreference()),

    apply: () => {
        const theme = window.themeHelper.getTheme();
        document.documentElement.setAttribute('data-bs-theme', theme);
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', theme === 'dark' ? '#0C0E13' : '#F4F5F7');
    },

    setTheme: (pref) => {
        try { localStorage.setItem('theme', pref); } catch { }
        window.themeHelper.apply();
    },

    init: () => {
        window.themeHelper.apply();
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
                if (window.themeHelper.getPreference() === 'auto') window.themeHelper.apply();
            });
        }
    }
};

window.themeHelper.init();

const _docMeta = {
    de: {
        lang: 'de',
        title: 'SpoolHero \u2013 Filament-Verwaltung f\u00fcr 3D-Drucker',
        description: 'SpoolHero ist die kostenlose L\u00f6sung zur Verwaltung von 3D-Druck-Filamentspulen. RFID-Tracking, Material-Datenbank, Team-Projekte, Trockner-\u00dcberwachung und mehr.',
        ogLocale: 'de_DE'
    },
    en: {
        lang: 'en',
        title: 'SpoolHero \u2013 Filament Management for 3D Printers',
        description: 'SpoolHero is the free solution for managing 3D printing filament spools. RFID tracking, material database, team projects, dryer monitoring and more.',
        ogLocale: 'en_US'
    }
};

window.spoolHeroGetBrowserLang = () => {
    const lang = (navigator.language || navigator.userLanguage || 'en').toLowerCase();
    return lang.startsWith('de') ? 'de' : 'en';
};

window.spoolHeroSetDocMeta = (lang) => {
    const m = _docMeta[lang] || _docMeta['de'];
    document.documentElement.lang = m.lang;
    document.title = m.title;
    const setMeta = (sel, val) => { const el = document.querySelector(sel); if (el) el.setAttribute('content', val); };
    setMeta('meta[name="description"]', m.description);
    setMeta('meta[property="og:title"]', m.title);
    setMeta('meta[property="og:description"]', m.description);
    setMeta('meta[property="og:locale"]', m.ogLocale);
    setMeta('meta[name="twitter:title"]', m.title);
    setMeta('meta[name="twitter:description"]', m.description);
};

// Ctrl/Cmd + K (or "/" outside of inputs) focuses the global search
document.addEventListener('keydown', (e) => {
    const target = e.target;
    const typing = target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable);
    const isShortcut = ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') || (!typing && e.key === '/');
    if (!isShortcut) return;
    const input = document.getElementById('sh-search-input');
    if (!input || input.offsetParent === null) return;
    e.preventDefault();
    input.focus();
    input.select();
});
