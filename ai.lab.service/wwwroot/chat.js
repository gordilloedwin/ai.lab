// Simple scroll listener for chat messages container.
// Calls DotNet.invokeMethodAsync to notify the Blazor component when user is near bottom.
// Debounce is handled in .NET side; here we just fire rapidly when near bottom threshold.

window.AILabChat = (function() {
    const thresholdPx = 80; // distance from bottom to consider 'at bottom'
    let initialized = false;

    function init(containerId) {
        if (initialized) return;
        const el = document.getElementById(containerId);
        if (!el) return;
        el.addEventListener('scroll', () => {
            const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
            if (distanceFromBottom <= thresholdPx) {
                // Notify .NET (assembly name must match project root namespace)
                DotNet.invokeMethodAsync('ai.lab.service', 'OnChatScrolledNearBottom')
                    .catch(() => {});
            }
        });
        initialized = true;
    }

    return { init };
})();