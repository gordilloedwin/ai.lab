// Simple scroll listener for chat messages container.
// Calls DotNet.invokeMethodAsync to notify the Blazor component when user is near bottom.
// Debounce is handled in .NET side; here we just fire rapidly when near bottom threshold.

// AILabChat smart auto-scroll module with read receipt integration
// Uses DotNetObjectReference pattern for reliable Blazor Server interop

window.AILabChat = (function() {
    const nearBottomThresholdPx = 120;
    const userScrollAwayThresholdPx = 240;
    let el = null;
    let userScrolledAway = false;
    let dotNetHelper = null;
    let lastScrollAt = 0;
    let lastReadNotifyAt = 0;
    const minScrollInterval = 75;
    const minReadNotifyInterval = 500; // Throttle read receipt calls

    function init(containerId, dotNetRef) {
        el = document.getElementById(containerId);
        if (!el) {
            console.warn('Chat container not found:', containerId);
            return;
        }
        
        dotNetHelper = dotNetRef;
        
        // Remove existing listener if re-initializing
        el.removeEventListener('scroll', handleScroll);
        el.addEventListener('scroll', handleScroll, { passive: true });
        
        // Initial snap to bottom
        scrollToBottom(true);
    }

    function handleScroll() {
        if (!el) return;
        
        const distance = distanceFromBottom();
        userScrolledAway = distance > userScrollAwayThresholdPx;
        
        // Notify .NET when near bottom for read receipts
        if (distance <= nearBottomThresholdPx && dotNetHelper) {
            const now = performance.now();
            if (now - lastReadNotifyAt > minReadNotifyInterval) {
                lastReadNotifyAt = now;
                dotNetHelper.invokeMethodAsync('OnScrolledNearBottom')
                    .catch(err => console.warn('Read receipt notify failed:', err));
            }
        }
    }

    function distanceFromBottom() {
        if (!el) return Infinity;
        return el.scrollHeight - el.scrollTop - el.clientHeight;
    }

    function scrollToBottom(immediate = false) {
        if (!el) return;
        const now = performance.now();
        if (!immediate && (now - lastScrollAt) < minScrollInterval) return;
        
        el.scrollTo({ 
            top: el.scrollHeight, 
            behavior: immediate ? 'auto' : 'smooth' 
        });
        lastScrollAt = now;
    }

    function notifyNewMessage() {
        if (!el) return;
        
        const distance = distanceFromBottom();
        const nearBottom = distance <= nearBottomThresholdPx;
        
        // Auto-scroll if near bottom or not scrolled away
        if (nearBottom || !userScrolledAway) {
            scrollToBottom(false);
            userScrolledAway = false;
        }
    }

    function forceScroll() {
        scrollToBottom(false);
        userScrolledAway = false;
    }

    function dispose() {
        if (el) {
            el.removeEventListener('scroll', handleScroll);
        }
        dotNetHelper = null;
        el = null;
    }

    return { init, notifyNewMessage, forceScroll, dispose };
})();