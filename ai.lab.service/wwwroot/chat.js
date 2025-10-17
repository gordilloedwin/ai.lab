// Simple scroll listener for chat messages container.
// Calls DotNet.invokeMethodAsync to notify the Blazor component when user is near bottom.
// Debounce is handled in .NET side; here we just fire rapidly when near bottom threshold.

// AILabChat smart auto-scroll module
// Behavior:
// - Automatically scrolls to bottom when new messages arrive IF user is near bottom.
// - If user scrolls up beyond a threshold, auto-scroll pauses until user returns near bottom.
// - Provides API: init(containerId), notifyNewMessage(), forceScroll()
// - Calls .NET method OnChatScrolledNearBottom when near bottom (read receipts debounce handled in .NET).

window.AILabChat = (function() {
    const nearBottomThresholdPx = 120; // distance from bottom considered 'near bottom'
    const userScrollAwayThresholdPx = 240; // beyond this user considered reading history
    let el = null;
    let userScrolledAway = false;
    let initialized = false;
    let pendingNotify = 0;
    let lastScrollAt = 0;
    const minInterval = 75; // ms throttle between auto-scroll attempts

    function init(containerId) {
        el = document.getElementById(containerId);
        if (!el || initialized) return;
        el.addEventListener('scroll', handleScroll, { passive: true });
        initialized = true;
        // Initial snap to bottom
        scrollToBottom(true);
    }

    function handleScroll() {
        const distance = distanceFromBottom();
        // User considered reading history if far from bottom
        userScrolledAway = distance > userScrollAwayThresholdPx;
        // Note: Read receipt handling is done through hub events, not JS interop
    }

    function distanceFromBottom() {
        if (!el) return Infinity;
        return el.scrollHeight - el.scrollTop - el.clientHeight;
    }

    function scrollToBottom(immediate = false) {
        if (!el) return;
        const now = performance.now();
        if (!immediate && (now - lastScrollAt) < minInterval) return; // throttle
        el.scrollTo({ top: el.scrollHeight, behavior: immediate ? 'auto' : 'smooth' });
        lastScrollAt = now;
    }

    function notifyNewMessage() {
        // Called when a new message element is rendered.
        pendingNotify++;
        // If user is near bottom OR not scrolledAway, auto scroll.
        const distance = distanceFromBottom();
        const nearBottom = distance <= nearBottomThresholdPx;
        if (nearBottom || !userScrolledAway) {
            scrollToBottom(false);
            userScrolledAway = false; // reset if we snap
        }
        // If userScrolledAway remains true, we do nothing (respect reading state).
    }

    function forceScroll() {
        scrollToBottom(false);
        userScrolledAway = false;
    }

    return { init, notifyNewMessage, forceScroll };
})();