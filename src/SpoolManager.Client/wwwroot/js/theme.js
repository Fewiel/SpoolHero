window.themeHelper = {
    getTheme: () => localStorage.getItem('theme') || 'light',

    setTheme: (theme) => {
        localStorage.setItem('theme', theme);
        document.documentElement.setAttribute('data-bs-theme', theme);
    },

    init: () => {
        const saved = localStorage.getItem('theme') || 'light';
        document.documentElement.setAttribute('data-bs-theme', saved);
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
