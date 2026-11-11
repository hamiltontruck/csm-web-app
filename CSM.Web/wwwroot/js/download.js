window.downloadFileFromBytes = (fileName, contentType, base64) => {
    const bytes = Uint8Array.from(atob(base64), character => character.charCodeAt(0));
    const url = window.URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    try { link.click(); }
    finally { link.remove(); setTimeout(() => window.URL.revokeObjectURL(url), 1000); }
};

window.downloadPdf = (fileName, base64) => {
    const link = document.createElement("a");
    link.href = "data:application/pdf;base64," + base64;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
