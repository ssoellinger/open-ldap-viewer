window.connectionStorage = {
    load: function () {
        return localStorage.getItem("ldap-connections") || "[]";
    },
    save: function (json) {
        localStorage.setItem("ldap-connections", json);
    },
    loadLastConnection: function () {
        return localStorage.getItem("ldap-last-connection") || "";
    },
    saveLastConnection: function (json) {
        localStorage.setItem("ldap-last-connection", json);
    },
    clearLastConnection: function () {
        localStorage.removeItem("ldap-last-connection");
    }
};

window.bookmarkStorage = {
    load: function () {
        return localStorage.getItem("ldap-bookmarks") || "[]";
    },
    save: function (json) {
        localStorage.setItem("ldap-bookmarks", json);
    }
};

window.clipboardCopy = function (text) {
    return navigator.clipboard.writeText(text);
};

window.searchHistoryStorage = {
    load: function () {
        return localStorage.getItem("ldap-search-history") || "[]";
    },
    save: function (json) {
        localStorage.setItem("ldap-search-history", json);
    }
};

window.darkModeStorage = {
    load: function () {
        return localStorage.getItem("ldap-dark-mode") || "false";
    },
    save: function (enabled) {
        localStorage.setItem("ldap-dark-mode", enabled);
    }
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

    // Re-apply after Blazor enhanced navigation (DOM gets patched)
    document.addEventListener("DOMContentLoaded", function () {
        if (window.Blazor) {
            Blazor.addEventListener("enhancedload", applyDarkMode);
        }
    });
})();

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
