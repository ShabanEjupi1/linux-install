// Triggers a browser download of a base64-encoded file produced server-side.
// Used by the Blazor circuit (JS interop) so the file never needs an auth-bearing
// HTTP endpoint of its own — the bytes come over the already-authenticated circuit.
window.kosovaDownload = function (fileName, base64, contentType) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};
