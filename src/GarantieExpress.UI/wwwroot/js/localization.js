const languageKey = "garantie-express-language";

function readStoredLanguage() {
    let stored = null;
    try {
        stored = localStorage.getItem(languageKey);
    } catch { /* Storage may be disabled. */ }
    return stored;
}

function normalizeLanguage(preferred) {
    if (preferred.toLowerCase().startsWith("fr")) return "fr-BE";
    if (preferred.toLowerCase().startsWith("nl")) return "nl-BE";
    return "en";
}

export function getLanguage() {
    return normalizeLanguage(readStoredLanguage() || navigator.language || "nl-BE");
}

export function applyLanguage(language, persist) {
    document.documentElement.lang = language;
    if (persist) {
        try { localStorage.setItem(languageKey, language); } catch { /* Keep the session language only. */ }
    }

    const copy = language.startsWith("fr")
        ? ["Une erreur inattendue s’est produite.", "Recharger"]
        : language.startsWith("nl")
            ? ["Er is een onverwachte fout opgetreden.", "Herladen"]
            : ["An unexpected error occurred.", "Reload"];
    const errorMessage = document.querySelector("#blazor-error-ui .error-message");
    const reloadLink = document.querySelector("#blazor-error-ui .reload");
    if (errorMessage) errorMessage.textContent = copy[0];
    if (reloadLink) reloadLink.textContent = copy[1];
}

export function bootstrapLanguage() {
    applyLanguage(getLanguage(), false);
}
