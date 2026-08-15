window.commanderPeriscope = {
    getLocalStorage: function (key) {
        return localStorage.getItem(key);
    },
    setLocalStorage: function (key, value) {
        localStorage.setItem(key, value);
    },
    copyToClipboard: function (text) {
        return navigator.clipboard.writeText(text);
    },
};
