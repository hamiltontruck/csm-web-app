(() => {
    const root = document.documentElement;
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    let theme = 'system';
    const read = key => {
        const entry = document.cookie.split(';').map(x => x.trim()).find(x => x.startsWith(key + '='));
        return entry ? entry.slice(key.length + 1) : null;
    };
    const cookie = (key, value) => { document.cookie = `${key}=${encodeURIComponent(value)};path=/;max-age=31536000;samesite=lax`; };
    const normalize = value => ['light', 'dark', 'system'].includes(String(value).toLowerCase()) ? String(value).toLowerCase() : 'system';
    const paint = dark => {
        const resolved = theme === 'system' ? (dark ? 'dark' : 'light') : theme;
        root.setAttribute('data-theme', resolved); root.setAttribute('data-bs-theme', resolved);
    };
    window.csmTheme = {
        apply(value) { theme = normalize(value); paint(media.matches); },
        persist(value) { this.apply(value); cookie('csm-theme', theme); },
        setCompact(value) { root.classList.toggle('csm-compact', value === true); },
        persistCompact(value) { this.setCompact(value); cookie('csm-compact', value === true ? 'true' : 'false'); }
    };
    media.addEventListener('change', event => paint(event.matches));
    window.csmTheme.apply(read('csm-theme') || 'system');
    window.csmTheme.setCompact(read('csm-compact') === 'true');
    window.csmCulture = {
        set(language) {
            if (!['en', 'am', 'om'].includes(language)) return false;
            const value = `c=${language}|uic=${language}`;
            cookie('.AspNetCore.Culture', value); return read('.AspNetCore.Culture') === encodeURIComponent(value);
        }
    };
    window.csmNavigation = {
        load(user, group, remember) {
            if (!remember || !user) return true;
            try { return window.sessionStorage.getItem(`csm-nav:${user}:${group}`) !== 'false'; }
            catch { return true; }
        },
        save(user, group, expanded, remember) {
            if (!remember || !user) return;
            try { window.sessionStorage.setItem(`csm-nav:${user}:${group}`, expanded ? 'true' : 'false'); }
            catch { /* Browser storage may be unavailable. Navigation still works. */ }
        }
    };
})();
