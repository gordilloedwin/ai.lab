// AILabChat smart auto-scroll module with DEBUG LOGGING
// Uses DotNetObjectReference pattern for reliable Blazor Server interop

window.AILabChat = (function() {
    console.log('🚀 AILabChat module loading...');
    
    const nearBottomThresholdPx = 120;
    const userScrollAwayThresholdPx = 240;
    let el = null;
    let userScrolledAway = false;
    let dotNetHelper = null;
    let lastScrollAt = 0;
    let lastReadNotifyAt = 0;
    const minScrollInterval = 75;
    const minReadNotifyInterval = 500;

    function init(containerId, dotNetRef) {
        console.log('🔧 AILabChat.init called', { 
            containerId, 
            hasDotNetRef: !!dotNetRef,
            dotNetRefType: typeof dotNetRef
        });
        
        el = document.getElementById(containerId);
        if (!el) {
            console.error('❌ Chat container not found:', containerId);
            console.log('Available elements:', document.querySelectorAll('[id]'));
            return;
        }
        
        console.log('✅ Chat container found:', el);
        console.log('Container dimensions:', {
            scrollHeight: el.scrollHeight,
            clientHeight: el.clientHeight,
            scrollTop: el.scrollTop
        });
        
        dotNetHelper = dotNetRef;
        
        // Remove existing listener if re-initializing
        el.removeEventListener('scroll', handleScroll);
        el.addEventListener('scroll', handleScroll, { passive: true });
        
        console.log('✅ Scroll listener attached');
        
        // Initial snap to bottom
        scrollToBottom(true);
        console.log('✅ Initial scroll to bottom complete');
    }

    function handleScroll() {
        if (!el) {
            console.warn('⚠️ handleScroll called but el is null');
            return;
        }
        
        const distance = distanceFromBottom();
        const wasScrolledAway = userScrolledAway;
        userScrolledAway = distance > userScrollAwayThresholdPx;
        
        if (wasScrolledAway !== userScrolledAway) {
            console.log('📜 Scroll state changed:', { 
                distance, 
                userScrolledAway, 
                hasDotNetHelper: !!dotNetHelper 
            });
        }
        
        // Notify .NET when near bottom for read receipts
        if (distance <= nearBottomThresholdPx && dotNetHelper) {
            const now = performance.now();
            if (now - lastReadNotifyAt > minReadNotifyInterval) {
                lastReadNotifyAt = now;
                console.log('📨 Calling OnScrolledNearBottom...');
                dotNetHelper.invokeMethodAsync('OnScrolledNearBottom')
                    .then(() => console.log('✅ OnScrolledNearBottom succeeded'))
                    .catch(err => console.error('❌ OnScrolledNearBottom failed:', err));
            }
        }
    }

    function distanceFromBottom() {
        if (!el) return Infinity;
        const dist = el.scrollHeight - el.scrollTop - el.clientHeight;
        return dist;
    }

    function scrollToBottom(immediate = false) {
        if (!el) {
            console.warn('⚠️ scrollToBottom called but el is null');
            return;
        }
        
        const now = performance.now();
        if (!immediate && (now - lastScrollAt) < minScrollInterval) {
            console.log('⏱️ Scroll throttled');
            return;
        }
        
        console.log('⬇️ Scrolling to bottom', { 
            immediate, 
            scrollHeight: el.scrollHeight,
            currentTop: el.scrollTop
        });
        
        el.scrollTo({ 
            top: el.scrollHeight, 
            behavior: immediate ? 'auto' : 'smooth' 
        });
        lastScrollAt = now;
    }

    function notifyNewMessage() {
        console.log('📬 notifyNewMessage called');
        
        if (!el) {
            console.error('❌ notifyNewMessage: el is null');
            return;
        }
        
        const distance = distanceFromBottom();
        const nearBottom = distance <= nearBottomThresholdPx;
        
        console.log('📬 Message state:', { 
            distance, 
            nearBottom, 
            userScrolledAway,
            willScroll: nearBottom || !userScrolledAway
        });
        
        // Auto-scroll if near bottom or not scrolled away
        if (nearBottom || !userScrolledAway) {
            console.log('✅ Auto-scrolling to bottom');
            scrollToBottom(false);
            userScrolledAway = false;
        } else {
            console.log('⏸️ Auto-scroll skipped (user scrolled away)');
        }
    }

    function forceScroll() {
        console.log('🔨 Force scroll called');
        scrollToBottom(false);
        userScrolledAway = false;
    }

    function dispose() {
        console.log('🗑️ Disposing AILabChat');
        if (el) {
            el.removeEventListener('scroll', handleScroll);
        }
        dotNetHelper = null;
        el = null;
    }

    console.log('✅ AILabChat module loaded successfully');
    return { init, notifyNewMessage, forceScroll, dispose };
})();

console.log('✅ chat.js file executed, window.AILabChat =', window.AILabChat);
