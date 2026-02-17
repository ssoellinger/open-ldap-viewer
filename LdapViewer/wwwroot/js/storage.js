// Factory for simple load/save localStorage objects
function createStorage(key, defaultValue) {
    return {
        load: function () { return localStorage.getItem(key) || defaultValue; },
        save: function (json) { localStorage.setItem(key, json); }
    };
}

// Connection storage has extra methods for last-connection
window.connectionStorage = {
    load: function () { return localStorage.getItem("ldap-connections") || "[]"; },
    save: function (json) { localStorage.setItem("ldap-connections", json); },
    loadLastConnection: function () { return localStorage.getItem("ldap-last-connection") || ""; },
    saveLastConnection: function (json) { localStorage.setItem("ldap-last-connection", json); },
    clearLastConnection: function () { localStorage.removeItem("ldap-last-connection"); }
};

window.bookmarkStorage = createStorage("ldap-bookmarks", "[]");
window.searchHistoryStorage = createStorage("ldap-search-history", "[]");
window.savedSearchStorage = createStorage("ldap-saved-searches", "[]");
window.darkModeStorage = createStorage("ldap-dark-mode", "false");

window.clipboardCopy = function (text) {
    return navigator.clipboard.writeText(text);
};

window.setDarkMode = function (enabled) {
    if (enabled) {
        document.documentElement.setAttribute("data-theme", "dark");
    } else {
        document.documentElement.removeAttribute("data-theme");
    }
};

window.toggleDarkMode = function () {
    var current = localStorage.getItem("ldap-dark-mode") === "true";
    var next = !current;
    localStorage.setItem("ldap-dark-mode", next.toString());
    window.setDarkMode(next);
};

// Apply dark mode on page load and after Blazor enhanced navigation
(function () {
    function applyDarkMode() {
        if (localStorage.getItem("ldap-dark-mode") === "true") {
            document.documentElement.setAttribute("data-theme", "dark");
        }
    }
    applyDarkMode();

    document.addEventListener("DOMContentLoaded", function () {
        if (window.Blazor) {
            Blazor.addEventListener("enhancedload", applyDarkMode);
        }
    });
})();

window.setLanguageCookie = function (culture) {
    document.cookie = ".AspNetCore.Culture=c=" + culture + "|uic=" + culture + ";path=/;max-age=31536000;samesite=lax";
};

window.downloadFile = function (filename, content) {
    var blob = new Blob([content], { type: "text/plain;charset=utf-8" });
    var url = URL.createObjectURL(blob);
    var a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
