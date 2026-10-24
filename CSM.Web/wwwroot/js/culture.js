window.csmCulture = {
    set: function (culture) {
        const value = `c=${culture}|uic=${culture}`;

        document.cookie =
            `.AspNetCore.Culture=${value};path=/;max-age=31536000;samesite=lax`;
    }
};